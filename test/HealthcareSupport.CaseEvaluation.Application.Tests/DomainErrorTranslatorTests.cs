using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// 2026-08-17 -- the nine delete guards must report their own reason.
///
/// <para>The defect: each guard threw a bare <see cref="BusinessException"/> carrying a
/// <c>*.InUse</c> code. ABP's auto-localization via <c>MapCodeNamespace</c> does not resolve
/// in this codebase, so the SPA received an empty message and its toast fell back to ABP's
/// "An internal error occurred during your request!". The guards were right and the messages
/// already existed; nothing joined them.</para>
///
/// <para>These run against the real localizer rather than a substitute, because a substitute
/// would happily return a plausible string for a key that does not exist -- which is exactly
/// the failure being fixed. Asserting on real resolved text is what proves the wiring.</para>
/// </summary>
public abstract class DomainErrorTranslatorTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly DomainErrorTranslator _translator;

    protected DomainErrorTranslatorTests()
    {
        _translator = GetRequiredService<DomainErrorTranslator>();
    }

    public static IEnumerable<object[]> EveryInUseCode() => new List<object[]>
    {
        new object[] { CaseEvaluationDomainErrorCodes.ApplicantAttorneyInUse },
        new object[] { CaseEvaluationDomainErrorCodes.DefenseAttorneyInUse },
        new object[] { CaseEvaluationDomainErrorCodes.PatientInUse },
        new object[] { CaseEvaluationDomainErrorCodes.DocumentInUse },
        new object[] { CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeInUse },
        new object[] { CaseEvaluationDomainErrorCodes.AppointmentTypeInUse },
        new object[] { CaseEvaluationDomainErrorCodes.AppointmentLanguageInUse },
        new object[] { CaseEvaluationDomainErrorCodes.StateInUse },
    };

    [Theory]
    [MemberData(nameof(EveryInUseCode))]
    public void EveryInUseCode_ResolvesToARealMessage(string code)
    {
        var translated = _translator.Translate(new BusinessException(code));

        translated.ShouldBeOfType<UserFriendlyException>();
        translated.Code.ShouldBe(code, "the code must survive so HTTP-status mapping is unchanged");

        // The whole point: a real sentence, not the key echoed back and not empty.
        //
        // ShouldNotContain(":InUse") is the load-bearing one. IStringLocalizer returns the KEY
        // when it cannot resolve it, so a typo in the map surfaces as the literal
        // "Document:InUse" rather than an exception -- silent, and exactly what a substituted
        // localizer would hide by returning something plausible.
        //
        // Deliberately NOT asserting a shared phrase: the nine messages are worded
        // independently (Document says "Unlink it before deleting", the others say "cannot be
        // deleted"), and pinning copy here would break on any harmless rewording.
        translated.Message.ShouldNotBeNullOrWhiteSpace();
        translated.Message.ShouldNotContain(":InUse");
        translated.Message.ShouldNotBe(code);
        translated.Message.Length.ShouldBeGreaterThan(20, "an unresolved key would be short");
        translated.Message.ShouldContain(" ", Case.Sensitive, "the message should be prose");
    }

    [Fact]
    public void LocationInUse_InterpolatesTheBlockingCountAndEntity()
    {
        // Location is the only one of the nine whose message takes arguments, and it is the
        // one most likely to break silently: its guard attaches count + entity, and the
        // message previously used NAMED placeholders ({count}/{entity}) which string.Format
        // cannot bind -- passing args against those throws FormatException rather than
        // rendering. The message now uses positional placeholders; this pins that.
        var ex = new BusinessException(CaseEvaluationDomainErrorCodes.LocationInUse)
            .WithData("entity", "Appointment")
            .WithData("count", 3);

        var translated = _translator.Translate(ex);

        translated.Message.ShouldContain("3");
        translated.Message.ShouldContain("Appointment");
        translated.Message.ShouldNotContain("{0}");
        translated.Message.ShouldNotContain("{count}");
    }

    [Fact]
    public void AnUnknownCode_PassesThroughUnchanged()
    {
        // Wrapping a manager call must never swallow an error the translator does not know
        // about, or a genuine failure would surface as a friendly message about deletion.
        var original = new BusinessException("CaseEvaluation:Something.Unmapped");

        var result = _translator.Translate(original);

        result.ShouldBeSameAs(original);
    }

    [Fact]
    public void AnAlreadyFriendlyException_IsNotDoubleWrapped()
    {
        var friendly = new UserFriendlyException("Already readable.");

        var result = _translator.Translate(friendly);

        result.ShouldBeSameAs(friendly);
    }

    [Fact]
    public void Refuse_BuildsTheSameTranslatedExceptionAsTranslate()
    {
        // The four inline guards call Refuse; the six manager call sites call Translate. Both
        // must produce the same thing or the message a user sees would depend on which layer
        // happened to raise it.
        var viaRefuse = _translator.Refuse(CaseEvaluationDomainErrorCodes.PatientInUse);
        var viaTranslate = _translator.Translate(
            new BusinessException(CaseEvaluationDomainErrorCodes.PatientInUse));

        viaRefuse.Code.ShouldBe(viaTranslate.Code);
        viaRefuse.Message.ShouldBe(viaTranslate.Message);
    }
}
