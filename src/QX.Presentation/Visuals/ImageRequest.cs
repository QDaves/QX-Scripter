namespace Qx.Presentation.Visuals;

public sealed record ImageRequest(string Url, bool ExactPixels, IconKind Fallback);
