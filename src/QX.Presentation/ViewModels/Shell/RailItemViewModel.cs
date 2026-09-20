using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Navigation;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Shell;

public sealed partial class RailItemViewModel : ObservableObject
{
    public RailItemViewModel(PageKey key, IconKind icon, string tip, IRelayCommand command)
    {
        Key = key;
        Icon = icon;
        Tip = tip ?? throw new ArgumentNullException(nameof(tip));
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public PageKey Key { get; }

    public IconKind Icon { get; }

    public string Tip { get; }

    public IRelayCommand Command { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
