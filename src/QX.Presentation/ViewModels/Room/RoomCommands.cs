using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Input;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Status;

namespace Qx.Presentation.ViewModels.Room;

public sealed class RoomCommands(
    INavigationService navigation,
    IPageProvider pages,
    ISessionStatusService status) : ICommandContributor
{
    public const string Visitors = "room.visitors";
    public const string Bans = "room.bans";

    readonly INavigationService _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    readonly IPageProvider _pages = pages ?? throw new ArgumentNullException(nameof(pages));
    readonly ISessionStatusService _status = status ?? throw new ArgumentNullException(nameof(status));

    public void Contribute(ICommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new AppCommand(
            Visitors,
            "People who have been in this room",
            CommandGroup.Room,
            Section(RoomSection.Visitors),
            () => _status.Current.IsInRoom,
            []));
        registry.Register(new AppCommand(
            Bans,
            "People banned from this room",
            CommandGroup.Room,
            Section(RoomSection.Bans),
            () => _status.Current.IsInRoom,
            []));
    }

    RelayCommand Section(RoomSection section) => new(() =>
    {
        _navigation.Navigate(PageKey.Room);
        ((RoomViewModel)_pages.Get(PageKey.Room)).ShowSection(section);
    });
}
