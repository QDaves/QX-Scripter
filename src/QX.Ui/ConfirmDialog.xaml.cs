using System.Windows;
using System.Windows.Input;

namespace Qx.Ui;

public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    public static bool Ask(Window owner, string title, string message, string confirmLabel)
    {
        var dialog = new ConfirmDialog(title, message, confirmLabel) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    /// <summary>Reports something the user cannot decline; only the dismiss button is shown.</summary>
    public static void Alert(Window owner, string title, string message)
    {
        var dialog = new ConfirmDialog(title, message, "OK") { Owner = owner };
        dialog.CancelButton.Visibility = Visibility.Collapsed;
        dialog.ShowDialog();
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

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
