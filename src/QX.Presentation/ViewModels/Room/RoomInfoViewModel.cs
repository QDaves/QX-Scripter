using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Collections;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomInfoViewModel : ViewModelBase
{
    readonly RoomPageContext _context;
    Id _pictured;
    string _picture_ref = "";

    public RoomInfoViewModel(RoomPageContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        CopyLink = Own(new CopyAction(
            context.Clipboard,
            context.Dispatcher,
            context.Time,
            () => LinkText,
            context.Warn));
    }

    public CopyAction CopyLink { get; }

    public ResettableCollection<RoomFact> Facts { get; } = [];

    public ResettableCollection<RoomInfoSection> Sections { get; } = [];

    [ObservableProperty]
    public partial string RoomTitle { get; private set; } = "This room";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOwner))]
    public partial string OwnerText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    public partial string Description { get; private set; } = "";

    [ObservableProperty]
    public partial string LinkText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenLinkCommand))]
    public partial bool CanOpenLink { get; private set; }

    [ObservableProperty]
    public partial ImageRequest? Thumbnail { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnailNote))]
    public partial string ThumbnailNote { get; private set; } = "";

    [ObservableProperty]
    public partial RoomRules Rules { get; private set; } = RoomRules.Unknown;

    public bool HasOwner => OwnerText.Length > 0;

    public bool HasDescription => Description.Length > 0;

    public bool HasThumbnailNote => ThumbnailNote.Length > 0;

    public void Show(RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RoomIdentity identity = snapshot.Identity;
        RoomTitle = identity.Name.Length > 0 ? identity.Name : "This room";
        OwnerText = identity.OwnerName.Length > 0 ? $"by {identity.OwnerName}" : "";
        Description = identity.Description;
        string host = string.IsNullOrWhiteSpace(_context.WebHost)
            ? HotelContext.DefaultWebHost
            : _context.WebHost;
        LinkText = $"{host}/room/{identity.RoomId}";
        CanOpenLink = identity.RoomId > 0;
        if (!Facts.SequenceEqual(snapshot.Facts) || Rules != snapshot.Rules || Sections.Count == 0)
        {
            Facts.ReplaceAll(snapshot.Facts);
            Rules = snapshot.Rules;
            Sections.ReplaceAll(GroupFacts(snapshot));
        }
        if (_pictured == identity.RoomId && _picture_ref == identity.PictureRef)
            return;
        _pictured = identity.RoomId;
        _picture_ref = identity.PictureRef;
        ReadThumbnailAsync(identity.RoomId, identity.PictureRef, _context.Lifetime).Observe("ui");
    }

    static RoomInfoSection[] GroupFacts(RoomSnapshot snapshot)
    {
        var room = new List<RoomFact>();
        var layout = new List<RoomFact>();
        var access = new List<RoomFact> { new("Entry", snapshot.Rules.Access) };
        foreach (RoomFact fact in snapshot.Facts)
        {
            List<RoomFact> group = fact.Caption switch
            {
                "FLOOR ITEMS" or "WALL ITEMS" or "ENTRY TILE" or "WALLS" or "WALL THICKNESS" or "FLOOR THICKNESS" => layout,
                "YOUR RIGHTS" or "TRADING" or "PETS" or "CONTROLLERS" or "GROUP MEMBER" or "ROOM MUTED" or "CAN MUTE" => access,
                _ => room
            };
            string caption = fact.Caption.Length == 0 ? "" : fact.Caption[..1] + fact.Caption[1..].ToLowerInvariant();
            caption = caption.Replace(" id", " ID", StringComparison.Ordinal);
            group.Add(fact with { Caption = caption });
        }
        return
        [
            new("Room", room),
            new("Furni & layout", layout),
            new("Access & moderation", [.. access,
                new("Mute", snapshot.Rules.Mute), new("Kick", snapshot.Rules.Kick), new("Ban", snapshot.Rules.Ban)]),
            new("Chat", [new("Flow", snapshot.Rules.Flow), new("Bubble", snapshot.Rules.Bubble),
                new("Scroll", snapshot.Rules.Scroll), new("Hearing", snapshot.Rules.Hearing), new("Flood", snapshot.Rules.Flood)])
        ];
    }

    [RelayCommand(CanExecute = nameof(CanOpenLink))]
    async Task OpenLinkAsync(CancellationToken cancellation_token)
    {
        if (LinkText.Length == 0)
            return;
        if (!Uri.TryCreate($"https://{LinkText}", UriKind.Absolute, out Uri? address))
            return;
        if (!await _context.Launcher.OpenUriAsync(address, cancellation_token))
            _context.Warn("Could not open this room in a browser.");
    }

    async Task ReadThumbnailAsync(Id room, string picture_ref, CancellationToken cancellation_token)
    {
        if (room <= 0)
        {
            Thumbnail = null;
            ThumbnailNote = "";
            return;
        }
        string? found = null;
        try
        {
            if (HabboUrls.OfficialRoomPicture(picture_ref) is { } official &&
                await _context.Images.PreloadAsync(official, cancellation_token))
            {
                found = official;
            }
            else if (HabboUrls.RoomThumbnail(room, _context.WebHost) is { } navigator &&
                await _context.Images.PreloadAsync(navigator, cancellation_token))
            {
                found = navigator;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            found = null;
        }
        if (_pictured != room)
            return;
        Thumbnail = found is null ? null : new ImageRequest(found, false, IconKind.Thumbnail);
        ThumbnailNote = found is null ? "no picture" : "";
    }
}

public sealed record RoomInfoSection(string Title, IReadOnlyList<RoomFact> Facts);
