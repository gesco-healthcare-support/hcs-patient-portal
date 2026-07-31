using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Pure logic for the IT-Admin notification-template editor (B-B2,
/// 2026-06-16):
///
/// <list type="bullet">
///   <item><see cref="GetVariablesForCode"/> -- the <c>##Var##</c> tokens valid
///         for a given code, derived from the shipped default subject + bodies.
///         Drives the editor's "insert variable" chips so a user only inserts
///         tokens the dispatcher will actually populate for that event.</item>
///   <item><see cref="IsCustomized"/> -- whether a tenant's row differs from the
///         shipped default, driving the "Customized" badge.</item>
///   <item><see cref="BuildSampleVariables"/> -- placeholder values used by the
///         send-test path so the rendered preview reads naturally.</item>
/// </list>
///
/// <para>Tokens are the <c>##Name##</c> placeholders consumed by
/// <c>TemplateVariableSubstitutor</c>; this class uses the same grammar so the
/// catalog never drifts from what substitution recognizes.</para>
/// </summary>
public static class NotificationTemplateVariableCatalog
{
    // Same grammar TemplateVariableSubstitutor replaces: ##Name## where Name is
    // one or more ASCII word characters. Ordinal/invariant so token discovery
    // does not shift across host locales.
    private static readonly Regex TokenPattern =
        new(@"##([A-Za-z0-9_]+)##", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Spacer for Humanize: inserts a space before an uppercase letter that
    // follows a lowercase letter or digit ("ExpiresAt" -> "Expires At").
    private static readonly Regex CamelBoundary =
        new("(?<=[a-z0-9])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Distinct <c>##Var##</c> token names found in <paramref name="text"/>, in
    /// first-seen order. Null / empty input yields an empty list.
    /// </summary>
    public static IReadOnlyList<string> ExtractTokens(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TokenPattern.Matches(text))
        {
            var token = match.Groups[1].Value;
            if (seen.Add(token))
            {
                result.Add(token);
            }
        }
        return result;
    }

    /// <summary>
    /// The valid variable tokens for <paramref name="code"/>: the union of the
    /// tokens in its shipped subject, email body, and SMS body, in first-seen
    /// order (subject, then email, then SMS). Codes still on stub content have
    /// no tokens and return an empty list.
    /// </summary>
    public static IReadOnlyList<string> GetVariablesForCode(string code)
    {
        var defaults = NotificationTemplateSeedDefaults.GetSeedDefaults(code);

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in CollectTokens(defaults))
        {
            if (seen.Add(token))
            {
                result.Add(token);
            }
        }
        return result;
    }

    /// <summary>
    /// True when the supplied row content differs from the shipped default for
    /// <paramref name="code"/> on any editable field (subject, email body, SMS
    /// body). Null fields compare as empty so a freshly-seeded row reads as not
    /// customized.
    /// </summary>
    public static bool IsCustomized(string code, string? subject, string? bodyEmail, string? bodySms)
    {
        var defaults = NotificationTemplateSeedDefaults.GetSeedDefaults(code);
        return !string.Equals(subject ?? string.Empty, defaults.Subject, StringComparison.Ordinal)
            || !string.Equals(bodyEmail ?? string.Empty, defaults.BodyEmail, StringComparison.Ordinal)
            || !string.Equals(bodySms ?? string.Empty, defaults.BodySms, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sample substitution map for <paramref name="code"/>: each valid token
    /// mapped to a synthetic, HIPAA-safe placeholder value. Used by the
    /// send-test path so the preview email renders readable content instead of
    /// raw <c>##Var##</c> markers.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> BuildSampleVariables(string code)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var token in GetVariablesForCode(code))
        {
            dict[token] = SampleValueFor(token);
        }
        return dict;
    }

    /// <summary>
    /// Turns a PascalCase token into spaced words for a UI label
    /// ("EmailSubjectIdentity" -> "Email Subject Identity"). All-caps acronyms
    /// (e.g. "URL") are left intact.
    /// </summary>
    public static string Humanize(string token) =>
        string.IsNullOrEmpty(token) ? string.Empty : CamelBoundary.Replace(token, " $1");

    private static IEnumerable<string> CollectTokens(NotificationTemplateSeedDefault defaults)
    {
        foreach (var token in ExtractTokens(defaults.Subject))
        {
            yield return token;
        }
        foreach (var token in ExtractTokens(defaults.BodyEmail))
        {
            yield return token;
        }
        foreach (var token in ExtractTokens(defaults.BodySms))
        {
            yield return token;
        }
    }

    private static string SampleValueFor(string token) => token switch
    {
        "EmailSubjectIdentity" => "(Jane Doe - Lower back injury, 06/01/2026)",
        "UserQuerySubjectIdentity" => " (APT-100245)",
        "DocumentLabel" => "Medical Records Release",
        "PacketLabel" => "Appointment Packet",
        "AppointmentRequestConfirmationNumber" => "APT-100245",
        "TenantName" => "Falkinstein Orthopedics",
        "RoleName" => "Applicant Attorney",
        "PatientFullName" => "Jane Doe",
        "UserName" => "Jane Doe",
        "LoginUserName" => "jane.doe@example.com",
        "Password" => "TempPa55!sample",
        "URL" => "https://falkinstein.localhost:4250/account/register?token=SAMPLE",
        "PortalUrl" => "https://falkinstein.localhost:4250",
        "ExpiresAt" => "06/30/2026",
        "ApprovedSubjectQualifier" => "approved",
        _ => "[" + Humanize(token) + "]",
    };
}
