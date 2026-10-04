using System.Globalization;
using System.Numerics;

namespace Qx.Model.Wired;

/// <summary>Converts the packed neighborhood and reward controls used by Wired forms.</summary>
public static class WiredFormPacking
{
    private static readonly WiredTileOffset[] spiral = create_spiral();
    private static readonly IReadOnlyDictionary<WiredTileOffset, int> ranks =
        spiral.Select((tile, rank) => (tile, rank)).ToDictionary(value => value.tile, value => value.rank);

    /// <summary>Reads selected tiles from the low-bit-first spiral mask. Missing bits are false.</summary>
    /// <param name="words">The signed mask words, excluding the form's first three parameters.</param>
    /// <returns>Selected coordinates in spiral order.</returns>
    public static IReadOnlyList<WiredTileOffset> ReadNeighborhood(IReadOnlyList<int> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var selected = new List<WiredTileOffset>();
        for (int rank = 0; rank < spiral.Length; rank++)
        {
            if (rank / 32 < words.Count && (words[rank / 32] & (1 << (rank % 32))) != 0)
                selected.Add(spiral[rank]);
        }
        return selected.AsReadOnly();
    }

    /// <summary>Writes all 441 tiles as fourteen words with zero padding in the last word.</summary>
    /// <param name="tiles">The selected coordinates, each from -10 to 10 on both axes.</param>
    /// <returns>The complete signed mask words.</returns>
    public static IReadOnlyList<int> WriteNeighborhood(IReadOnlyList<WiredTileOffset> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        int[] words = new int[14];
        foreach (WiredTileOffset tile in tiles)
        {
            if (!ranks.TryGetValue(tile, out int rank))
                throw new ArgumentOutOfRangeException(nameof(tiles), "Neighborhood coordinates must be within -10 to 10.");
            words[rank / 32] |= 1 << (rank % 32);
        }
        return Array.AsReadOnly(words);
    }

    /// <summary>Reads the reward rows as displayed by the client.</summary>
    /// <param name="text">The packed reward string.</param>
    /// <returns>Typed reward entries; malformed numeric probabilities become zero as in the client.</returns>
    public static IReadOnlyList<WiredRewardEntry> ReadRewards(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return [];
        return Array.AsReadOnly(text.Split(';').Select(row =>
        {
            string[] fields = row.Split(',');
            return new WiredRewardEntry(fields[0] == "0" ? 0 : 1,
                fields.Length > 1 ? fields[1] : "",
                fields.Length > 2 ? number_to_int(fields[2]) : 0);
        }).ToArray());
    }

    /// <summary>Validates and packs reward rows using the client's code sanitization and integer probabilities.</summary>
    /// <param name="rewards">At most twenty reward rows.</param>
    /// <param name="unique">Whether unique rewards disable probability validation.</param>
    /// <returns>The packed reward string.</returns>
    public static string WriteRewards(IReadOnlyList<WiredRewardEntry> rewards, bool unique)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rewards.Count, 20);
        var rows = new List<string>();
        long total = 0;
        foreach (WiredRewardEntry reward in rewards)
        {
            ArgumentNullException.ThrowIfNull(reward);
            ArgumentNullException.ThrowIfNull(reward.Code);
            if (reward.Type is not 0 and not 1)
                throw new ArgumentOutOfRangeException(nameof(rewards), "Reward type must be zero or one.");
            if (reward.Code.Length > 100)
                throw new ArgumentException("Reward codes are limited to 100 characters.", nameof(rewards));
            string code = reward.Code.Replace(",", "", StringComparison.Ordinal).Replace(";", "", StringComparison.Ordinal);
            if (code.Length == 0)
                continue;
            if (!unique && reward.Probability is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(rewards), "Reward probabilities must be within 1 to 100.");
            total += reward.Probability;
            rows.Add(string.Create(CultureInfo.InvariantCulture, $"{reward.Type},{code},{reward.Probability}"));
        }
        if (!unique && total > 100)
            throw new ArgumentException("Reward probabilities cannot total more than 100.", nameof(rewards));
        return string.Join(';', rows);
    }

    private static int number_to_int(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            BigInteger.TryParse("0" + text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out BigInteger hexadecimal))
            return unchecked((int)(uint)(hexadecimal & uint.MaxValue));
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            return 0;
        double truncated = Math.Truncate(value) % 4294967296d;
        if (truncated < 0)
            truncated += 4294967296d;
        return unchecked((int)(uint)truncated);
    }

    private static WiredTileOffset[] create_spiral()
    {
        var result = new WiredTileOffset[441];
        int x = 0;
        int y = 0;
        int rank = 0;
        int direction = 0;
        for (int length = 1; rank < result.Length; length++)
        {
            for (int repeat = 0; repeat < 2 && rank < result.Length; repeat++)
            {
                for (int step = 0; step < length && rank < result.Length; step++)
                {
                    result[rank++] = new(x, y);
                    switch (direction)
                    {
                        case 0: x++; break;
                        case 1: y--; break;
                        case 2: x--; break;
                        case 3: y++; break;
                    }
                }
                direction = (direction + 1) % 4;
            }
        }
        return result;
    }
}
