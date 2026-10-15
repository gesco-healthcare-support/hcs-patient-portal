using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// 2026-08-19 -- pins the shape of error-message keys, because getting it wrong is silent.
///
/// <para><b>The defect.</b> A refused action reached the user as ABP's "An internal error
/// occurred during your request!" rather than its own reason. ABP looks up a
/// <c>BusinessException</c> code using the COMPLETE code as the localization key -- for
/// <c>CaseEvaluation:Appointment.BookingSlotFull</c> the key must be exactly that. Every entry
/// in en.json instead used a shortened <c>Appointment:BookingSlotFull</c>, which ABP never
/// looks up, so the lookup missed and ABP substituted its generic text. Note that a failed
/// lookup does NOT fall back to the exception's own message, which is why nothing surfaced.</para>
///
/// <para><b>Why a test.</b> Both the old and new key shapes are plausible-looking JSON, the
/// failure produces no error anywhere, and the previous conclusion drawn from the same symptom
/// was that ABP's <c>MapCodeNamespace</c> did not work -- which sent two developers down the
/// path of hand-written translators. A test is the only thing here that tells the truth.</para>
///
/// <para>This asserts on the REAL localizer rather than a substitute: a substitute returns a
/// plausible string for a key that does not exist, which is precisely the failure being
/// guarded.</para>
/// </summary>
public abstract class ErrorCodeLocalizationTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IStringLocalizer<CaseEvaluationResource> _l;

    protected ErrorCodeLocalizationTests()
    {
        _l = GetRequiredService<IStringLocalizer<CaseEvaluationResource>>();
    }

    /// <summary>
    /// Codes that legitimately have no localization entry, each for a checked reason. Anything
    /// else without a message is a bug: the user gets the generic dialog.
    /// </summary>
    private static readonly HashSet<string> NoEntryExpected = new()
    {
        // Declared but never thrown anywhere in src/. Nothing to localize.
        CaseEvaluationDomainErrorCodes.ResetPasswordTokenInvalid,

        // Only ever thrown as a UserFriendlyException that already carries its own text, and
        // ABP uses that message directly rather than looking up the code.
        CaseEvaluationDomainErrorCodes.AppointmentDocumentInvalidFileFormat,
    };

    /// <summary>
    /// Every code ABP is expected to localize. Aggregated into one pass rather than a theory
    /// per code: the ABP test host is expensive to spin up, and one failure listing all
    /// offenders is more useful than the first one aborting the run.
    /// </summary>
    private static IEnumerable<string> CodesAbpMustLocalize() =>
        typeof(CaseEvaluationDomainErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(c => c.StartsWith("CaseEvaluation:"))
            .Distinct()
            .Where(c => !NoEntryExpected.Contains(c))
            .OrderBy(c => c);

    /// <summary>
    /// The load-bearing assertion. A code whose key is the wrong shape resolves to nothing, and
    /// the user sees ABP's generic dialog instead of the reason.
    /// </summary>
    [Fact]
    public void Every_error_code_resolves_to_a_real_message()
    {
        var unresolved = CodesAbpMustLocalize()
            .Where(c => _l[c].ResourceNotFound || _l[c].Value == c)
            .ToList();

        unresolved.ShouldBeEmpty(
            "en.json must hold an entry keyed by the FULL code. A shortened key is never looked "
            + "up, so these would reach the user as 'An internal error occurred':\n  "
            + string.Join("\n  ", unresolved));
    }

    /// <summary>
    /// Placeholders are filled BY NAME from the exception's <c>Data</c>, so a positional
    /// <c>{0}</c> left in a message renders literally to the user.
    /// </summary>
    [Fact]
    public void No_message_uses_a_positional_placeholder()
    {
        var positional = CodesAbpMustLocalize()
            .Where(c => !_l[c].ResourceNotFound && _l[c].Value.Contains("{0}"))
            .ToList();

        positional.ShouldBeEmpty(
            "these must use a named placeholder matching their WithData key, or the token "
            + "renders literally:\n  " + string.Join("\n  ", positional));
    }
}
