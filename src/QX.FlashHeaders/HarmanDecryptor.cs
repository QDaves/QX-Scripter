using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Qx.Headers.Flash;

public static class HarmanDecryptor
{
    const string GlobalKey = "Adobe AIR SDK (c) 2021 HARMAN Internation Industries Incorporated";
    const int MaximumDecryptedBytes = 536_870_912;

    public static bool IsEncrypted(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 8) return false;
        if (raw[1] != (byte)'W' || raw[2] != (byte)'S') return false;
        char marker = (char)raw[0];
        return marker is 'c' or 'f' or 'z';
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 12 + 32)
            throw new InvalidDataException("File is too small to be a HARMAN-encrypted SWF.");

        byte[] header = raw[..8].ToArray();
        header[0] -= 32;

        long key = DeriveKey(header);

        byte[] encryptedLengthBytes = raw.Slice(8, 4).ToArray();
        long encryptedLength = Unpack(encryptedLengthBytes, 0);
        long decoded_length = encryptedLength ^ key;
        if (decoded_length < 8 || decoded_length > MaximumDecryptedBytes)
            throw new InvalidDataException($"Invalid HARMAN payload length: {decoded_length} bytes.");
        int decryptedLength = (int)decoded_length;
        int paddedLength = checked((decryptedLength + 0x1F) & ~0x1F);

        int payloadStart = 12;
        int keyStart = checked(payloadStart + paddedLength);
        if (keyStart != raw.Length - 32)
        {
            if (keyStart > raw.Length - 32)
            {
                throw new InvalidDataException(
                    "HARMAN payload length exceeds the file size; the derived length is wrong.");
            }
            throw new InvalidDataException(
                "HARMAN container contains trailing bytes outside the encrypted wrapper.");
        }

        byte[] iv = BuildIv(header, encryptedLengthBytes, key);
        byte[] aesKey = RecoverAesKey(raw.Slice(keyStart, 32), key);
        byte[] payload = raw.Slice(payloadStart, paddedLength).ToArray();

        byte[] plain = DecryptAes(payload, aesKey, iv);

        byte[] result = new byte[8 + decryptedLength];
        header.CopyTo(result, 0);
        Array.Copy(plain, 0, result, 8, decryptedLength);
        return result;
    }

    static long DeriveKey(byte[] header)
    {
        int checksum = 0;
        foreach (byte b in header) checksum += b & 0xFF;

        int rotation = checksum % GlobalKey.Length;
        string material = GlobalKey[rotation..] + GlobalKey[..rotation] + " EncryptSWF " + checksum;

        long value = 0;
        unchecked
        {
            foreach (char c in material)
                value = value * 31 + c;
        }
        return value & 0xFFFFFFFFL;
    }

    static byte[] BuildIv(byte[] header, byte[] encryptedLengthBytes, long key)
    {
        byte[] iv = new byte[16];
        Array.Copy(header, 0, iv, 0, 8);
        Array.Copy(encryptedLengthBytes, 0, iv, 8, 4);
        iv[12] = (byte)key;
        iv[13] = (byte)(key >> 8);
        iv[14] = (byte)(key >> 16);
        iv[15] = (byte)(key >> 24);
        for (int i = 0; i < 16; i++)
            iv[i] ^= (byte)GlobalKey[i];
        return iv;
    }

    static byte[] RecoverAesKey(ReadOnlySpan<byte> obfuscated, long key)
    {
        byte[] aesKey = new byte[32];
        for (int i = 0; i < 32; i += 4)
        {
            long value = Unpack(obfuscated, i);
            value = (i & 4) == 4 ? value - key : value + key;
            aesKey[i] = (byte)value;
            aesKey[i + 1] = (byte)(value >> 8);
            aesKey[i + 2] = (byte)(value >> 16);
            aesKey[i + 3] = (byte)(value >> 24);
        }
        return aesKey;
    }

    static byte[] DecryptAes(byte[] payload, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;
        using ICryptoTransform transform = aes.CreateDecryptor();
        return transform.TransformFinalBlock(payload, 0, payload.Length);
    }

    static long Unpack(ReadOnlySpan<byte> data, int start) =>
        (uint)(data[start] | (data[start + 1] << 8) | (data[start + 2] << 16) | (data[start + 3] << 24));
}
