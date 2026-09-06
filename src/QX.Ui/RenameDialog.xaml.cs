using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace Qx.Ui;

public partial class RenameDialog : Window
{
    private RenameDialog(string current, string title, string confirmLabel, PackIconKind icon)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        HeaderIcon.Kind = icon;
        RenameButton.Content = confirmLabel;
        AutomationProperties.SetName(RenameButton, title);
        NameBox.Text = current;
        Loaded += (_, _) =>
        {
            NameBox.SelectAll();
            NameBox.Focus();
        };
    }

    public static string? Ask(Window owner, string current) =>
        Ask(owner, current, "Rename script", "Rename", PackIconKind.RenameBox);

    public static string? Ask(Window owner, string current, string title, string confirmLabel, PackIconKind icon)
    {
        var dialog = new RenameDialog(current, title, confirmLabel, icon) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.NameBox.Text : null;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (RenameButton is not null)
            RenameButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        e.Handled = true;
        DialogResult = false;
    }
}
