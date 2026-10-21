using System.Globalization;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

/// <summary>The hotel configuration of one WIRED Link, as read through the Link window or GetWiredLinkConfiguration.</summary>
/// <param name="FurnitureId">The Link furniture ID.</param>
/// <param name="NetworkId">The connected network, or "0" while disconnected.</param>
/// <param name="ReceiveLocal">Whether the Link receives its own sends.</param>
public sealed record WiredSimulationLinkEndpoint(long FurnitureId, string NetworkId = WiredLinkNetworks.Disconnected,
    bool ReceiveLocal = false);

public sealed record WiredSimulationDataValue(long? Number = null, string? Text = null)
{
    public string AsText() => Number?.ToString(CultureInfo.InvariantCulture) ?? Text ?? "";
}

/// <summary>A message or dataframe sent through a WIRED Link.</summary>
/// <param name="Network">The sending Link's network, or "0" for a disconnected Link.</param>
/// <param name="SourceRoomId">The sending room.</param>
/// <param name="SourceFurnitureId">The sending Link.</param>
/// <param name="Message">The message, already expanded and limited to 1000 characters.</param>
/// <param name="Dataframe">The named dataframe values in send order.</param>
public sealed record WiredSimulationLinkPayload(string Network, long SourceRoomId, long SourceFurnitureId,
    string? Message = null, IReadOnlyDictionary<string, WiredSimulationDataValue>? Dataframe = null)
{
    /// <summary>Creates the receive input for a Link in another loaded room. The hotel supplies no triggering user.</summary>
    /// <param name="furniture_id">The receiving Link, connected to the same network.</param>
    /// <param name="at">The receive time in the other simulation.</param>
    public WiredSimulationEvent ReceiveAt(long furniture_id, long at = 0) =>
        new(Dataframe is null ? WiredSimulationEventKind.LinkMessage : WiredSimulationEventKind.LinkDataframe,
            at, null, furniture_id, Text: Message, Link: this with { Dataframe = Dataframe?.ToDictionary(StringComparer.Ordinal) });
}

public sealed partial class WiredSimulation
{
    internal const int LinkMessageCharacters = 1000;
    private const string link_substitution = "WIRED Link substituted a missing or unconvertible value: ";
    private const int link_substitution_level = 3;

    private static WiredSimulationLinkPayload copy(WiredSimulationLinkPayload payload) =>
        payload with { Dataframe = payload.Dataframe?.ToDictionary(StringComparer.Ordinal) };

