using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Pure unit tests for <see cref="NotificationTemplateVariableCatalog"/>
/// (no DB / DI) -- the B-B2 variable-chip palette, the customized-badge
/// derivation, and the send-test sample-variable map.
/// </summary>
public class NotificationTemplateVariableCatalogTests
{
    // ------------------------------------------------------------------
    // ExtractTokens
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no tokens here")]
    [InlineData("half ##open and #single# markers")]
    public void ExtractTokens_NoTokens_ReturnsEmpty(string? text)
        => NotificationTemplateVariableCatalog.ExtractTokens(text).ShouldBeEmpty();

    [Fact]
    public void ExtractTokens_ReturnsDistinct_FirstSeenOrder()
    {
        var tokens = NotificationTemplateVariableCatalog.ExtractTokens(
            "Hi ##UserName##, your ##RoleName## at ##TenantName## -- regards, ##UserName##.");

        tokens.ShouldBe(new[] { "UserName", "RoleName", "TenantName" });
    }

    // ------------------------------------------------------------------
    // GetVariablesForCode
    // ------------------------------------------------------------------

    [Fact]
    public void GetVariablesForCode_InviteExternalUser_UnionsSubjectAndBody()
    {
        var tokens = NotificationTemplateVariableCatalog.GetVariablesForCode(
            NotificationTemplateConsts.Codes.InviteExternalUser);

        // Subject contributes TenantName first; the body adds the rest.
        tokens.First().ShouldBe("TenantName");
        tokens.ShouldContain("Greeting");
        tokens.ShouldContain("RoleName");
        tokens.ShouldContain("URL");
        tokens.ShouldContain("ExpiresAt");
        // Distinct -- TenantName appears in both subject and body but once here.
        tokens.Count(t => t == "TenantName").ShouldBe(1);
    }

    [Fact]
    public void GetVariablesForCode_StubCode_ReturnsEmpty()
        => NotificationTemplateVariableCatalog
            .GetVariablesForCode(NotificationTemplateConsts.Codes.AppointmentBooked)
            .ShouldBeEmpty();

    // ------------------------------------------------------------------
    // IsCustomized
    // ------------------------------------------------------------------

    [Fact]
    public void IsCustomized_MatchingSeedDefault_IsFalse()
    {
        var code = NotificationTemplateConsts.Codes.InviteExternalUser;
        var d = NotificationTemplateSeedDefaults.GetSeedDefaults(code);

        NotificationTemplateVariableCatalog
            .IsCustomized(code, d.Subject, d.BodyEmail, d.BodySms)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsCustomized_EditedSubject_IsTrue()
    {
        var code = NotificationTemplateConsts.Codes.InviteExternalUser;
        var d = NotificationTemplateSeedDefaults.GetSeedDefaults(code);

        NotificationTemplateVariableCatalog
            .IsCustomized(code, "Edited subject", d.BodyEmail, d.BodySms)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsCustomized_EditedSms_IsTrue()
    {
        var code = NotificationTemplateConsts.Codes.InviteExternalUser;
        var d = NotificationTemplateSeedDefaults.GetSeedDefaults(code);

        NotificationTemplateVariableCatalog
            .IsCustomized(code, d.Subject, d.BodyEmail, "Edited SMS body")
            .ShouldBeTrue();
    }

    [Fact]
    public void IsCustomized_NullFieldsAgainstNonEmptyDefault_IsTrue()
        => NotificationTemplateVariableCatalog
            .IsCustomized(NotificationTemplateConsts.Codes.InviteExternalUser, null, null, null)
            .ShouldBeTrue();

    // ------------------------------------------------------------------
    // Humanize
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("ExpiresAt", "Expires At")]
    [InlineData("TenantName", "Tenant Name")]
    [InlineData("EmailSubjectIdentity", "Email Subject Identity")]
    [InlineData("URL", "URL")]
    [InlineData("", "")]
    public void Humanize_SplitsPascalCase(string token, string expected)
        => NotificationTemplateVariableCatalog.Humanize(token).ShouldBe(expected);

    // ------------------------------------------------------------------
    // BuildSampleVariables
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSampleVariables_CoversEveryToken_WithKnownAndFallbackValues()
    {
        var code = NotificationTemplateConsts.Codes.InviteExternalUser;
        var samples = NotificationTemplateVariableCatalog.BuildSampleVariables(code);
        var tokens = NotificationTemplateVariableCatalog.GetVariablesForCode(code);

        // Every valid token gets a sample value.
        foreach (var token in tokens)
        {
            samples.ShouldContainKey(token);
        }

        // Known tokens use curated samples...
        samples["TenantName"].ShouldBe("Falkinstein Orthopedics");
        // ...unknown tokens fall back to a bracketed humanized label.
        samples["Greeting"].ShouldBe("[Greeting]");
    }
}
