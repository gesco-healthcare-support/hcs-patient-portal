using System.Linq;
using HealthcareSupport.CaseEvaluation.Emailing;
using Shouldly;
using Volo.Abp.Account.Emailing;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// OBS-14 Scriban-avoidance guard (added 2026-05-22). Companion to the
/// compile-time guard in Directory.Build.props + BannedSymbols.txt.
///
/// <para>Background: ABP 10.0.2 ships expecting Scriban 6.3.0; we pin Scriban
/// to 7.2.0 for CVE patches. The 7.x <c>ParserOptions</c> layout is binary-
/// incompatible with 6.x, so any code path that resolves ABP's Scriban-backed
/// <c>ITemplateRenderer</c> throws <see cref="System.TypeLoadException"/>.
/// The whole app routes around Scriban (see
/// <c>CaseEvaluationAccountEmailer</c> XML doc + Directory.Build.props comment
/// block at lines 37-72).</para>
///
/// <para>This test asserts the *single* runtime invariant that keeps the
/// route-around working: <see cref="CaseEvaluationAccountEmailer"/> must carry
/// <c>[Dependency(ReplaceServices = true)]</c> and <c>[ExposeServices(typeof(
/// IAccountEmailer))]</c>. If either attribute is removed, ABP's default
/// AccountEmailer wins the DI race, the default emailer resolves the Scriban-
/// backed <c>ITemplateRenderer</c>, and every Forgot Password / Email
/// Confirmation / 2FA send hits <see cref="System.TypeLoadException"/>.</para>
/// </summary>
public class ScribanAvoidanceTests
{
    [Fact]
    public void CaseEvaluationAccountEmailer_HasReplaceServicesAttribute()
    {
        var dependencyAttribute = typeof(CaseEvaluationAccountEmailer)
            .GetCustomAttributes(typeof(DependencyAttribute), inherit: false)
            .Cast<DependencyAttribute>()
            .FirstOrDefault();

        dependencyAttribute.ShouldNotBeNull(
            "CaseEvaluationAccountEmailer must carry [Dependency(ReplaceServices = true)] " +
            "to win the DI race against ABP's default AccountEmailer. Without the override " +
            "ABP's default emailer resolves the Scriban-backed ITemplateRenderer and throws " +
            "System.TypeLoadException because ABP 10.x targets Scriban 6.3.0 but we pin to " +
            "Scriban 7.2.0 for CVE patches. See OBS-14 and Directory.Build.props lines 37-72.");

        dependencyAttribute.ReplaceServices.ShouldBeTrue(
            "The [Dependency] attribute is present but ReplaceServices is false. " +
            "Set ReplaceServices = true so the override displaces ABP's default IAccountEmailer.");
    }

    [Fact]
    public void CaseEvaluationAccountEmailer_ExposesIAccountEmailer()
    {
        var exposeAttribute = typeof(CaseEvaluationAccountEmailer)
            .GetCustomAttributes(typeof(ExposeServicesAttribute), inherit: false)
            .Cast<ExposeServicesAttribute>()
            .FirstOrDefault();

        exposeAttribute.ShouldNotBeNull(
            "CaseEvaluationAccountEmailer must carry [ExposeServices(typeof(IAccountEmailer))] " +
            "so ABP's auto-registration scans it as an IAccountEmailer provider. Without this " +
            "attribute the class registers only as itself, ABP's default AccountEmailer wins " +
            "the IAccountEmailer resolution, and Scriban-backed rendering kicks in.");

        exposeAttribute.ServiceTypes.ShouldContain(
            typeof(IAccountEmailer),
            "ExposeServices is present but does not list IAccountEmailer. Add typeof(IAccountEmailer) " +
            "so this class wins the IAccountEmailer DI registration.");
    }

    [Fact]
    public void CaseEvaluationAccountEmailer_ImplementsIAccountEmailer()
    {
        typeof(IAccountEmailer).IsAssignableFrom(typeof(CaseEvaluationAccountEmailer))
            .ShouldBeTrue(
                "CaseEvaluationAccountEmailer must implement IAccountEmailer for the override " +
                "to be a valid replacement of ABP's default. See OBS-14 for the full Scriban " +
                "avoidance pipeline.");
    }
}
