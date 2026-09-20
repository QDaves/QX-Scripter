using Avalonia.Controls;
using Avalonia.Input;

namespace Qx.Desktop.Controls;

public sealed class SearchBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(SearchBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text))
        {
            Clear();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
