using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// The action label turns one audited change into "Booked", "Approved", "Document rejected" and so on.
/// Labels come from enum states only, so none of them can carry PHI; these tests pin each mapping.
/// </summary>
public class AppointmentChangeActionLabelerTests
{
    private const string Appt = AppointmentAuditedEntities.Appointment;

    private static RawEntityChange Change(
        string type, string kind, params (string Name, string? Old, string? New)[] props)
        => new(type, Guid.NewGuid().ToString(), kind, new DateTime(2026, 6, 1),
            props.Select(p => new RawPropertyChange(p.Name, p.Old, p.New)).ToList());

    private static string Status(AppointmentStatusType? old, AppointmentStatusType next)
        => AppointmentChangeActionLabeler.Label(Change(Appt, "Updated",
            ("AppointmentStatus", old == null ? null : ((int)old.Value).ToString(), ((int)next).ToString())));

    [Fact]
    public void A_created_appointment_is_Booked()
        => AppointmentChangeActionLabeler.Label(Change(Appt, "Created", ("PanelNumber", null, "TEST-P1")))
            .ShouldBe("Booked");

    [Theory]
    [InlineData(AppointmentStatusType.Approved, AppointmentStatusType.Pending, "Approved")]
    [InlineData(AppointmentStatusType.Rejected, AppointmentStatusType.Pending, "Rejected")]
    [InlineData(AppointmentStatusType.InfoRequested, AppointmentStatusType.Pending, "Sent back for more information")]
    [InlineData(AppointmentStatusType.Pending, AppointmentStatusType.InfoRequested, "Resubmitted")]
    [InlineData(AppointmentStatusType.NoShow, AppointmentStatusType.Approved, "No show")]
    [InlineData(AppointmentStatusType.NotSeen, AppointmentStatusType.Approved, "Not seen")]
    [InlineData(AppointmentStatusType.CancelledNoBill, AppointmentStatusType.Approved, "Cancelled (no bill)")]
    [InlineData(AppointmentStatusType.CancelledLate, AppointmentStatusType.Approved, "Cancelled (late)")]
    [InlineData(AppointmentStatusType.RescheduledNoBill, AppointmentStatusType.Approved, "Rescheduled (no bill)")]
    [InlineData(AppointmentStatusType.RescheduledLate, AppointmentStatusType.Approved, "Rescheduled (late)")]
    [InlineData(AppointmentStatusType.RescheduleRequested, AppointmentStatusType.Approved, "Reschedule requested")]
    [InlineData(AppointmentStatusType.CancellationRequested, AppointmentStatusType.Approved, "Cancellation requested")]
    [InlineData(AppointmentStatusType.Approved, AppointmentStatusType.RescheduleRequested, "Back to approved (change request closed)")]
    [InlineData(AppointmentStatusType.Approved, AppointmentStatusType.CancellationRequested, "Back to approved (change request closed)")]
    public void Every_status_transition_has_its_label(
        AppointmentStatusType next, AppointmentStatusType previous, string expected)
        => Status(previous, next).ShouldBe(expected);

    [Fact]
    public void A_dead_status_value_gets_the_generic_label_and_the_integers_are_untouched()
    {
        Status(AppointmentStatusType.Approved, AppointmentStatusType.CheckedIn).ShouldBe("Status changed");
        ((int)AppointmentStatusType.CheckedIn).ShouldBe(9);
        ((int)AppointmentStatusType.Billed).ShouldBe(11);
    }

    [Fact]
    public void A_status_recorded_by_name_or_with_quotes_is_still_understood()
    {
        AppointmentChangeActionLabeler.Label(Change(Appt, "Updated", ("AppointmentStatus", "\"Pending\"", "\"Approved\"")))
            .ShouldBe("Approved");
    }

    [Fact]
    public void Appointment_edits_without_a_status_move_are_Details_or_Parties_edited()
    {
        AppointmentChangeActionLabeler.Label(Change(Appt, "Updated", ("PanelNumber", "a", "b")))
            .ShouldBe("Details edited");
        AppointmentChangeActionLabeler.Label(Change(Appt, "Updated", ("ApplicantAttorneyEmail", "a", "b")))
            .ShouldBe("Parties edited");
    }

