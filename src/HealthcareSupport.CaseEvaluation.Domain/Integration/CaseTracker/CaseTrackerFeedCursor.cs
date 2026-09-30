using System;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed cursor's wire form (#927): the position as exactly 16 upper-case hex digits, the 8 bytes of the
/// row's rowversion. Opaque to the consumer, which only echoes back what it was given.
///
/// <para>Strict on purpose. Anything that is not exactly 16 hex digits is refused rather than repaired: a
/// cursor is the consumer's acknowledgement, and guessing what a damaged one meant could mark rows delivered
/// that never arrived.</para>
/// </summary>
public static class CaseTrackerFeedCursor
{
    private const int Length = 16;

    /// <summary>The wire form of <paramref name="position"/>.</summary>
    public static string Encode(long position)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "A feed position is never negative.");
        }

        return position.ToString("X16", CultureInfo.InvariantCulture);
    }

    /// <summary>True, with the position, only for exactly 16 hex digits encoding a non-negative value.</summary>
    public static bool TryDecode(string? value, out long position)
    {
        position = 0;
        if (value == null || value.Length != Length)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false; // long.TryParse(AllowHexSpecifier) would also accept surrounding whitespace
            }
        }

        if (!ulong.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var raw)
            || raw > long.MaxValue)
        {
            return false;
        }

        position = (long)raw;
        return true;
    }
}
