namespace Qx.Presentation.Services.Files;

public static class PathComparison
{
    public static StringComparer Comparer => StoragePaths.FileComparer;

    public static StringComparison Comparison => StoragePaths.FileComparison;

    public static bool Same(string? left, string? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        return string.Equals(Full(left), Full(right), Comparison);
    }

    public static string Full(string path) => Path.GetFullPath(path);
}
