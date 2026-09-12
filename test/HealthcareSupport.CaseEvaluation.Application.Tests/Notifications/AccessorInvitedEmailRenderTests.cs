using System;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// 2026-07-10 QA (item 2): pins the accessor-invite email so the two regressions
/// found in triage cannot recur -- (1) the password-setup link and the invited
/// email must render (the handler passed pre-wrapped "##URL##"/"##Email##" keys,
/// which the substitutor double-wrapped into "####URL####" so they never matched
/// and the button was a dead link); (2) the "... at {practice}" location must not
/// be blank (the handler passed the null ICurrentTenant.Name). Render-level: builds
/// the handler's real variable bag and substitutes it into the seeded template body.
/// </summary>
public class AccessorInvitedEmailRenderTests
{
    private static DocumentEmailContext SampleContext() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        RequestConfirmationNumber = "A00002",
        AppointmentDate = new DateTime(2026, 7, 15),
        PatientFirstName = "Daniel",
        PatientLastName = "Harper",
        PatientEmail = "daniel.harper@example.test",
        PortalBaseUrl = "https://falkinstein.portal.example.test",
    };

    [Fact]
    public void Builds_bare_keys_not_double_wrapped()
    {
        var variables = AccessorInvitedEmailHandler.BuildAccessorEmailVariables(
            SampleContext(),
            practiceName: "Dr. Falkinstein",
            setupUrl: "https://x/Account/ResetPassword?userId=a&resetToken=b",
            invitedEmail: "accessor@example.test");

        // The substitutor wraps each key as "##" + key + "##"; keys must be bare.
        variables.ShouldContainKey("URL");
        variables.ShouldContainKey("Email");
        variables.ShouldNotContainKey("##URL##");
        variables.ShouldNotContainKey("##Email##");
        variables["ClinicName"].ShouldBe("Dr. Falkinstein");
    }

    [Fact]
    public void Rendered_body_has_live_link_email_and_practice_no_leftover_tokens()
    {
        const string setupUrl = "https://falkinstein.auth.portal.example.test/Account/ResetPassword?userId=a1b2&resetToken=tok-xyz";
        const string invitedEmail = "accessor@example.test";
        const string practiceName = "Dr. Falkinstein";

        var variables = AccessorInvitedEmailHandler.BuildAccessorEmailVariables(
            SampleContext(), practiceName, setupUrl, invitedEmail);

        var body = NotificationTemplateSeedDefaults
            .GetSeedDefaults(NotificationTemplateConsts.Codes.AccessorAppointmentBooked)
            .BodyEmail;

        var rendered = TemplateVariableSubstitutor.Substitute(body, variables);

        // The password-setup link, the invited email, the patient, the confirmation
        // number, and the practice location all render...
        rendered.ShouldContain(setupUrl);
        rendered.ShouldContain(invitedEmail);
        rendered.ShouldContain("Daniel Harper");
        rendered.ShouldContain("A00002");
        rendered.ShouldContain(practiceName);

        // ...and no template placeholder survives unsubstituted.
        rendered.ShouldNotContain("##URL##");
        rendered.ShouldNotContain("##Email##");
        rendered.ShouldNotContain("##ClinicName##");
        rendered.ShouldNotContain("##PatientFullName##");
        rendered.ShouldNotContain("##AppointmentRequestConfirmationNumber##");
    }
}
