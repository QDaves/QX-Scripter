using Qx.Diagnostics;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Threading;
using Qx.Updates;

namespace Qx.Presentation.Services.Updates;

public sealed class UpdateNoticeCoordinator : IDisposable
{
    public const string RepositoryPrefix = "QDaves/QX-Scripter/";
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    readonly Func<CancellationToken, Task<GitHubRelease?>> _fetch;
    readonly ISettingsStore _settings;
    readonly IShellWindow _window;
    readonly IUpdateNoticePresenter _presenter;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly CancellationToken _lifetime;
    readonly string _installed_version;
    GitHubRelease? _pending;
    bool _started;
    bool _shown;
    bool _showing;

    public UpdateNoticeCoordinator(
        Func<CancellationToken, Task<GitHubRelease?>> fetch,
        ISettingsStore settings,
        IShellWindow window,
        IUpdateNoticePresenter presenter,
        IUiDispatcher dispatcher,
        TimeProvider time,
        string installed_version,
        CancellationToken lifetime)
    {
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _installed_version = installed_version ?? throw new ArgumentNullException(nameof(installed_version));
        _lifetime = lifetime;
        _window.Activated += OnActivated;
    }

    public GitHubRelease? Pending => _pending;

    public void Dispose() => _window.Activated -= OnActivated;

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        CheckAsync().Observe("updates");
    }

    async Task CheckAsync()
    {
        using var timeout = new CancellationTokenSource(CheckTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime, timeout.Token);
        GitHubRelease? release = await Task.Run(() => _fetch(linked.Token), linked.Token).ConfigureAwait(false);
        if (release is null)
            return;
        await _dispatcher.InvokeAsync(() => Offer(release), _lifetime).ConfigureAwait(false);
    }

    void Offer(GitHubRelease release)
    {
        string? stored = _settings.Current.LastNotifiedRelease;
        string? last_tag = stored is not null && stored.StartsWith(RepositoryPrefix, StringComparison.Ordinal)
            ? stored[RepositoryPrefix.Length..]
            : null;
        if (!GitHubReleaseUpdates.ShouldNotify(_installed_version, last_tag, release))
            return;
        _pending = release;
        TryShowAsync().Observe("updates");
    }

    void OnActivated() => TryShowAsync().Observe("updates");

    async Task TryShowAsync()
    {
        if (_shown || _showing || _pending is not { } release || _lifetime.IsCancellationRequested)
            return;
        if (!_window.IsVisible || _window.IsMinimized)
            return;
        _showing = true;
        try
        {
            await _presenter.ShowAsync(_installed_version, release, _lifetime);
            _shown = true;
            _pending = null;
            _settings.Update(document => document with { LastNotifiedRelease = RepositoryPrefix + release.Tag });
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Diag.Warn($"Update notice could not be shown: {error.Message}", "updates");
        }
        finally
        {
            _showing = false;
        }
    }
}
