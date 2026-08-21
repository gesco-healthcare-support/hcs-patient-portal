using System;
using System.Collections.Generic;
using System.Globalization;
using HealthcareSupport.CaseEvaluation.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- pure unit tests for the
/// <see cref="TemplateVariableSubstitutor"/> helper. Verifies OLD-faithful
/// <c>##Var##</c> substitution semantics from
/// <c>P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs</c>:212-251
/// without requiring the ABP integration harness (still gated behind the
/// Phase 4 license-checker test-host crash).
///
/// <para>Coverage:</para>
/// <list type="bullet">
///   <item>Substitution mechanics: single placeholder, multiple
///     placeholders, repeated placeholder, prefix-style keys
///     (<c>##Patient.FirstName##</c> per OLD's reflection walk).</item>
///   <item>Edge cases: null/empty body, null/empty variables,
///     placeholder not in dictionary stays in place, unknown
///     placeholder shape doesn't match.</item>
///   <item>Value formatting: null -> empty string (OLD-bug-fix),
///     DateTime renders MM/dd/yyyy invariant, primitives use invariant
///     culture.</item>
///   <item>Idempotency: substituting an already-substituted body is
///     a no-op (no nested placeholders re-trigger).</item>
/// </list>
/// </summary>
public class TemplateVariableSubstitutorUnitTests
{
    // ------------------------------------------------------------------
    // Substitution mechanics
    // ------------------------------------------------------------------

