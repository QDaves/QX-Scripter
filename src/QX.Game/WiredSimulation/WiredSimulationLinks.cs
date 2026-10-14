using System.Globalization;
using System.Text;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed record WiredSimulationLinkEndpoint(long FurnitureId, string Network,
    bool CanSend = false, bool CanReceive = false);

public sealed record WiredSimulationDataValue(long? Number = null, string? Text = null)
{
    public string AsText() => Number?.ToString(CultureInfo.InvariantCulture) ?? Text ?? "";
}

public sealed record WiredSimulationLinkPayload(string Network, long SourceRoomId, long SourceFurnitureId,
    string? Message = null, IReadOnlyDictionary<string, WiredSimulationDataValue>? Dataframe = null)
{
    public WiredSimulationEvent ReceiveAt(long furniture_id, long at = 0, long? user_id = null) =>
        new(Dataframe is null ? WiredSimulationEventKind.LinkMessage : WiredSimulationEventKind.LinkDataframe,
            at, user_id, furniture_id, Text: Message, Link: this with { Dataframe = Dataframe?.ToDictionary(StringComparer.Ordinal) });
}

public sealed partial class WiredSimulation
{
    private static WiredSimulationLinkPayload copy(WiredSimulationLinkPayload payload) =>
        payload with { Dataframe = payload.Dataframe?.ToDictionary(StringComparer.Ordinal) };

    private void validate_links()
    {
        var ids = new HashSet<long>();
        foreach (var link in initial.Links ?? [])
            if (link is null || string.IsNullOrWhiteSpace(link.Network) || !ids.Add(link.FurnitureId) ||
                !furniture.TryGetValue(link.FurnitureId, out var item) || item.ClassName != "wf_link")
                fail("snapshot.link", "Link bindings require a unique existing WIRED Link and explicit network identity.");
    }

    private WiredSimulationLinkEndpoint link_endpoint(long id, bool sending, WiredSimulationNode? node = null)
    {
        var link = initial.Links?.SingleOrDefault(link => link.FurnitureId == id);
        if (link is null || !furniture.ContainsKey(id))
        {
            note(WiredSimulationSeverity.Unsupported, "link.binding_missing", "The selected endpoint has no explicit network binding.", node?.FurnitureId);
            status = WiredSimulationStatus.Faulted;
            throw new SimulationFailure();
        }
        if (sending ? !link.CanSend : !link.CanReceive)
        {
            fail("link.access", "The supplied network binding does not authorize this operation.", node?.FurnitureId);
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
        var endpoint = link_endpoint(id, false);
        if (!StringComparer.Ordinal.Equals(endpoint.Network, payload.Network) || payload.SourceRoomId <= 0 ||
            payload.SourceFurnitureId <= 0 || (payload.Message is null) == (payload.Dataframe is null) ||
            (input.Kind == WiredSimulationEventKind.LinkDataframe) != (payload.Dataframe is not null))
        {
            fail("link.payload", "Link payload kind, provenance or network does not match the receiving binding.");
            throw new SimulationFailure();
        }
        if (payload.Message is { } text && Encoding.UTF8.GetByteCount(text) > 1000 ||
            payload.Dataframe is { } cells && (cells.Count > 12 || cells.Any(pair => string.IsNullOrEmpty(pair.Key) ||
                pair.Value is null || (pair.Value.Number is null) == (pair.Value.Text is null))))
        {
            fail("link.payload", "Link payload exceeds the modeled message/row bounds or contains an ambiguous value.");
            throw new SimulationFailure();
        }
        link_speakers(frame);
    }

    private void link_speakers(Frame frame)
    {
        bool dataframe = frame.Event.Kind == WiredSimulationEventKind.LinkDataframe;
        long endpoint = frame.Event.FurnitureId!.Value;
        foreach (var speaker in nodes.Values.Where(node => node.Name == "LinkSpeaker").OrderBy(node => node.FurnitureId))
        {
            if (!furniture.ContainsKey(speaker.FurnitureId) || !speaker.Flag(dataframe ? "dataframes" : "messages")) continue;
            if (speaker.FurnitureSources is { Count: > 0 } sources && sources[0] != WiredFurnitureSource.Picked)
                unsupported(speaker, "Link Speaker projection requires its saved Link endpoint selection.");
            if (!(speaker.Furniture ?? []).Contains(endpoint)) continue;
            effects.Add(new(now, "link_speaker", speaker.FurnitureId, [endpoint], Link: copy(frame.Event.Link!)));
            record("link.speaker", speaker.FurnitureId, "Incoming Link payload selected for display.", [endpoint]);
            charge(frame, 1, speaker.FurnitureId);
        }
    }

    private bool link_trigger(WiredSimulationNode node, Frame frame, WiredSimulationEventKind kind)
    {
        if (frame.Event.Kind != kind || frame.Event.FurnitureId is not { } id || !(node.Furniture ?? []).Contains(id)) return false;
        if (kind == WiredSimulationEventKind.LinkMessage) return true;
        var values = frame.Event.Link!.Dataframe!;
        var numbers = new Dictionary<string, long>(StringComparer.Ordinal);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in node.Fields.GetValueOrDefault("rows")?.ReceiveRows ?? [])
        {
            if (!values.TryGetValue(row.Key, out var value))
                unsupported(node, "Missing incoming dataframe keys require separate server conformance: " + row.Key);
            if (row.Destination == WiredDataframeDestination.Placeholder) texts.Add(row.Placeholder, value!.AsText());
            else
            {
                if (!variables.TryGetValue(row.VariableId, out var variable) || variable.Domain != WiredSourceDomain.Context ||
                    !variable.Writable || !variable.HasValue || variable.Reference is not null || variable.PointerAccessor is not null ||
                    variable.RequiresExecutionContext == true || variable.Derived is not null)
                    unsupported(node, "A dataframe variable destination requires an explicit writable numeric context variable.");
                if (value!.Number is not { } number)
                    unsupported(node, "String-to-number dataframe conversion has no verified model.");
                numbers.Add(row.VariableId, value.Number!.Value);
            }
        }
        foreach (var pair in numbers)
        {
            frame.Context[pair.Key] = pair.Value;
            writes++;
            charge(frame, 1, node.FurnitureId);
        }
        foreach (var pair in texts)
        {
            frame.TextValues![pair.Key] = pair.Value;
            charge(frame, 1, node.FurnitureId);
        }
        return true;
    }

    private void send_link(WiredSimulationNode node, Frame frame)
    {
        var ids = fs(node, frame);
        if (ids.Length == 0) return;
        var endpoints = ids.Select(id => link_endpoint(id, true, node)).ToArray();
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
            if (Encoding.UTF8.GetByteCount(text!) > 1000)
                unsupported(node, "The expanded Link message exceeds 1000 UTF-8 bytes; overflow behavior is unverified.");
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
            var payload = new WiredSimulationLinkPayload(endpoint.Network, initial.RoomId, endpoint.FurnitureId, text, cells);
            effects.Add(new(now, "link_send", node.FurnitureId, [endpoint.FurnitureId], Link: payload));
            charge(frame, 1, node.FurnitureId);
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