    private void validate_links()
    {
        var ids = new HashSet<long>();
        var networks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in initial.Links ?? [])
        {
            if (link is null || string.IsNullOrEmpty(link.NetworkId) || !ids.Add(link.FurnitureId) ||
                !furniture.TryGetValue(link.FurnitureId, out var item) || item.ClassName != "wf_link")
                fail("snapshot.link", "Link configurations require a unique existing WIRED Link and a network identifier (\"0\" when disconnected).");
            else if (link.NetworkId != WiredLinkNetworks.Disconnected && !networks.Add(link.NetworkId))
                fail("snapshot.link", "Only one Link per room may be connected to a network.", link.FurnitureId);
        }
    }

    private WiredSimulationLinkEndpoint link_endpoint(long id, WiredSimulationNode? node = null)
    {
        var link = initial.Links?.SingleOrDefault(link => link.FurnitureId == id);
        if (link is null || !furniture.ContainsKey(id))
        {
            note(WiredSimulationSeverity.Unsupported, "link.binding_missing",
                "The selected Link has no captured configuration; read it with GetWiredLinkConfiguration.", node?.FurnitureId);
            status = WiredSimulationStatus.Faulted;
            throw new SimulationFailure();
        }
        return link;
    }

    private void receive_link(Frame frame)
    {
        var input = frame.Event;
        if (input.FurnitureId is not { } id || input.Link is not { } payload)
        {
            fail("link.payload", "A Link input requires an explicit receiving endpoint and payload.");
            throw new SimulationFailure();
        }
        var endpoint = link_endpoint(id);
        bool own_room = payload.SourceRoomId == initial.RoomId;
        bool delivered = own_room
            ? payload.SourceFurnitureId == id && endpoint.ReceiveLocal
            : endpoint.NetworkId != WiredLinkNetworks.Disconnected && StringComparer.Ordinal.Equals(endpoint.NetworkId, payload.Network);
        if (!delivered || payload.SourceRoomId <= 0 || payload.SourceFurnitureId <= 0 ||
            (payload.Message is null) == (payload.Dataframe is null) ||
            (input.Kind == WiredSimulationEventKind.LinkDataframe) != (payload.Dataframe is not null) || input.UserId is not null)
        {
            fail("link.payload", own_room
                ? "Within its own room a Link receives only its own sends, and only while it receives locally."
                : "A Link receives from other rooms only through its connected network; Link inputs carry no triggering user.");
            throw new SimulationFailure();
        }
        if (payload.Message is { Length: > LinkMessageCharacters } ||
            payload.Dataframe is { } cells && (cells.Count > 12 || cells.Any(pair => string.IsNullOrEmpty(pair.Key) ||
                pair.Value is null || (pair.Value.Number is null) == (pair.Value.Text is null))))
        {
            fail("link.payload", "The Link payload exceeds 1000 message characters or 12 rows, or contains an ambiguous value.");
            throw new SimulationFailure();
        }
        link_speakers(frame);
    }

    private void link_speakers(Frame frame)
    {
        var payload = frame.Event.Link!;
        bool dataframe = frame.Event.Kind == WiredSimulationEventKind.LinkDataframe;
        long endpoint = frame.Event.FurnitureId!.Value;
        foreach (var speaker in nodes.Values.Where(node => node.Name == "LinkSpeaker").OrderBy(node => node.FurnitureId))
        {
            if (!furniture.ContainsKey(speaker.FurnitureId) || !speaker.Flag(dataframe ? "dataframes" : "messages")) continue;
            if (!(speaker.Furniture ?? []).Contains(endpoint)) continue;
            string text = dataframe
                ? string.Join("\n", payload.Dataframe!.Select(pair => $"[b]{pair.Key}[/b]: {pair.Value.AsText()}"))
                : payload.Message!;
            effects.Add(new(now, "link_speaker", speaker.FurnitureId, [endpoint], text, Link: copy(payload)));
            record("link.speaker", speaker.FurnitureId,
                "Shown as a chat bubble at the speaker's tile without a username (room unit -1, bubble style 253).", [endpoint]);
            charge(frame, 1, speaker.FurnitureId);
        }
    }

    private bool link_trigger(WiredSimulationNode node, Frame frame, WiredSimulationEventKind kind)
    {
        if (frame.Event.Kind != kind || frame.Event.FurnitureId is not { } id || !(node.Furniture ?? []).Contains(id)) return false;
        if (kind == WiredSimulationEventKind.LinkMessage) return true;
        var values = frame.Event.Link!.Dataframe!;
        foreach (var row in node.Fields.GetValueOrDefault("rows")?.ReceiveRows ?? [])
        {
            values.TryGetValue(row.Key, out var value);
            if (row.Destination == WiredDataframeDestination.Placeholder)
            {
                if (value is null) link_substituted(node, "Missing Link dataframe text: " + row.Key);
                frame.TextValues![row.Placeholder] = value?.AsText() ?? "null";
                charge(frame, 1, node.FurnitureId);
                continue;
            }
            if (!variables.TryGetValue(row.VariableId, out var variable) || variable.Domain != WiredSourceDomain.Context ||
                !variable.Writable || !variable.HasValue || variable.Reference is not null || variable.PointerAccessor is not null ||
                variable.RequiresExecutionContext == true || variable.Derived is not null)
                unsupported(node, "A dataframe variable destination requires an explicit writable numeric context variable.");
            long number;
            if (value?.Number is { } exact) number = exact;
            else if (value?.Text is { } text && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed)) number = parsed;
            else
            {
                link_substituted(node, "Missing or nonnumeric Link dataframe value: " + row.Key);
                number = -1;
            }
            frame.Context[row.VariableId] = number;
            writes++;
            charge(frame, 1, node.FurnitureId);
        }
        return true;
    }

    private void link_substituted(WiredSimulationNode node, string detail)
    {
        effects.Add(new(now, "Log", node.FurnitureId, [], link_substitution + detail, link_substitution_level));
        record("link.substituted", node.FurnitureId, "The hotel substitutes \"null\" for a text destination and -1 for a context variable, and writes a level 3 room log.");
    }

    private void send_link(WiredSimulationNode node, Frame frame)
    {
        var ids = fs(node, frame);
        if (ids.Length == 0) return;
        var endpoints = ids.Select(id => link_endpoint(id, node)).ToArray();
        string? text = null;
        IReadOnlyDictionary<string, WiredSimulationDataValue>? cells = null;
        if (node.Name == "SendLinkMessage")
        {
            text = node.Number("message_source") switch
            {
                0 when frame.Event.Kind is WiredSimulationEventKind.Chat or WiredSimulationEventKind.LinkMessage =>
                    frame.Event.Link?.Message ?? frame.Event.Text ?? "",
                1 => output_text(node, frame),
                _ => null
            };
            if (text is null) unsupported(node, "Forwarding requires an explicit chat or received Link message.");
            if (text!.Length > LinkMessageCharacters)
            {
                text = text[..LinkMessageCharacters];
                record("link.message_truncated", node.FurnitureId, "The expanded message is cut to its first 1000 characters without an error log.");
            }
        }
        else
        {
            var rows = node.Fields.GetValueOrDefault("rows")?.SendRows ?? [];
            var data = new Dictionary<string, WiredSimulationDataValue>(StringComparer.Ordinal);
            for (int index = 0; index < rows.Count; index++)
                data.Add(rows[index].Key, dataframe_value(node, frame, rows[index], index));
            cells = data;
        }
        foreach (var endpoint in endpoints)
        {
            var payload = new WiredSimulationLinkPayload(endpoint.NetworkId, initial.RoomId, endpoint.FurnitureId, text, cells);
            charge(frame, 1, node.FurnitureId);
            if (endpoint.ReceiveLocal)
            {
                var input = payload.ReceiveAt(endpoint.FurnitureId, now);
                enqueue(now, new Frame(++root_sequence, input, new(StringComparer.Ordinal), [], [], []), external: true);
                record("link.self_receive", endpoint.FurnitureId, "The sending Link receives its own payload in a new execution without a triggering user.");
            }
            if (endpoint.NetworkId != WiredLinkNetworks.Disconnected)
                effects.Add(new(now, "link_send", node.FurnitureId, [endpoint.FurnitureId], Link: payload));
            else if (!endpoint.ReceiveLocal)
                record("link.undelivered", endpoint.FurnitureId, "A disconnected Link that does not receive locally delivers nowhere.");
        }
    }

    private WiredSimulationDataValue dataframe_value(WiredSimulationNode node, Frame frame, WiredSendDataframeRow row, int index)
    {
        if (row.Source == WiredDataframeSource.Number) return new(row.Number);
        if (row.Source == WiredDataframeSource.Message)
            return new(Text: output_text(node, frame, row.Text));
        var holders = row.Target switch
        {
            WiredSourceDomain.Furniture => fs(node, frame, index + 1),
            WiredSourceDomain.User => us(node, frame, index),
            WiredSourceDomain.Global or WiredSourceDomain.Context => [0L],
            _ => []
        };
        if (holders.Length != 1) unsupported(node, "Each modeled dataframe variable row requires exactly one source holder.");
        var current = value(row.Target, holders[0], row.VariableId, frame);
        if (current is null) unsupported(node, "An absent dataframe source has no verified fallback.");
        if (row.Source == WiredDataframeSource.VariableValue) return new(current!.Value);
        string reference = resolve_reference(row.VariableId, frame);
        if (!variables.TryGetValue(reference, out var definition) || definition.Text is null)
            unsupported(node, "A text dataframe row requires explicit text connector metadata.");
        return new(Text: definition!.Text!.GetValueOrDefault(current!.Value, definition.TextFallback ?? current.Value.ToString(CultureInfo.InvariantCulture)));
    }
}
