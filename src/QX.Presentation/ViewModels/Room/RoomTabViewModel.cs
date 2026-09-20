using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomTabViewModel(RoomSection section, string title, IconKind icon) : ObservableObject
{
    public RoomSection Section { get; } = section;

    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));

    public IconKind Icon { get; } = icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    public partial string CountBadge { get; private set; } = "";

    public bool HasCount => CountBadge.Length > 0;

    public void Count(int total) => CountBadge = total > 0 ? total.ToString("N0") : "";
}
