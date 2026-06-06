using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// E1 (2026-06-03) -- unit tests for the ex-parte addressing split used by the
/// appointment-request notice. One message is addressed To the booker (or the
/// patient when the booker is not a party) with the other parties CC'd; the
/// office mailbox is NOT a party and is returned separately for its own notice.
/// </summary>
public class AppointmentRequestedAddressingUnitTests
{
    private static SendAppointmentEmailArgs R(string email, RecipientRole role) =>
        new SendAppointmentEmailArgs { To = email, Role = role };

    [Fact]
    public void Partition_BookerIsParty_BookerIsTo_OthersCc_OfficeSeparate()
    {
        var recipients = new List<SendAppointmentEmailArgs>
        {
            R("patient@gesco.com", RecipientRole.Patient),
            R("aa@gesco.com", RecipientRole.ApplicantAttorney),
            R("ce@gesco.com", RecipientRole.ClaimExaminer),
            R("office@gesco.com", RecipientRole.OfficeAdmin),
        };

        var result = BookingSubmissionEmailHandler.PartitionAppointmentRequested(
            recipients, bookerEmail: "aa@gesco.com");

        result.To!.To.ShouldBe("aa@gesco.com");
        result.Cc.Select(c => c.To).ShouldBe(new[] { "patient@gesco.com", "ce@gesco.com" }, ignoreOrder: true);
        result.Cc.Any(c => c.To == "office@gesco.com").ShouldBeFalse();
        result.Office.Single().To.ShouldBe("office@gesco.com");
    }

    [Fact]
    public void Partition_BookerNotAParty_FallsBackToPatient()
    {
        // Internal staff booked on a patient's behalf: the booker email is not
        // among the parties, so the patient becomes the To.
        var recipients = new List<SendAppointmentEmailArgs>
        {
            R("aa@gesco.com", RecipientRole.ApplicantAttorney),
            R("patient@gesco.com", RecipientRole.Patient),
        };

        var result = BookingSubmissionEmailHandler.PartitionAppointmentRequested(
            recipients, bookerEmail: "staff@gesco.com");

        result.To!.To.ShouldBe("patient@gesco.com");
        result.Cc.Single().To.ShouldBe("aa@gesco.com");
        result.Office.ShouldBeEmpty();
    }

    [Fact]
    public void Partition_NullBookerEmail_FallsBackToPatient()
    {
        var recipients = new List<SendAppointmentEmailArgs>
        {
            R("da@gesco.com", RecipientRole.DefenseAttorney),
            R("patient@gesco.com", RecipientRole.Patient),
        };

        var result = BookingSubmissionEmailHandler.PartitionAppointmentRequested(
            recipients, bookerEmail: null);

        result.To!.To.ShouldBe("patient@gesco.com");
        result.Cc.Single().To.ShouldBe("da@gesco.com");
    }

    [Fact]
    public void Partition_OnlyOffice_NoToNoCc()
    {
        var recipients = new List<SendAppointmentEmailArgs>
        {
            R("office@gesco.com", RecipientRole.OfficeAdmin),
        };

        var result = BookingSubmissionEmailHandler.PartitionAppointmentRequested(
            recipients, bookerEmail: "someone@gesco.com");

        result.To.ShouldBeNull();
        result.Cc.ShouldBeEmpty();
        result.Office.Single().To.ShouldBe("office@gesco.com");
    }
}
