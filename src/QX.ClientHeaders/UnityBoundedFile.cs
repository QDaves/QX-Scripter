using System.Text;

namespace Qx.Unity;

internal static class UnityBoundedFile
{
    public const long MaximumDumpBytes = 128L * 1024 * 1024;
    public const long MaximumAssemblyBytes = 512L * 1024 * 1024;
    const int MaximumDumpLines = 2_000_000;
    const int MaximumLineCharacters = 1_048_576;

    public static string ReadAllText(string path, long maximum_bytes)
    {
        using FileStream stream = Open(path, maximum_bytes);
        long expected_length = stream.Length;
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            true,
            131072,
            false);
        string value;
        try
        {
            value = reader.ReadToEnd();
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"Unity analysis input is not valid UTF-8: {path}", error);
        }
        if (stream.Length != expected_length)
            throw new InvalidDataException($"Unity analysis input changed while reading: {path}");
        return value;
    }

    public static string[] ReadAllLines(string path, long maximum_bytes)
    {
        using FileStream stream = Open(path, maximum_bytes);
        long expected_length = stream.Length;
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            true,
            131072,
            false);
        var lines = new List<string>();
        try
        {
            while (reader.ReadLine() is string line)
            {
                if (line.Length > MaximumLineCharacters)
                    throw new InvalidDataException($"Unity analysis input contains an oversized line: {path}");
                lines.Add(line);
                if (lines.Count > MaximumDumpLines)
                    throw new InvalidDataException($"Unity analysis input contains too many lines: {path}");
            }
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"Unity analysis input is not valid UTF-8: {path}", error);
        }
        if (stream.Length != expected_length)
            throw new InvalidDataException($"Unity analysis input changed while reading: {path}");
        return lines.ToArray();
    }

    public static byte[] ReadAllBytes(string path, long maximum_bytes)
    {
        using FileStream stream = Open(path, maximum_bytes);
        long expected_length = stream.Length;
        if (expected_length > int.MaxValue)
            throw new InvalidDataException($"Unity analysis input cannot be materialized: {path}");
        byte[] value = GC.AllocateUninitializedArray<byte>(checked((int)expected_length));
        stream.ReadExactly(value);
        if (stream.Length != expected_length)
            throw new InvalidDataException($"Unity analysis input changed while reading: {path}");
        return value;
    }

    public static async Task<byte[]> ReadAllBytesAsync(
        string path,
        long maximum_bytes,
        CancellationToken cancellation_token = default)
    {
        await using FileStream stream = Open(path, maximum_bytes, true);
        long expected_length = stream.Length;
        if (expected_length > int.MaxValue)
            throw new InvalidDataException($"Unity analysis input cannot be materialized: {path}");
        byte[] value = GC.AllocateUninitializedArray<byte>(checked((int)expected_length));
        await stream.ReadExactlyAsync(value, cancellation_token).ConfigureAwait(false);
        if (stream.Length != expected_length)
            throw new InvalidDataException($"Unity analysis input changed while reading: {path}");
        return value;
    }

    static FileStream Open(string path, long maximum_bytes, bool asynchronous = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            131072,
            FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));
        if (stream.Length is <= 0 || stream.Length > maximum_bytes)
        {
            long length = stream.Length;
            stream.Dispose();
            throw new InvalidDataException(
                $"Unity analysis input has invalid length {length}, maximum {maximum_bytes}: {path}");
        }
        return stream;
    }
}
