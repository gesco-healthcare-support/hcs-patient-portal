using HealthcareSupport.CaseEvaluation.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// The shipped wording of the email-routing notices (2026-11-03, PR B2a). Pure: reads each embedded body and
/// subject through the public seed defaults. Pins what a recipient would notice if it slipped: the reschedule
/// request notice is neutral (it now reaches every party, the inbox and Intake Staff, not just the requester);
/// the consent information notice carries NO consent link; the decline notice is staff-only; Not Seen does not
/// say No-Show; the reminder carries its "when"; and the cancellation notice no longer greets everyone with
/// the patient's first name.
/// </summary>
public class RoutingNoticeTemplateTests
{
    private static NotificationTemplateSeedDefault Seed(string code) =>
        NotificationTemplateSeedDefaults.GetSeedDefaults(code);

    [Theory]
    [InlineData(NotificationTemplateConsts.Codes.ChangeRequestConsentInfo)]
    [InlineData(NotificationTemplateConsts.Codes.ChangeRequestConsentDeclined)]
    [InlineData(NotificationTemplateConsts.Codes.PatientAppointmentNotSeen)]
    [InlineData(NotificationTemplateConsts.Codes.AppointmentDayReminder)]
    public void Each_new_code_is_registered_with_a_real_subject_and_body_not_the_stub(string code)
    {
        NotificationTemplateConsts.Codes.All.ShouldContain(code);
        EmailSubjects.ByCode.ShouldContainKey(code);
        var seed = Seed(code);
        seed.BodyEmail.ShouldNotContain("Stub body");
        seed.Subject.ShouldContain("##AppointmentRequestConfirmationNumber##");
    }

    [Fact]
    public void The_reschedule_request_notice_is_neutral_not_voiced_for_the_requester()
    {
        var seed = Seed(NotificationTemplateConsts.Codes.AppointmentRescheduleRequest);

        seed.Subject.ShouldBe("Appointment Portal - ##EmailSubjectIdentity## - Reschedule request received");
        seed.BodyEmail.ShouldNotContain("You have successfully");
        seed.BodyEmail.ShouldNotContain("##PatientFirstName##");
        // The date line lives inside the details block so it can be left out when no date was proposed.
        seed.BodyEmail.ShouldContain("##ConsentDetailsBlock##");
        seed.BodyEmail.ShouldNotContain("##NewAppointmentDate##");
    }

    [Fact]
    public void The_consent_information_notice_carries_no_consent_link_and_says_no_action_is_needed()
    {
        var body = Seed(NotificationTemplateConsts.Codes.ChangeRequestConsentInfo).BodyEmail;

        body.ShouldNotContain("##ConsentUrl##");
        body.ShouldContain("No action is needed");
        body.ShouldContain("##ConsentDetailsBlock##");
    }

    [Fact]
    public void The_decline_notice_is_for_clinic_staff_only_and_has_no_portal_button()
    {
        var body = Seed(NotificationTemplateConsts.Codes.ChangeRequestConsentDeclined).BodyEmail;

        body.ShouldContain("declined");
        body.ShouldContain("clinic staff only");
        body.ShouldNotContain("##PortalUrl##");
    }

    [Fact]
    public void The_not_seen_notice_says_not_seen_and_never_no_show()
    {
        var seed = Seed(NotificationTemplateConsts.Codes.PatientAppointmentNotSeen);

        seed.BodyEmail.ShouldContain("Not Seen");
        seed.BodyEmail.ShouldNotContain("No-Show");
        seed.Subject.ShouldContain("Not Seen");
    }

    [Fact]
    public void The_reminder_carries_when_the_appointment_is()
    {
        var body = Seed(NotificationTemplateConsts.Codes.AppointmentDayReminder).BodyEmail;

        body.ShouldContain("##ReminderWhen##");
        body.ShouldContain("##AppointmentDate##");
        body.ShouldContain("##AppointmentFromTime##");
    }

    [Fact]
    public void The_cancellation_request_notice_no_longer_greets_everyone_by_the_patients_first_name()
    {
        var body = Seed(NotificationTemplateConsts.Codes.AppointmentCancelledRequest).BodyEmail;

        body.ShouldNotContain("Hello ##PatientFirstName##");
        body.ShouldContain("<p>Hello,</p>");
    }
}
