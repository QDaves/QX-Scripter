using System.Diagnostics;
using System.Windows.Navigation;

namespace Qx.Ui;

/// <summary>What the tool is and which version is running.</summary>
public partial class AboutPage : GamePage
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = "Version " + Qx.ProductVersion.Current;
    }

    public override void Refresh()
    {
    }

    private void OpenProfile(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
        }

        e.Handled = true;
    }
}
