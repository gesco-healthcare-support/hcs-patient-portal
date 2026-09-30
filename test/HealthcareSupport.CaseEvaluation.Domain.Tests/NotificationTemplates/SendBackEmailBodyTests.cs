using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Pins the send-back (PatientAppointmentInfoRequested) email body to its
/// shipped template + the HIPAA invariant that NO patient field VALUE is ever
/// templated into the body -- the email carries only the staff note + a deep
/// link; the fix-it page (not the email) shows the fields. Pure: reads the
/// embedded body via the public seed-default + substitutor, no DB.
/// </summary>
public class SendBackEmailBodyTests
{
    private const string Code = NotificationTemplateConsts.Codes.PatientAppointmentInfoRequested;

    private static string Body() => NotificationTemplateSeedDefaults.GetSeedDefaults(Code).BodyEmail;

    [Fact]
    public void Body_is_the_shipped_template_not_the_stub()
    {
        var body = Body();
        body.ShouldNotContain("Stub body");
        body.ShouldContain("##InfoRequestNote##");
        body.ShouldContain("##AppointmentViewUrl##");
    }

    [Theory]
    [InlineData("##SocialSecurityNumber##")]
    [InlineData("##DateOfBirth##")]
    [InlineData("##Address##")]
    [InlineData("##CellPhoneNumber##")]
    public void Body_never_templates_a_patient_field_value(string valueToken)
    {
        Body().ShouldNotContain(valueToken);
    }

    [Fact]
    public void Rendered_body_carries_note_and_link_but_drops_any_field_value()
    {
        var vars = new Dictionary<string, object?>
        {
            ["BookerFullName"] = "Maria Gonzalez",
            ["InfoRequestNote"] = "Please correct your details and resubmit.",
            ["AppointmentViewUrl"] = "http://falkinstein.localhost:4250/appointments/view/abc",
            ["AppointmentRequestConfirmationNumber"] = "PQ-24817",
            // If the body ever templated a field value, these sentinels would
            // surface. The body has no value tokens, so they must be dropped.
            ["SocialSecurityNumber"] = "REDACTED-SSN-SENTINEL",
            ["DateOfBirth"] = "REDACTED-DOB-SENTINEL",
        };

        var rendered = TemplateVariableSubstitutor.Substitute(Body(), vars);

        rendered.ShouldContain("Please correct your details and resubmit.");
        rendered.ShouldContain("/appointments/view/abc");
        rendered.ShouldNotContain("REDACTED-SSN-SENTINEL");
        rendered.ShouldNotContain("REDACTED-DOB-SENTINEL");
    }
}
