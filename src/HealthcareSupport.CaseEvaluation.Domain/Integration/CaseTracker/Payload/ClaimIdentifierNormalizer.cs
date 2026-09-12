using System.Text;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Produces a comparable form of a claim number or WCAB ADJ number.
///
/// <para>Both are FREE TEXT in the portal: validated only for required and 50 characters, with no
/// pattern, trim or case rule. So the same claim entered at two bookings can arrive as <c>WC-4417</c>
/// and <c>WC4417</c>. The Case Tracker groups a patient's records by claim, so without a normalised
/// form it cannot match those two -- which is exactly the misfiling this data was added to prevent.</para>
///
/// <para>The raw value is ALWAYS published alongside this one, because staff need to see what was
/// actually typed. This form is for equality only and is explicitly not a key.</para>
///
/// <para>ACCEPTED TRADE-OFF: stripping all punctuation can theoretically collapse two genuinely
/// different identifiers that differ only in punctuation. That is tolerable because the receiver groups
/// for HUMAN confirmation and still holds the raw values to tell them apart. Normalising less (trim and
/// case only) would fail the receiver's actual example, which is the case that matters.</para>
/// </summary>
public static class ClaimIdentifierNormalizer
{
    /// <summary>
    /// Uppercased alphanumerics only, or <c>null</c> when nothing comparable remains. Null rather than
    /// an empty string on purpose: two values with no alphanumeric content must not compare equal.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
