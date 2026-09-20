namespace Qx.Presentation.Platform;

public sealed record FilePickResult(string? LocalPath, bool Cancelled, string? Failure);
