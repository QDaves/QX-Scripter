using System.Collections.ObjectModel;

namespace Qx.Presentation.Services.Logging;

public interface IApplicationLog
{
    ReadOnlyObservableCollection<LogEntry> Entries { get; }

    event Action<IReadOnlyList<LogEntry>>? Appended;

    event Action<int>? Trimmed;

    event Action? Cleared;

    void Clear();

    Task FlushAsync(CancellationToken cancellation_token);
}
