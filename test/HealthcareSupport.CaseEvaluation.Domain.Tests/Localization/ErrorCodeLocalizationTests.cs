using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Localization;

/// <summary>
/// Every business error code must resolve to a message a user can act on.
///
/// <para>Found live 2026-08-19: 95 of our 96 error codes resolved to NOTHING, so every business
/// rule rejection in the application reached the user as "An internal error occurred during your
/// request!". The messages existed -- they were keyed with a colon (<c>Appointment:BookingSlotFull</c>)
/// while ABP looks them up with the separator the CODE uses. A code of
/// <c>CaseEvaluation:Appointment.BookingSlotFull</c> means resource <c>CaseEvaluation</c>, key
/// <c>Appointment.BookingSlotFull</c> -- a DOT. Only <c>Appointment.AccessDenied</c> happened to be
/// written that way, which is why exactly one rule in the product explained itself.</para>
///
/// <para>The failure mode is silent and total: nothing throws, nothing logs a warning, the build is
/// green, and the only symptom is that users are told a business rule is an internal server error.
/// A single missing key is invisible in review, which is why this is a test and not a convention.</para>
/// </summary>
public class ErrorCodeLocalizationTests
{
    [Fact]
    public void Every_error_code_has_a_localised_message()
    {
        var texts = LoadEnglishTexts();
        var missing = AllErrorCodes()
            .Where(code => !texts.ContainsKey(KeyFor(code)))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        missing.ShouldBeEmpty(
            "These error codes reach the user as a generic internal error because en.json has no " +
            "matching key. Add one keyed EXACTLY as the code reads after 'CaseEvaluation:' -- note " +
            "the separator is a dot, not a colon: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The separator regression specifically, so a future edit that reintroduces a colon-keyed entry
    /// fails on the reason rather than on a bare count.
    /// </summary>
    [Fact]
    public void No_error_code_is_keyed_with_a_colon_instead_of_a_dot()
    {
        var texts = LoadEnglishTexts();
        var colonKeyed = AllErrorCodes()
            .Select(KeyFor)
            .Where(key => key.Contains('.', StringComparison.Ordinal))
            .Select(key => new { Key = key, Wrong = ReplaceFirstDotWithColon(key) })
            .Where(pair => texts.ContainsKey(pair.Wrong))
            .Select(pair => pair.Wrong + " should be " + pair.Key)
            .ToList();

        colonKeyed.ShouldBeEmpty(
            "en.json holds these under a colon, which ABP will never look up: " +
            string.Join(", ", colonKeyed));
    }

    private static string KeyFor(string errorCode)
    {
        // Codes read "<resource>:<key>"; the resource is always CaseEvaluation here, and the key is
        // everything after the FIRST colon -- it may itself contain dots.
        var separator = errorCode.IndexOf(':');
        return separator < 0 ? errorCode : errorCode[(separator + 1)..];
    }

    private static string ReplaceFirstDotWithColon(string key)
    {
        var dot = key.IndexOf('.');
        return dot < 0 ? key : key[..dot] + ":" + key[(dot + 1)..];
    }

    private static IEnumerable<string> AllErrorCodes()
    {
        return typeof(CaseEvaluationDomainErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string?)f.GetRawConstantValue())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the SHIPPED en.json out of the assembly rather than off disk, so the test asserts what
    /// the application will actually serve and does not depend on the working directory.
    /// </summary>
    private static Dictionary<string, string> LoadEnglishTexts()
    {
        var assembly = typeof(CaseEvaluationDomainErrorCodes).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith("Localization.CaseEvaluation.en.json", StringComparison.Ordinal));

        resourceName.ShouldNotBeNull("The CaseEvaluation en.json is not embedded in Domain.Shared.");

        using var stream = assembly.GetManifestResourceStream(resourceName!)!;
        using var reader = new StreamReader(stream);
        using var document = JsonDocument.Parse(reader.ReadToEnd());

        // en.json currently holds 45 duplicate keys (menu and column labels, none of them error
        // codes). A parser takes the last occurrence, so this does the same rather than throwing --
        // deduplicating the file is a separate cleanup and not what these tests are guarding.
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.GetProperty("Texts").EnumerateObject())
        {
            texts[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return texts;
    }
}
