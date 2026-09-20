using CommunityToolkit.Mvvm.Input;

namespace Qx.Presentation.ViewModels.About;

public sealed class AboutLink
{
    public AboutLink(string text, Uri url, string tooltip, Func<AboutLink, CancellationToken, Task> open)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooltip);
        ArgumentNullException.ThrowIfNull(open);
        Text = text;
        Url = url ?? throw new ArgumentNullException(nameof(url));
        Tooltip = tooltip;
        OpenCommand = new AsyncRelayCommand(cancellation_token => open(this, cancellation_token));
    }

    public string Text { get; }

    public Uri Url { get; }

    public string Tooltip { get; }

    public IAsyncRelayCommand OpenCommand { get; }
}