    [Fact]
    public void Change_request_actions()
    {
        const string cr = AppointmentAuditedEntities.ChangeRequest;
        AppointmentChangeActionLabeler.Label(Change(cr, "Created", ("ChangeRequestType", null, "1")))
            .ShouldBe("Cancellation requested");
        AppointmentChangeActionLabeler.Label(Change(cr, "Created", ("ChangeRequestType", null, "2")))
            .ShouldBe("Reschedule requested");
        AppointmentChangeActionLabeler.Label(Change(cr, "Updated", ("RequestStatus", "25", "26")))
            .ShouldBe("Change request approved");
        AppointmentChangeActionLabeler.Label(Change(cr, "Updated", ("RequestStatus", "25", "27")))
            .ShouldBe("Change request rejected");
    }

    [Fact]
    public void Consent_actions_name_the_side_or_both()
    {
        const string round = AppointmentAuditedEntities.ConsentRound;
        AppointmentChangeActionLabeler.Label(Change(round, "Created", ("RoundNumber", null, "1")))
            .ShouldBe("Date set");
        AppointmentChangeActionLabeler.Label(Change(round, "Updated", ("SideAConsentStatus", "1", "2")))
            .ShouldBe("Consent agreed (side A)");
        AppointmentChangeActionLabeler.Label(Change(round, "Updated", ("SideBConsentStatus", "1", "3")))
            .ShouldBe("Consent declined (side B)");
        AppointmentChangeActionLabeler.Label(Change(round, "Updated",
                ("SideAConsentStatus", "0", "1"), ("SideBConsentStatus", "0", "1")))
            .ShouldBe("Consent requested (both sides)");
        AppointmentChangeActionLabeler.Label(Change(round, "Updated", ("SupersededAt", null, "2026-06-02")))
            .ShouldBe("Date superseded");
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.ChangeRequest, "Updated",
                ("SideAConsentStatus", "1", "2")))
            .ShouldBe("Consent agreed (side A)");
    }

    [Fact]
    public void Document_actions()
    {
        const string doc = AppointmentAuditedEntities.Document;
        AppointmentChangeActionLabeler.Label(Change(doc, "Created", ("DocumentName", null, "x")))
            .ShouldBe("Document uploaded");
        AppointmentChangeActionLabeler.Label(Change(doc, "Updated", ("Status", "1", "2")))
            .ShouldBe("Document accepted");
        AppointmentChangeActionLabeler.Label(Change(doc, "Updated", ("Status", "1", "3"), ("RejectionReason", null, "x")))
            .ShouldBe("Document rejected");
        // A replace swaps the stored file AND resets the status; the file wins.
        AppointmentChangeActionLabeler.Label(Change(doc, "Updated", ("BlobName", "a", "b"), ("Status", "3", "1")))
            .ShouldBe("Document replaced");
        AppointmentChangeActionLabeler.Label(Change(doc, "Deleted")).ShouldBe("Document removed");
    }

    [Fact]
    public void Info_request_accessor_and_party_actions()
    {
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.InfoRequest, "Created"))
            .ShouldBe("Information requested");
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.InfoRequest, "Updated", ("Status", "1", "2")))
            .ShouldBe("Information answered");
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.Accessor, "Created"))
            .ShouldBe("Authorized user added");
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.Accessor, "Deleted"))
            .ShouldBe("Authorized user removed");
        // ABP may record a soft delete as an update that flips IsDeleted.
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.Accessor, "Updated", ("IsDeleted", "false", "true")))
            .ShouldBe("Authorized user removed");
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.ApplicantAttorneyLink, "Updated"))
            .ShouldBe("Parties edited");
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.ClaimExaminer, "Created"))
            .ShouldBe("Party added");
    }

    [Fact]
    public void Everything_else_falls_back_to_Details_edited()
    {
        AppointmentChangeActionLabeler.Label(Change(AppointmentAuditedEntities.InjuryDetail, "Updated", ("ClaimNumber", "a", "b")))
            .ShouldBe("Details edited");
        AppointmentChangeActionLabeler.Label(Change("Some.Unknown.Type", "Updated")).ShouldBe("Details edited");
    }

    [Fact]
    public void A_label_never_contains_the_changed_values()
    {
        var label = AppointmentChangeActionLabeler.Label(Change(
            AppointmentAuditedEntities.Document, "Updated",
            ("Status", "1", "3"), ("RejectionReason", null, "SECRET-REASON-TEXT")));
        label.ShouldNotContain("SECRET");
    }
}
