using Qx.Diagnostics;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.Room;

public sealed class RoomPageContext(
    IGameGateway gateway,
    HotelContext hotel,
    IImageService images,
    IOutfitStore outfits,
    IClipboardService clipboard,
    ILauncherService launcher,
    IDialogService dialogs,
    INotificationService notifications,
    IFurniDirections directions,
    IUiDispatcher dispatcher,
    TimeProvider time,
    NoticeLine notices)
{
    public IGameGateway Gateway { get; } = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public HotelContext Hotel { get; } = hotel ?? throw new ArgumentNullException(nameof(hotel));

    public IImageService Images { get; } = images ?? throw new ArgumentNullException(nameof(images));

    public IOutfitStore Outfits { get; } = outfits ?? throw new ArgumentNullException(nameof(outfits));

    public IClipboardService Clipboard { get; } = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

    public ILauncherService Launcher { get; } = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public IDialogService Dialogs { get; } = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public INotificationService Notifications { get; } = notifications ?? throw new ArgumentNullException(nameof(notifications));

    public IFurniDirections Directions { get; } = directions ?? throw new ArgumentNullException(nameof(directions));

    public IUiDispatcher Dispatcher { get; } = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    public TimeProvider Time { get; } = time ?? throw new ArgumentNullException(nameof(time));

    public NoticeLine Notices { get; } = notices ?? throw new ArgumentNullException(nameof(notices));

    public CancellationToken Lifetime { get; internal set; }

    public Id? Self { get; internal set; }

    public string WebHost => Hotel.WebHost;

    public void Say(string text) => Notices.Show(NoticeSeverity.Success, text);

    public void Note(string text) => Notices.Show(NoticeSeverity.Info, text);

    public void Warn(string text) => Notices.Show(NoticeSeverity.Warning, text);

    public void Fail(string text, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Diag.Warn($"{text}: {error.Message}", "ui");
        Notices.Show(NoticeSeverity.Error, $"{text}: {FailureText.Describe(error)}");
    }

    public async Task CopyAsync(string text, string done, CancellationToken cancellation_token)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (await Clipboard.TrySetTextAsync(text, cancellation_token))
        {
            Say(done);
            return;
        }
        Warn("Could not reach the clipboard. Another program may be holding it.");
    }
}