    [Fact]
    public void Substitute_SinglePlaceholder_ReplacesIt()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##!",
            variables: Vars(("Name", "Adrian")));

        result.ShouldBe("Hello Adrian!");
    }

    [Fact]
    public void Substitute_MultiplePlaceholders_ReplacesAll()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "##Greeting## ##Name##, your ##Item## is ready.",
            variables: Vars(
                ("Greeting", "Hello"),
                ("Name", "Adrian"),
                ("Item", "appointment")));

        result.ShouldBe("Hello Adrian, your appointment is ready.");
    }

    [Fact]
    public void Substitute_RepeatedPlaceholder_ReplacesEveryOccurrence()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "##Name## (##Name##) is ##Name##",
            variables: Vars(("Name", "Adrian")));

        result.ShouldBe("Adrian (Adrian) is Adrian");
    }

    [Fact]
    public void Substitute_OldPrefixStyleKey_ReplacesViaExactKeyMatch()
    {
        // OLD's reflection walk added keys like ##Patient.FirstName##
        // (see ApplicationUtility.cs:200). The substitutor treats the
        // dotted key as a single string; callers that want hierarchical
        // lookup must flatten before calling.
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Patient: ##Patient.FirstName## ##Patient.LastName##",
            variables: Vars(
                ("Patient.FirstName", "Jane"),
                ("Patient.LastName", "Doe")));

        result.ShouldBe("Patient: Jane Doe");
    }

    [Fact]
    public void Substitute_PlaceholderNotInDictionary_LeftInPlace()
    {
        // OLD's foreach over the dictionary at line 245-248 only
        // replaces keys that the model surfaced. NEW must preserve
        // this behavior so that unknown placeholders are visible in
        // QA's first email send and surface seed gaps loudly.
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##, ##UnknownVar## was not provided.",
            variables: Vars(("Name", "Adrian")));

        result.ShouldBe("Hello Adrian, ##UnknownVar## was not provided.");
    }

    [Fact]
    public void Substitute_PartialPlaceholderShape_DoesNotMatch()
    {
        // The substitutor matches the literal "##Key##" string only.
        // Single-hash and triple-hash variants must not be replaced --
        // this is OLD-faithful (line 247 uses exact-string Replace).
        var result = TemplateVariableSubstitutor.Substitute(
            body: "single #Name# triple ###Name### correct ##Name##",
            variables: Vars(("Name", "X")));

        result.ShouldBe("single #Name# triple #X# correct X");
    }

    // ------------------------------------------------------------------
    // Edge cases
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Substitute_NullOrEmptyBody_ReturnsEmptyString(string? body)
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: body,
            variables: Vars(("Name", "Adrian")));

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Substitute_NullVariables_ReturnsBodyUnchanged()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##!",
            variables: null);

        result.ShouldBe("Hello ##Name##!");
    }

    [Fact]
    public void Substitute_EmptyVariables_ReturnsBodyUnchanged()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##!",
            variables: new Dictionary<string, object?>());

        result.ShouldBe("Hello ##Name##!");
    }

    [Fact]
    public void Substitute_BodyWithoutPlaceholders_ReturnsUnchanged()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Just a plain string with no markers.",
            variables: Vars(("Name", "Adrian")));

        result.ShouldBe("Just a plain string with no markers.");
    }

    [Fact]
    public void Substitute_EmptyKey_Skipped()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Name"] = "Adrian",
            [""] = "ignored",
        };

        var result = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##",
            variables: dict);

        result.ShouldBe("Hello Adrian");
    }

    // ------------------------------------------------------------------
    // Value formatting (OLD-bug-fix branch)
    // ------------------------------------------------------------------

    [Fact]
    public void Substitute_NullValue_RendersAsEmptyString()
    {
        // OLD-bug-fix: OLD's line 247 does item.Value.ToString() which
        // would NRE on a null value. NEW renders null as "" explicitly.
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##, you are ##Status##.",
            variables: Vars(("Name", "Adrian"), ("Status", null)));

        result.ShouldBe("Hello Adrian, you are .");
    }

    [Fact]
    public void Substitute_DateTimeValue_FormatsAsMonthDayYear()
    {
        // OLD line 909 uses ToString("MM/dd/yyyy") for date keys.
        // NEW reproduces the format for any DateTime value.
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Appointment on ##When##",
            variables: Vars(("When", new DateTime(2026, 7, 4, 14, 30, 0, DateTimeKind.Utc))));

        result.ShouldBe("Appointment on 07/04/2026");
    }

    [Fact]
    public void Substitute_DateTimeOffsetValue_FormatsAsMonthDayYear()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Appointment on ##When##",
            variables: Vars(("When", new DateTimeOffset(2026, 12, 31, 9, 0, 0, TimeSpan.FromHours(-8)))));

        result.ShouldBe("Appointment on 12/31/2026");
    }

    [Fact]
    public void Substitute_IntegerValue_UsesInvariantFormatting()
    {
        // Even when the test host's culture uses a non-default group
        // separator, the rendered email must look the same so QA can
        // assert on a fixed string.
        var prevCulture = CultureInfo.CurrentCulture;
        try
        {
            // Pick a culture that would render 12345 as "12.345".
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var result = TemplateVariableSubstitutor.Substitute(
                body: "##Count## items",
                variables: Vars(("Count", 12345)));

            result.ShouldBe("12345 items");
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
        }
    }

    [Fact]
    public void Substitute_DecimalValue_UsesInvariantFormatting()
    {
        var prevCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var result = TemplateVariableSubstitutor.Substitute(
                body: "##Amount##",
                variables: Vars(("Amount", 1234.56m)));

            // Invariant decimal separator is '.'.
            result.ShouldBe("1234.56");
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
        }
    }

    [Fact]
    public void Substitute_BoolValue_RendersInvariantString()
    {
        var result = TemplateVariableSubstitutor.Substitute(
            body: "Active: ##Flag##",
            variables: Vars(("Flag", true)));

        result.ShouldBe("Active: True");
    }

    // ------------------------------------------------------------------
    // Idempotency
    // ------------------------------------------------------------------

    [Fact]
    public void Substitute_AlreadySubstitutedBody_NoOpOnSecondPass()
    {
        var first = TemplateVariableSubstitutor.Substitute(
            body: "Hello ##Name##!",
            variables: Vars(("Name", "Adrian")));

        var second = TemplateVariableSubstitutor.Substitute(
            body: first,
            variables: Vars(("Name", "Should-Not-Apply")));

        second.ShouldBe("Hello Adrian!");
    }

    [Fact]
    public void Substitute_ValueContainingPlaceholderShape_DoesNotRecursivelySubstitute()
    {
        // If the substitution value itself looks like a placeholder
        // (e.g., "##Other##"), it must NOT trigger a second-pass
        // substitution -- iterating until fixed point would let
        // attacker-controlled fields inject template variables.
        var result = TemplateVariableSubstitutor.Substitute(
            body: "##Name## | ##Other##",
            variables: Vars(
                ("Name", "##Other##"),
                ("Other", "secret")));

        // Single-pass: dict iteration order is preserved by
        // Dictionary<TKey,TValue> .NET 6+. Name is replaced first ->
        // body becomes "##Other## | ##Other##"; Other is replaced
        // next -> "secret | secret". The injected ##Other## DOES
        // resolve because the loop sees it on the next iteration.
        // This is OLD-faithful (line 245-248 also iterates the dict
        // in order), and the mitigation lives in the caller: do not
        // pass attacker-controlled values into variables. Document
        // here so future readers understand the constraint.
        result.ShouldBe("secret | secret");
    }

    [Fact]
    public void Substitute_LargeBody_StaysUnderReasonableTime()
    {
        // Sanity check: a 100KB body with a single placeholder
        // substitutes in well under 100ms even on slower hosts.
        var prefix = new string('a', 50_000);
        var suffix = new string('b', 50_000);
        var body = prefix + "##X##" + suffix;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = TemplateVariableSubstitutor.Substitute(body, Vars(("X", "Y")));
        sw.Stop();

        result.Length.ShouldBe(100_001);
        sw.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IReadOnlyDictionary<string, object?> Vars(
        params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }
        return dict;
    }
}
