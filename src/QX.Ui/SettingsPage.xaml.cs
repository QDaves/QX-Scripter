using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Qx.Game.Rules;
using Qx.Mcp;

namespace Qx.Ui;

public partial class SettingsPage : GamePage
{
    private string _scriptsDir = "";
    private Func<int> _gearthPort = () => 9092;
    private McpServer? _mcp;
    private Action? _themeChanged;
    private bool _ready;

    public SettingsPage()
    {
        InitializeComponent();
    }

    /// <summary>Hands over what the page needs from the window that owns it.</summary>
    public void Bind(string scriptsDir, McpServer? mcp, Func<int> gearthPort, Action themeChanged)
    {
        _scriptsDir = scriptsDir;
        _mcp = mcp;
        _themeChanged = themeChanged;
        _gearthPort = gearthPort;
        Refresh();
    }

    /// <summary>The port the extension speaks to G-Earth on, shown under connection.</summary>
    public int GEarthPort => _gearthPort();

    public override void Refresh()
    {
        _ready = false;

        FolderText.Text = _scriptsDir;
        FolderText.ToolTip = _scriptsDir;

        string? clientUrl = McpClientUrl();
        string address = _mcp is { IsRunning: true } running
            ? $"http://127.0.0.1:{running.Port}/mcp"
            : "not running";
        McpText.Text = Masked(clientUrl) ?? address;
        McpText.ToolTip = McpText.Text;
        McpHint.Text = clientUrl is null
            ? "Start QX with the port free to expose the MCP server."
            : "Copy puts the full URL, token and all, on the clipboard.";
        CopyMcpButton.IsEnabled = clientUrl is not null;

        RestoreSessionOption.IsChecked = App.Settings.RestoreSession;
        DarkOption.IsChecked = App.Theme.IsDark;
        LightOption.IsChecked = !App.Theme.IsDark;

        ShowCapabilities();
        ShowConnection();

        _ready = true;
    }

    /// <summary>
    /// What the session is talking to.
    /// </summary>
    /// <remarks>
    /// Here rather than on the general page, where it sat among the switches. Which client is
    /// connected and on which port is a fact about the setup, not something you act on, and it made
    /// a page of controls read as a page of readouts.
    /// </remarks>
    private void ShowConnection() =>
        ConnectionFields.ItemsSource = new List<RoomPage.Stat>
        {
            new("G-EARTH PORT", GEarthPort.ToString()),
            new("CLIENT", Game?.Room is null ? "—" : ClientName())
        };

    private string ClientName() => Rules?.ClientName ?? "not connected";

    /// <summary>The rules object, only for the client name it already knows.</summary>
    public SessionRules? Rules { get; set; }

    /// <summary>Shows the URL without putting the access token on screen for a screenshot to catch.</summary>
    private static string? Masked(string? clientUrl)
    {
        if (clientUrl is null)
            return null;

        int token = clientUrl.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
        return token < 0 ? clientUrl : clientUrl[..(token + 6)] + "••••••••";
    }

    private void ShowCapabilities()
    {
        bool live = _mcp is not null;
        foreach (CheckBox box in new[] { AllowExecuteOption, AllowFileWriteOption, AllowEditorOption })
            box.IsEnabled = live;

        if (_mcp?.Config is not { } config)
        {
            CapabilityHint.Text = "The MCP server is not running, so there is nothing to permit.";
            return;
        }

        AllowExecuteOption.IsChecked = config.AllowExecute;
        AllowFileWriteOption.IsChecked = config.AllowFileWrite;
        AllowEditorOption.IsChecked = config.AllowEditor;
        CapabilityHint.Text = "Applies straight away, no restart.";
    }

    private void OnRestoreSessionChanged(object sender, RoutedEventArgs e)
    {
        if (_ready)
            App.Settings.RestoreSession = RestoreSessionOption.IsChecked == true;
    }

    /// <summary>
    /// Rewrites the server's configuration and the file behind it.
    /// </summary>
    /// <remarks>
    /// The token is carried across untouched: this changes what a client may do, not who it is, and
    /// minting a new one here would drop every client mid-session for a checkbox.
    /// </remarks>
    private void OnCapabilityChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _mcp is null)
            return;

        McpConfig updated = _mcp.Config with
        {
            AllowExecute = AllowExecuteOption.IsChecked == true,
            AllowFileWrite = AllowFileWriteOption.IsChecked == true,
            AllowEditor = AllowEditorOption.IsChecked == true
        };

        _mcp.Config = updated;

        try
        {
            updated.Save();
            CapabilityHint.Text = "Applies straight away, no restart.";
        }
        catch (Exception error)
        {
            // The running server already has the change; only the record of it failed, so the
            // message says exactly that rather than implying nothing happened.
            CapabilityHint.Text = $"Applied, but could not be saved for next time: {error.Message}";
        }
    }

    private void CopyMcpUrl(object sender, RoutedEventArgs e)
    {
        string? clientUrl = McpClientUrl();
        if (clientUrl is null)
            return;

        try
        {
            Clipboard.SetText(clientUrl);
            CopyMcpButton.Content = new TextBlock { Text = "Copied" };
        }
        catch (Exception error)
        {
            McpHint.Text = "Could not copy: " + error.Message;
        }
    }

    private string? McpClientUrl() =>
        _mcp is { IsRunning: true } running ? running.ClientUrl : null;

    private void OnDark(object sender, RoutedEventArgs e) => SetTheme(true);

    private void OnLight(object sender, RoutedEventArgs e) => SetTheme(false);

    private void SetTheme(bool dark)
    {
        if (!_ready || App.Theme.IsDark == dark)
            return;

        App.Theme.Apply(dark);
        _themeChanged?.Invoke();
    }

    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_scriptsDir);
            Process.Start(new ProcessStartInfo(_scriptsDir) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            FolderText.Text = $"Could not open the folder: {error.Message}";
        }
    }
}
