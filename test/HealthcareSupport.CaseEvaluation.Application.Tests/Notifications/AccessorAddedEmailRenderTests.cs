using System;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Issue #3 (2026-07-16): pins the existing-account accessor "you were added" email so
/// it renders without dead links or leftover tokens (the same failure class that hit the
/// invite template -- see <see cref="AccessorInvitedEmailRenderTests"/>). Render-level:
/// builds the handler's real variable bag and substitutes it into the seeded body. Unlike
/// the invite, this body has NO password-setup ##URL## -- the recipient signs in at
/// ##PortalUrl## with their existing ##Email##.
/// </summary>
public class AccessorAddedEmailRenderTests
{
    private static DocumentEmailContext SampleContext() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        RequestConfirmationNumber = "A00042",
        AppointmentDate = new DateTime(2026, 7, 20),
        PatientFirstName = "Sandra",
        PatientLastName = "Rivera",
        PatientEmail = "sandra.rivera@example.test",
        PortalBaseUrl = "https://falkinstein.portal.example.test",
    };

    [Fact]
    public void Builds_bare_email_key_and_no_reset_url()
    {
        var variables = AccessorAddedEmailHandler.BuildAddedEmailVariables(
            SampleContext(),
            practiceName: "Dr. Falkinstein",
            accessorEmail: "accessor@example.test");

        variables.ShouldContainKey("Email");
        variables.ShouldNotContainKey("##Email##");
        // Existing account: no password-setup link is added (only the invite template has it).
        variables.ShouldNotContainKey("URL");
        variables["ClinicName"].ShouldBe("Dr. Falkinstein");
    }

    [Fact]
    public void Rendered_body_has_portal_link_email_and_practice_no_leftover_tokens()
    {
        const string portalUrl = "https://falkinstein.portal.example.test";
        const string accessorEmail = "accessor@example.test";
        const string practiceName = "Dr. Falkinstein";

        var variables = AccessorAddedEmailHandler.BuildAddedEmailVariables(
            SampleContext(), practiceName, accessorEmail);

        var body = NotificationTemplateSeedDefaults
            .GetSeedDefaults(NotificationTemplateConsts.Codes.AccessorAppointmentAdded)
            .BodyEmail;

        var rendered = TemplateVariableSubstitutor.Substitute(body, variables);

        rendered.ShouldContain(portalUrl);
        rendered.ShouldContain(accessorEmail);
        rendered.ShouldContain("Sandra Rivera");
        rendered.ShouldContain("A00042");
        rendered.ShouldContain(practiceName);

        rendered.ShouldNotContain("##PortalUrl##");
        rendered.ShouldNotContain("##Email##");
        rendered.ShouldNotContain("##ClinicName##");
        rendered.ShouldNotContain("##PatientFullName##");
        rendered.ShouldNotContain("##AppointmentRequestConfirmationNumber##");
    }
}
