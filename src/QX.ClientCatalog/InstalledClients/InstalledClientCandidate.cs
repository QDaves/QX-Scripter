namespace Qx.ClientCatalog.InstalledClients;

public sealed record InstalledClientCandidate(
    string Version,
    string Path,
    string Source,
    DateTimeOffset LastModified,
    IReadOnlyList<string> Files)
{
    public string? ContentRevision { get; init; }
}

public sealed class InstalledClientCandidateChangedEventArgs : EventArgs
{
    public InstalledClientCandidateChangedEventArgs(
        InstalledClientCandidate? previous,
        InstalledClientCandidate? candidate)
    {
        Previous = previous;
        Candidate = candidate;
    }

    public InstalledClientCandidate? Previous { get; }

    public InstalledClientCandidate? Candidate { get; }
}
