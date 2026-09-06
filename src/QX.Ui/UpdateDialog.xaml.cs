using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Qx.Updates;

namespace Qx.Ui;

public partial class UpdateDialog : Window
{
    private readonly Uri _release_uri;

    private UpdateDialog(string installed_version, GitHubRelease release)
    {
        _release_uri = release.Uri;
        InitializeComponent();
        InstalledVersionText.Text = installed_version;
        AvailableVersionText.Text = release.Version;
        ReleaseNameText.Text = string.Equals(release.Name, release.Tag, StringComparison.OrdinalIgnoreCase)
            ? "A new version is available."
            : release.Name;
        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        MaxWidth = SystemParameters.WorkArea.Width * 0.9;
        Loaded += (_, _) => OpenButton.Focus();
    }

    public static void Show(Window owner, string installed_version, GitHubRelease release)
    {
        var dialog = new UpdateDialog(installed_version, release) { Owner = owner };
        dialog.ShowDialog();
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_release_uri.AbsoluteUri) { UseShellExecute = true });
            DialogResult = true;
        }
        catch (Exception error)
        {
            ErrorText.Text = "Could not open GitHub: " + error.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnLater(object sender, RoutedEventArgs e) => DialogResult = false;

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
