using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Flazzy;
using Flazzy.ABC;
using Flazzy.Tags;
using SharpCompress.Compressors.LZMA;

namespace Qx.Headers.Flash;

public enum SwfCompression
{
    None = 0x46,
    ZLib = 0x43,
    Lzma = 0x5A
}

public sealed class SwfInfo : IDisposable
{
    IReadOnlyList<ABCFile>? _abcFiles;
    Avm2DeclaringScopeIndex? declaring_scopes;
    byte[]? source_container_snapshot;
    byte[]? authenticated_harman_original_snapshot;
    string? source_container_sha256;
    readonly object analysis_sync = new();
    bool authenticated_harman_transform;
    bool disposed;

    public required SwfCompression Compression { get; init; }
    public required bool Encrypted { get; init; }
    public required bool ScrambledSignature { get; init; }
    public bool AuthenticatedHarmanTransform
    {
        get
        {
            lock (analysis_sync)
            {
                EnsureActive();
                return authenticated_harman_transform;
            }
        }
    }
    public required byte Version { get; init; }
    public required uint FileLength { get; init; }
    public required ShockwaveFlash Flash { get; init; }
    public ReadOnlyMemory<byte> UncompressedContainer { get; init; }

    public int SourceContainerLength
    {
        get
        {
            lock (analysis_sync)
            {
                EnsureActive();
                return SourceContainer().Length;
            }
        }
    }

    public string SourceContainerSha256
    {
        get
        {
            lock (analysis_sync)
            {
                EnsureActive();
                return source_container_sha256 ??
                    throw new InvalidOperationException(
                        "The SWF source container is unavailable because this instance was not created by SwfLoader.");
            }
        }
    }

    internal ReadOnlyMemory<byte> SourceContainerSnapshot
    {
        get
        {
            lock (analysis_sync)
            {
                EnsureActive();
                return new ReadOnlyMemory<byte>(
                    SourceContainer());
            }
        }
    }

    internal ReadOnlyMemory<byte>? AuthenticatedHarmanOriginalSnapshot
    {
        get
        {
            lock (analysis_sync)
            {
                EnsureActive();
                return authenticated_harman_original_snapshot is null
                    ? null
                    : new ReadOnlyMemory<byte>(
                        authenticated_harman_original_snapshot);
            }
        }
    }

    public IEnumerable<DoABCTag> AbcTags
    {
        get
        {
            lock (analysis_sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(SwfInfo));
                return Flash.Tags.OfType<DoABCTag>().ToArray();
            }
        }
    }

    public IReadOnlyList<ABCFile> AbcFiles
    {
        get
        {
            lock (analysis_sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(SwfInfo));
                return MaterializeAbcFiles();
            }
        }
    }

    internal Avm2DeclaringScopeIndex DeclaringScopes
    {
        get
        {
            lock (analysis_sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(SwfInfo));
                IReadOnlyList<ABCFile> abc_files =
                    MaterializeAbcFiles();
                return declaring_scopes ??=
                    Avm2DeclaringScopeIndex.Create(abc_files);
            }
        }
    }

    IReadOnlyList<ABCFile> MaterializeAbcFiles()
    {
        if (_abcFiles is not null)
            return _abcFiles;
        var abc_files = new List<ABCFile>();
        try
        {
            foreach (DoABCTag tag in Flash.Tags.OfType<DoABCTag>())
                abc_files.Add(new ABCFile(tag.ABCData));
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            foreach (ABCFile abc in abc_files)
                TryDispose(abc, ref errors);
            if (errors is { Count: > 1 } cleanup_errors)
                throw new AggregateException(cleanup_errors);
            throw;
        }
        _abcFiles = abc_files;
        return abc_files;
    }

    internal void SetContainerSnapshots(
        byte[] source_container,
        byte[]? authenticated_harman_original)
    {
        lock (analysis_sync)
        {
            EnsureActive();
            if (source_container_snapshot is not null)
                throw new InvalidOperationException("SWF container snapshot is already set.");
            source_container_snapshot = source_container;
            authenticated_harman_original_snapshot =
                authenticated_harman_original;
            authenticated_harman_transform =
                Encrypted ||
                authenticated_harman_original is not null;
            source_container_sha256 = Convert
                .ToHexString(SHA256.HashData(source_container))
                .ToLowerInvariant();
        }
    }

    public void Dispose()
    {
        IReadOnlyList<ABCFile>? abc_files;
        lock (analysis_sync)
        {
            if (disposed)
                return;
            disposed = true;
            abc_files = _abcFiles;
            _abcFiles = null;
            declaring_scopes = null;
            source_container_snapshot = null;
            authenticated_harman_original_snapshot = null;
            authenticated_harman_transform = false;
            source_container_sha256 = null;
        }

        List<Exception>? errors = null;
        if (abc_files is not null)
        {
            foreach (ABCFile abc in abc_files)
                TryDispose(abc, ref errors);
        }
        TryDispose(Flash, ref errors);
        if (errors is { Count: 1 })
            throw errors[0];
        if (errors is { Count: > 1 })
            throw new AggregateException(errors);
    }

    static void TryDispose(
        IDisposable value,
        ref List<Exception>? errors)
    {
        try
        {
            value.Dispose();
        }
        catch (Exception exception)
        {
            errors ??= [];
            errors.Add(exception);
        }
    }

    void EnsureActive()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(SwfInfo));
    }

    byte[] SourceContainer() =>
        source_container_snapshot ??
        throw new InvalidOperationException(
            "The SWF source container is unavailable because this instance was not created by SwfLoader.");
}

