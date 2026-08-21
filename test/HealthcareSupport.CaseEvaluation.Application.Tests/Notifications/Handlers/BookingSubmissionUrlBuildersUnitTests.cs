using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Issue 2.4 (2026-05-12) — verifies the URL shape for the
/// AppointmentRequested* email templates. Register URL must carry
/// __tenant + email + role; Login URL must carry __tenant + email.
/// </summary>
public class BookingSubmissionUrlBuildersUnitTests
{
    private const string AuthBase = "http://falkinstein.localhost:44368";

    [Fact]
    public void BuildRegisterUrl_AllParams_IncludesTenantEmailRole()
    {
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            AuthBase, "Falkinstein", "newuser@gesco.com", RecipientRole.DefenseAttorney);
        url.ShouldBe("http://falkinstein.localhost:44368/Account/Register?__tenant=Falkinstein&email=newuser%40gesco.com&role=Defense+Attorney");
    }

    [Fact]
    public void BuildRegisterUrl_ApplicantAttorneyRole_EncodesSpace()
    {
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            AuthBase, "Falkinstein", "u@x.com", RecipientRole.ApplicantAttorney);
        url.ShouldContain("role=Applicant+Attorney");
    }

    [Fact]
    public void BuildRegisterUrl_ClaimExaminer_HasRoleParam()
    {
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            AuthBase, "Falkinstein", "u@x.com", RecipientRole.ClaimExaminer);
        url.ShouldContain("role=Claim+Examiner");
    }

    [Fact]
    public void BuildRegisterUrl_Patient_HasRoleParam()
    {
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            AuthBase, "Falkinstein", "u@x.com", RecipientRole.Patient);
        url.ShouldContain("role=Patient");
    }

    [Fact]
    public void BuildRegisterUrl_OfficeAdmin_SkipsRoleParam()
    {
        // OfficeAdmin uses a different template that never carries the
        // register link. Defensive: even if the helper is called, no
        // role= should appear (RoleToRoleName returns empty).
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            AuthBase, "Falkinstein", "u@x.com", RecipientRole.OfficeAdmin);
        url.ShouldNotContain("role=");
    }

    [Fact]
    public void BuildRegisterUrl_NullTenant_OmitsTenantParam()
    {
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            AuthBase, null, "u@x.com", RecipientRole.Patient);
        url.ShouldNotContain("__tenant=");
        url.ShouldContain("email=u%40x.com");
        url.ShouldContain("role=Patient");
    }

    [Fact]
    public void BuildRegisterUrl_TrimsTrailingSlash()
    {
        var url = BookingSubmissionEmailHandler.BuildRegisterUrl(
            "http://falkinstein.localhost:44368/", "Falkinstein", "u@x.com", RecipientRole.Patient);
        url.ShouldStartWith("http://falkinstein.localhost:44368/Account/Register?");
    }

    [Fact]
    public void BuildLoginUrl_AllParams_IncludesTenantAndEmail()
    {
        var url = BookingSubmissionEmailHandler.BuildLoginUrl(
            AuthBase, "Falkinstein", "user@gesco.com");
        url.ShouldBe("http://falkinstein.localhost:44368/Account/Login?__tenant=Falkinstein&email=user%40gesco.com");
    }

    [Fact]
    public void BuildLoginUrl_NeverHasRoleParam()
    {
        // Login pages don't take a role — Login URL must never carry one.
        var url = BookingSubmissionEmailHandler.BuildLoginUrl(
            AuthBase, "Falkinstein", "user@gesco.com");
        url.ShouldNotContain("role=");
    }

    [Fact]
    public void BuildLoginUrl_NullTenant_OmitsTenantParam()
    {
        var url = BookingSubmissionEmailHandler.BuildLoginUrl(
            AuthBase, null, "user@gesco.com");
        url.ShouldNotContain("__tenant=");
        url.ShouldContain("email=user%40gesco.com");
    }

    [Fact]
    public void BuildLoginUrl_EmptyEmail_OmitsEmailParam()
    {
        var url = BookingSubmissionEmailHandler.BuildLoginUrl(
            AuthBase, "Falkinstein", string.Empty);
        url.ShouldContain("__tenant=Falkinstein");
        url.ShouldNotContain("email=");
    }
}
