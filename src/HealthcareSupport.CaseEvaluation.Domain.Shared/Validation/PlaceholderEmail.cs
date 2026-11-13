using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Validation;

/// <summary>
/// 2026-11-13 -- recognises the "I do not know it" email addresses people type into a required field
/// (unknown@gmail.com, na@na.com, xxx@x.com). Such an address looks valid, then receives real case
/// mail, packets included. The same lists live in
/// <c>angular/src/app/shared/placeholder-email.ts</c>; a Domain test fails when they differ.
///
/// <para>Rule: take the part before the last <c>@</c>, lowercase it, cut it at the first <c>+</c>,
/// remove <c>. - _ /</c>, remove trailing digits. It is a placeholder when the result is in
/// <see cref="Words"/>, or is one character repeated three or more times (xxx, aaaa). A domain in
/// <see cref="Domains"/> makes any address at it a placeholder.</para>
/// </summary>
public static class PlaceholderEmail
{
    public static readonly IReadOnlyList<string> Words = new[]
    {
        "unknown", "unkown", "unknow", "unknwn", "unk", "unkn", "notknown", "dontknow", "donotknow", "idk",
        "na", "no", "none", "nil", "null", "nobody", "noone", "nothing", "x",
        "noemail", "nomail", "noaddress", "noemailaddress", "notprovided", "notavailable", "notapplicable", "unavailable",
        "tbd", "tba", "tbc", "pending", "empty", "blank", "missing",
        "test", "testing", "tester", "fake", "dummy", "sample", "example", "placeholder", "temp", "invalid",
        "noreply", "donotreply", "asdf", "qwerty", "abc", "abcd",
    };

    public static readonly IReadOnlyList<string> Domains = new[]
    {
        "noemail.com", "nomail.com", "none.com", "unknown.com", "fake.com",
        "example.com", "example.net", "example.org",
    };

    private static readonly HashSet<string> WordSet = new(Words, StringComparer.Ordinal);
    private static readonly HashSet<string> DomainSet = new(Domains, StringComparer.Ordinal);

    /// <summary>True when <paramref name="email"/> is a placeholder. Blank is NOT a placeholder (it is "no value").</summary>
    public static bool IsPlaceholder(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var at = trimmed.LastIndexOf('@');
        var local = at >= 0 ? trimmed[..at] : trimmed;
        var domain = at >= 0 ? trimmed[(at + 1)..].ToLowerInvariant() : string.Empty;
        if (DomainSet.Contains(domain))
        {
            return true;
        }

        var key = Normalize(local);
        if (key.Length == 0)
        {
            return false;
        }

        return WordSet.Contains(key) || (key.Length >= 3 && key.All(c => c == key[0]));
    }

    private static string Normalize(string local)
    {
        var lowered = local.ToLowerInvariant();
        var plus = lowered.IndexOf('+');
        if (plus >= 0)
        {
            lowered = lowered[..plus];
        }

        var kept = new string(lowered.Where(c => c is not ('.' or '-' or '_' or '/')).ToArray());
        return kept.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
    }
}