public static class SwfLoader
{
    const int MaximumContainerBytes = 536_870_912;

    public static SwfInfo Load(string path)
    {
        byte[] raw = ReadContainer(path);
        return LoadOwned(raw, null);
    }

    public static SwfInfo Load(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 8)
            throw new InvalidDataException("File is too small to be a SWF.");
        if (raw.Length > MaximumContainerBytes)
            throw new InvalidDataException($"SWF container exceeds {MaximumContainerBytes} bytes.");
        return LoadOwned(raw.ToArray(), null);
    }

    public static SwfInfo LoadDecryptedHarman(
        string decrypted_path,
        string original_path)
    {
        byte[] decrypted = ReadContainer(decrypted_path);
        byte[] original = ReadContainer(original_path);
        return LoadDecryptedHarmanOwned(decrypted, original);
    }

    public static SwfInfo LoadDecryptedHarman(
        ReadOnlySpan<byte> decrypted,
        ReadOnlySpan<byte> original)
    {
        if (decrypted.Length > MaximumContainerBytes ||
            original.Length > MaximumContainerBytes)
        {
            throw new InvalidDataException(
                $"SWF container exceeds {MaximumContainerBytes} bytes.");
        }
        return LoadDecryptedHarmanOwned(
            decrypted.ToArray(),
            original.ToArray());
    }

    static SwfInfo LoadOwned(
        byte[] source_container,
        byte[]? authenticated_harman_original)
    {
        ReadOnlySpan<byte> raw = source_container;
        if (raw.Length < 8)
            throw new InvalidDataException("File is too small to be a SWF.");
        if (raw.Length > MaximumContainerBytes)
            throw new InvalidDataException($"SWF container exceeds {MaximumContainerBytes} bytes.");

        bool encrypted = HarmanDecryptor.IsEncrypted(raw);
        byte[]? decrypted = encrypted ? HarmanDecryptor.Decrypt(raw) : null;
        if (decrypted != null) raw = decrypted;

        byte marker = raw[0];
        byte normalized = (byte)(marker & ~0x20);
        bool scrambled = encrypted || marker != normalized;

        if (raw[1] != (byte)'W' || raw[2] != (byte)'S')
            throw new InvalidDataException($"Not a SWF container (signature '{(char)raw[0]}{(char)raw[1]}{(char)raw[2]}').");

        if (!Enum.IsDefined((SwfCompression)normalized))
            throw new InvalidDataException($"Unknown SWF compression marker 0x{marker:X2}.");

        var compression = (SwfCompression)normalized;
        byte version = raw[3];
        uint fileLength = BinaryReader(raw, 4);
        if (fileLength < 8 || fileLength > MaximumContainerBytes)
            throw new InvalidDataException($"Invalid declared SWF length: {fileLength} bytes.");
        if (compression == SwfCompression.None && raw.Length != fileLength)
        {
            throw new InvalidDataException(
                $"Uncompressed SWF contains {raw.Length} bytes instead of its declared {fileLength} bytes.");
        }
        if (compression == SwfCompression.ZLib && version < 6)
            throw new InvalidDataException($"Zlib compression requires SWF version 6 or later, found {version}.");
        if (compression == SwfCompression.Lzma && version < 13)
            throw new InvalidDataException($"LZMA compression requires SWF version 13 or later, found {version}.");

        byte[] body = compression switch
        {
            SwfCompression.None => raw[8..].ToArray(),
            SwfCompression.ZLib => InflateZlib(raw[8..], fileLength, scrambled),
            SwfCompression.Lzma => DecodeLzma(raw, fileLength),
            _ => throw new InvalidDataException("Unreachable.")
        };

        byte[] uncompressed = BuildUncompressedContainer(version, body);
        var flash = new ShockwaveFlash(uncompressed);
        try
        {
            flash.Disassemble();
            var swf = new SwfInfo
            {
                Compression = compression,
                Encrypted = encrypted,
                ScrambledSignature = scrambled,
                Version = version,
                FileLength = fileLength,
                Flash = flash,
                UncompressedContainer = uncompressed
            };
            swf.SetContainerSnapshots(
                source_container,
                authenticated_harman_original);
            return swf;
        }
        catch (Exception error)
        {
            try
            {
                flash.Dispose();
            }
            catch (Exception dispose_error)
            {
                throw new AggregateException(
                    error,
                    dispose_error);
            }
            throw;
        }
    }

    internal static byte[] ReadContainerSnapshot(string path) =>
        ReadContainer(path);

    static byte[] ReadContainer(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            131072,
            FileOptions.SequentialScan);
        long length = stream.Length;
        if (length > MaximumContainerBytes)
        {
            throw new InvalidDataException(
                $"SWF container exceeds {MaximumContainerBytes} bytes.");
        }
        byte[] raw = GC.AllocateUninitializedArray<byte>(
            checked((int)length));
        try
        {
            stream.ReadExactly(raw);
        }
        catch (EndOfStreamException error)
        {
            throw new InvalidDataException(
                "SWF container changed while its immutable snapshot was being captured.",
                error);
        }
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException(
                "SWF container changed while its immutable snapshot was being captured.");
        }
        return raw;
    }

    static SwfInfo LoadDecryptedHarmanOwned(
        byte[] decrypted,
        byte[] original)
    {
        if (!HarmanDecryptor.IsEncrypted(original))
        {
            throw new InvalidDataException(
                "The authenticated HARMAN original is not encrypted.");
        }
        byte[] expected = HarmanDecryptor.Decrypt(original);
        if (!decrypted.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidDataException(
                "The decrypted HARMAN container does not match its authenticated original.");
        }
        return LoadOwned(decrypted, original);
    }

    static uint BinaryReader(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));

    static byte[] InflateZlib(ReadOnlySpan<byte> compressed, uint fileLength, bool scrambled)
    {
        if (compressed.Length >= 1 && (compressed[0] & 0x0F) != 8)
        {
            string reason = scrambled
                ? "signature marker was lowercased and the body is not a valid zlib stream; this is a protected Habbo build whose payload is encrypted/scrambled by the client loader and cannot be recovered by signature normalization alone"
                : "body does not begin with a valid zlib header";
            throw new InvalidDataException($"Unable to decompress SWF: {reason}.");
        }

        int expected = fileLength >= 8 ? (int)(fileLength - 8) : 0;
        using var input = new MemoryStream(compressed.ToArray(), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = expected > 0 ? new MemoryStream(expected) : new MemoryStream();
        CopyWithLimit(zlib, output, checked((int)fileLength - 8));
        return output.ToArray();
    }

    static byte[] DecodeLzma(ReadOnlySpan<byte> raw, uint fileLength)
    {
        if (raw.Length < 17)
            throw new InvalidDataException("LZMA SWF is missing its compressed header.");
        uint compressedLength = BinaryReader(raw, 8);
        long containerLength = 17L + compressedLength;
        if (containerLength != raw.Length)
        {
            throw new InvalidDataException(
                $"LZMA SWF contains {raw.Length - 17} compressed bytes instead of its declared {compressedLength} bytes.");
        }
        int bodyLength = fileLength >= 8 ? (int)(fileLength - 8) : 0;
        byte[] properties = raw.Slice(12, 5).ToArray();
        byte[] payload = raw.Slice(17, checked((int)compressedLength)).ToArray();

        using var input = new MemoryStream(payload, writable: false);
        using var output = bodyLength > 0 ? new MemoryStream(bodyLength) : new MemoryStream();
        var decoder = new Decoder();
        decoder.SetDecoderProperties(properties);
        decoder.Code(input, output, payload.Length, bodyLength, null);
        if (input.Position != payload.Length)
        {
            throw new InvalidDataException(
                $"LZMA decoder consumed {input.Position} of {payload.Length} compressed bytes.");
        }
        if (output.Length != bodyLength)
            throw new InvalidDataException($"LZMA SWF expanded to {output.Length} bytes instead of {bodyLength} bytes.");
        return output.ToArray();
    }

    static void CopyWithLimit(Stream source, Stream destination, int expected)
    {
        byte[] buffer = new byte[131072];
        int written = 0;
        while (true)
        {
            int read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            written = checked(written + read);
            if (written > expected)
                throw new InvalidDataException($"SWF expands beyond its declared size of {expected + 8} bytes.");
            destination.Write(buffer, 0, read);
        }
        if (written != expected)
            throw new InvalidDataException($"SWF expanded to {written + 8} bytes instead of its declared {expected + 8} bytes.");
    }

    static byte[] BuildUncompressedContainer(byte version, byte[] body)
    {
        byte[] container = new byte[8 + body.Length];
        container[0] = (byte)'F';
        container[1] = (byte)'W';
        container[2] = (byte)'S';
        container[3] = version;
        BinaryPrimitives.WriteUInt32LittleEndian(container.AsSpan(4, 4), (uint)container.Length);
        body.CopyTo(container, 8);
        return container;
    }
}
