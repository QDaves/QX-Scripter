using Avalonia.Controls;
using Qx.Desktop.Services;

namespace Qx.Desktop.Views.Shell;

public sealed partial class TitleBar : UserControl
{
    public TitleBar() => InitializeComponent();

    public void Reserve(TitleBarInsets insets)
    {
        LeadingReserve.Width = insets.Leading;
        TrailingReserve.Width = insets.Trailing;
    }
}
