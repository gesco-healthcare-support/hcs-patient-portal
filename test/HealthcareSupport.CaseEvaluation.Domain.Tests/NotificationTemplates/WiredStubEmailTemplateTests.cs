using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// QA item M (2026-06-29): two notification codes are wired to live triggers
/// (AccessorInvitedEmailHandler, JdfAutoCancelledEmailHandler) but shipped stub
/// bodies/subjects. The accessor stub dropped the ##URL## password-setup link,
/// so a newly-invited authorized party could not set a password. These tests pin
/// both codes to real, resource-backed content so the wire-without-body
/// regression cannot recur. Pure: reads the embedded body + curated subject via
/// the public seed-default, no DB.
/// </summary>
public class WiredStubEmailTemplateTests
{
    [Theory]
    [InlineData(NotificationTemplateConsts.Codes.AccessorAppointmentBooked)]
    [InlineData(NotificationTemplateConsts.Codes.AppointmentCancelledDueDate)]
    public void Wired_code_has_real_subject_and_body(string code)
    {
        var defaults = NotificationTemplateSeedDefaults.GetSeedDefaults(code);

        NotificationTemplateSeedDefaults.HasResourceBackedBody(code).ShouldBeTrue();
        defaults.BodyEmail.ShouldNotContain("Stub body");
        defaults.Subject.ShouldNotContain("TODO:");
    }

    [Fact]
    public void Accessor_invite_body_carries_the_password_setup_link()
    {
        // The whole point of the fix: the single-use setup link must render, or
        // the invited party cannot set a password and is locked out.
        var body = NotificationTemplateSeedDefaults
            .GetSeedDefaults(NotificationTemplateConsts.Codes.AccessorAppointmentBooked)
            .BodyEmail;

        body.ShouldContain("##URL##");
    }
}
