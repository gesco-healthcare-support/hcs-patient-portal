using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Validation;

/// <summary>
/// A phone or fax number must be exactly ten digits. Added 2026-08-27.
///
/// <para>WHY THIS EXISTS. Every phone property carried only <c>[StringLength]</c>, so the API
/// accepted any text up to the column width -- seven digits, a number with an extension, a note,
/// an empty pair of brackets. The Angular field now masks input to ten digits, but a client-side
/// mask is a convenience, not a control: anything that posts to the API directly bypasses it. This
/// is the check that actually holds.</para>
///
/// <para>LENIENT ON SHAPE, STRICT ON CONTENT. Punctuation is stripped before counting, so
/// <c>2135550134</c>, <c>(213)-555-0134</c> and <c>213 555 0134</c> all pass while nine digits or
/// eleven do not. Being lenient here is deliberate: a browser holding a cached copy of the previous
/// SPA bundle still posts formatted values, and this box has served a stale app shell before, so a
/// strict digits-only rule would reject real submissions during a deploy rather than at the point
/// the data is wrong.</para>
///
/// <para>WHAT THIS DOES NOT DO. It does not normalize -- a validation attribute cannot rewrite the
/// value it inspects, and the stored shape is the client's job (the field's value accessor keeps
/// ten bare digits in the model). So a formatted value from a stale bundle can still be STORED
/// with punctuation. That is a transient, self-correcting anomaly: the field strips punctuation
/// again the next time the record is opened. Normalizing server-side would mean touching every
/// write path, which is a larger change than the problem.</para>
///
/// <para>An absent value is valid. These fields are optional across the app, and making a phone
/// number mandatory is a product decision, not a formatting one -- use <c>[Required]</c> alongside
/// this attribute where a number genuinely is required.</para>
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = false)]
public sealed class PhoneNumberAttribute : ValidationAttribute
{
    /// <summary>Digits in a US phone number, area code included.</summary>
    public const int DigitCount = 10;

    public PhoneNumberAttribute()
        : base("Enter a 10-digit phone number.")
    {
    }

    /// <summary>The digits in <paramref name="value"/>, ignoring any formatting.</summary>
    public static string Digits(string? value)
    {
        return value == null
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());
    }

    /// <summary>True when the value is absent or holds exactly <see cref="DigitCount"/> digits.</summary>
    public static bool IsValidNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Digits(value).Length == DigitCount;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string text || IsValidNumber(text))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            ErrorMessage,
            validationContext?.MemberName == null
                ? null
                : new[] { validationContext.MemberName });
    }
}
