using System;
using System.Collections.Generic;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- pure <c>##Var##</c> placeholder substitution.
/// Mirrors OLD <c>ApplicationUtility.GetEmailTemplateFromHTML</c>
/// (<c>P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs</c>:212-251)
/// flat-replace loop verbatim, with one OLD-bug-fix exception noted below.
///
/// <para>Why we keep OLD's syntax instead of switching to Razor (the
/// audit's original Things-NOT-to-port row): Razor on DB-stored
/// templates needs Roslyn dynamic compilation per render, and Phase 4's
/// seed contributor copies OLD's HTML bodies verbatim (with their
/// <c>##Var##</c> placeholders intact) -- switching syntaxes forces a
/// one-time rewrite of every seeded body. Strict parity wins.</para>
///
/// <para>OLD-bug-fix: OLD line 247 calls <c>item.Value.ToString()</c>
/// unconditionally. The dictionary is built at lines 222-234 with a
/// null-guard that adds <c>""</c> for null values, so the bug never
/// fires in the OLD path -- but a future caller passing null directly
/// would NRE. NEW's substitutor also accepts null values and renders
/// them as the empty string explicitly so this stays safe.</para>
///
/// <para>2026-05-06: moved from Application/Notifications (internal) to
/// Domain/Notifications (public) so the AuthServer's IAccountEmailer
/// override can use it without the AuthServer needing a reference to
/// the Application layer. Pure function -- no IO, no DI -- so tests
/// run in microseconds.</para>
/// </summary>
public static class TemplateVariableSubstitutor
{
    /// <summary>
    /// Substitutes <c>##Key##</c> placeholders in <paramref name="body"/>
    /// with the matching value from <paramref name="variables"/>.
    /// Unknown placeholders are left in place (mirrors OLD's
    /// <c>foreach</c> over the explicit dictionary at line 245-248 --
    /// keys not in the dictionary are not replaced). Null values render
    /// as the empty string. Null body returns the empty string.
    /// </summary>
    /// <param name="body">
    /// Template body containing zero or more <c>##Key##</c> placeholders.
    /// May be null (returns empty string) or have no placeholders
    /// (returns unchanged).
    /// </param>
    /// <param name="variables">
    /// Map of variable name (without the <c>##</c> wrapping) to the
    /// substitution value. Keys are wrapped in <c>##...##</c> at render
    /// time. May be null or empty (returns body unchanged).
    /// </param>
    /// <returns>The body with all known placeholders substituted.</returns>
    public static string Substitute(string? body, IReadOnlyDictionary<string, object?>? variables)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }
        if (variables == null || variables.Count == 0)
        {
            return body;
        }
        var result = body;
        foreach (var kvp in variables)
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                continue;
            }
            var placeholder = "##" + kvp.Key + "##";
            var replacement = FormatValue(kvp.Value);
            result = result.Replace(placeholder, replacement, StringComparison.Ordinal);
        }
        return result;
    }

    /// <summary>
    /// Formats a substitution value into a string. Null -> empty string
    /// (OLD-bug-fix). DateTime / DateTimeOffset use invariant
    /// MM/dd/yyyy format to match OLD's
    /// <c>recordValue.ToString("MM/dd/yyyy")</c> pattern at
    /// <c>ApplicationUtility.cs</c> line 909. All other types use
    /// <see cref="IFormattable.ToString(string?, IFormatProvider?)"/>
    /// with invariant culture so emails do not change shape across
    /// host locales.
    /// </summary>
    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }
        return value switch
        {
            string s => s,
            DateTime dt => dt.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
