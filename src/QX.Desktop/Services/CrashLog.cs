using Avalonia.Threading;
using Qx.Diagnostics;

namespace Qx.Desktop.Services;

public static class CrashLog
{
    public static readonly TimeSpan CrashFlushBudget = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan NoticeInterval = TimeSpan.FromSeconds(10);

    static readonly Lock _gate = new();
    static string? _path;
    static Func<TimeSpan, bool>? _flush;
    static Action<string>? _notice;
    static DateTime _last_notice = DateTime.MinValue;
    static bool _installed;
    static bool _ui_attached;

    public static void Install(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (_gate)
        {
            _path = path;
            if (_installed)
                return;
            _installed = true;
        }
        AppDomain.CurrentDomain.UnhandledException += OnDomainFailure;
        TaskScheduler.UnobservedTaskException += OnUnobserved;
    }

    public static void UseLogFlush(Func<TimeSpan, bool> flush)
    {
        ArgumentNullException.ThrowIfNull(flush);
        lock (_gate)
            _flush = flush;
    }

    public static void AttachUi()
    {
        lock (_gate)
        {
            if (_ui_attached)
                return;
            _ui_attached = true;
        }
        Dispatcher.UIThread.UnhandledException += OnUiFailure;
    }

    public static void UseNotice(Action<string> notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        lock (_gate)
            _notice = notice;
    }

    public static void Write(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string? path;
        lock (_gate)
            path = _path;
        if (path is null)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{DateTime.Now}\n{error}");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
        }
    }

    static void OnDomainFailure(object? sender, UnhandledExceptionEventArgs args)
    {
        Func<TimeSpan, bool>? flush;
        lock (_gate)
            flush = _flush;
        flush?.Invoke(CrashFlushBudget);
        if (args.ExceptionObject is Exception error)
            Write(error);
    }

    static void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Diag.Error(args.Exception.ToString(), "app");
        args.SetObserved();
    }

    static void OnUiFailure(object? sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Write(args.Exception);
        Diag.Error(args.Exception.ToString(), "app");
        args.Handled = true;
        Action<string>? notice;
        lock (_gate)
        {
            DateTime now = DateTime.UtcNow;
            notice = now - _last_notice >= NoticeInterval ? _notice : null;
            if (notice is not null)
                _last_notice = now;
        }
        notice?.Invoke("Something went wrong. The application log has the details.");
    }
}
