using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pins <see cref="Appointment.CopyPartySnapshotFrom"/>, added 2026-08-26 after a live finding on the
/// box: the reschedule split created the replacement appointment with every denormalised party column
/// NULL, so the read-access guard 403'd the external party of record off their own replacement
/// appointment -- while they held its packet PDF in their inbox.
///
/// <para>The email columns are the load-bearing ones: the guard's email+role rule reads
/// PatientEmail / ApplicantAttorneyEmail / DefenseAttorneyEmail / ClaimExaminerEmail off the
/// appointment, and the replacement had a staff CreatorId and no accessor rows, so that rule was the
/// only pathway left. They are asserted individually below rather than folded into the bulk check,
/// because losing one of those four silently reintroduces the outage for that role only.</para>
///
/// <para>All values here are synthetic.</para>
/// </summary>
public class AppointmentPartySnapshotCopyTests
{
    private static Appointment NewAppointment() => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: Guid.NewGuid(),
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 9, 4, 10, 30, 0),
        requestConfirmationNumber: "A00099",
        appointmentStatus: AppointmentStatusType.Approved);

    private static Appointment FullySnapshottedSource()
    {
        var a = NewAppointment();

        a.PatientEmail = "patient@example.test";
        a.PatientFirstName = "Jane";
        a.PatientMiddleName = "Q";
        a.PatientLastName = "Smith";
        a.PatientDateOfBirth = new DateTime(1978, 5, 17);
        a.PatientSocialSecurityNumber = "synthetic-not-a-real-identifier";
        a.PatientPhoneNumber = "5105550100";
        a.PatientCellPhoneNumber = "5105550101";
        a.PatientStreet = "400 Aberdeen Drive";
        a.PatientApptNumber = "802";
        a.PatientCity = "Carlsbad";
        a.PatientStateId = Guid.NewGuid();
        a.PatientZipCode = "98756-0000";
        a.PatientGenderId = Gender.Female;
        a.PatientInterpreterVendorName = "Acme Interpreting";

        a.ApplicantAttorneyEmail = "aa@example.test";
        a.ApplicantAttorneyFirstName = "Marcus";
        a.ApplicantAttorneyLastName = "Bennett";
        a.ApplicantAttorneyFirmName = "Bennett and Associates";
        a.ApplicantAttorneyWebAddress = "https://example.test";
        a.ApplicantAttorneyPhoneNumber = "5105550142";
        a.ApplicantAttorneyFaxNumber = "5105550143";
        a.ApplicantAttorneyStreet = "1400 Harbor Boulevard";
        a.ApplicantAttorneyCity = "Oakland";
        a.ApplicantAttorneyStateId = Guid.NewGuid();
        a.ApplicantAttorneyZipCode = "94607-0000";

        a.DefenseAttorneyEmail = "da@example.test";
        a.DefenseAttorneyFirstName = "Gregory";
        a.DefenseAttorneyLastName = "Stone";
        a.DefenseAttorneyFirmName = "Stone Defense";
        a.DefenseAttorneyWebAddress = "https://defense.example.test";
        a.DefenseAttorneyPhoneNumber = "5105550200";
        a.DefenseAttorneyFaxNumber = "5105550201";
        a.DefenseAttorneyStreet = "1 Defense Way";
        a.DefenseAttorneyCity = "San Jose";
        a.DefenseAttorneyStateId = Guid.NewGuid();
        a.DefenseAttorneyZipCode = "95101-0000";

        a.ClaimExaminerEmail = "ce@example.test";

        return a;
    }

    [Fact]
    public void CopyPartySnapshotFrom_CarriesTheFourPartyEmails()
    {
        // These four ARE the access-control payload. A regression on any one of them silently locks
        // that role out of the replacement appointment, which is the outage this method exists for.
        var source = FullySnapshottedSource();
        var replacement = NewAppointment();

        replacement.CopyPartySnapshotFrom(source);

        replacement.PatientEmail.ShouldBe("patient@example.test");
        replacement.ApplicantAttorneyEmail.ShouldBe("aa@example.test");
        replacement.DefenseAttorneyEmail.ShouldBe("da@example.test");
        replacement.ClaimExaminerEmail.ShouldBe("ce@example.test");
    }

    [Fact]
    public void CopyPartySnapshotFrom_CarriesEveryDenormalisedPartyColumn()
    {
        // Reflection rather than 38 hand-written assertions: a column ADDED to the entity later and
        // forgotten in the copy is exactly how this bug recurs, and a hand-written list cannot catch
        // that. Any settable Patient* / ApplicantAttorney* / DefenseAttorney* / ClaimExaminer*
        // property that does not round-trip fails here by name.
        var source = FullySnapshottedSource();
        var replacement = NewAppointment();

        replacement.CopyPartySnapshotFrom(source);

        var partyProps = typeof(Appointment)
            .GetProperties()
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.Name.StartsWith("Patient")
                        || p.Name.StartsWith("ApplicantAttorney")
                        || p.Name.StartsWith("DefenseAttorney")
                        || p.Name.StartsWith("ClaimExaminer"))
            // PatientId identifies the person, not the snapshot: the replacement keeps its own,
            // and SamePersonGroupKey is derived from it (see ApplyPatientSnapshot's remarks).
            .Where(p => p.Name != nameof(Appointment.PatientId))
            .ToList();

        partyProps.ShouldNotBeEmpty();

        var notCopied = partyProps
            .Where(p => !Equals(p.GetValue(source), p.GetValue(replacement)))
            .Select(p => p.Name)
            .ToList();

        notCopied.ShouldBeEmpty(
            $"these party columns were not carried onto the replacement appointment: {string.Join(", ", notCopied)}");
    }

    [Fact]
    public void CopyPartySnapshotFrom_LeavesTheReplacementsOwnSlotAndIdentityAlone()
    {
        // The replacement defines its own date, slot and confirmation number -- that is the whole
        // point of the split. Copying those would silently move the appointment back.
        var source = FullySnapshottedSource();
        var replacement = NewAppointment();
        var ownDate = replacement.AppointmentDate;
        var ownSlot = replacement.DoctorAvailabilityId;
        var ownNumber = replacement.RequestConfirmationNumber;
        var ownPatientId = replacement.PatientId;

        replacement.CopyPartySnapshotFrom(source);

        replacement.AppointmentDate.ShouldBe(ownDate);
        replacement.DoctorAvailabilityId.ShouldBe(ownSlot);
        replacement.RequestConfirmationNumber.ShouldBe(ownNumber);
        replacement.PatientId.ShouldBe(ownPatientId);
    }

    [Fact]
    public void CopyPartySnapshotFrom_CopiesNullsRatherThanSkippingThem()
    {
        // The copy must not be "only if non-null": that would leave whatever the replacement
        // happened to hold, which on a real reschedule is nothing, but on a re-copy would be stale.
        var source = NewAppointment();
        var replacement = FullySnapshottedSource();

        replacement.CopyPartySnapshotFrom(source);

        replacement.ApplicantAttorneyEmail.ShouldBeNull();
        replacement.DefenseAttorneyEmail.ShouldBeNull();
        replacement.ClaimExaminerEmail.ShouldBeNull();
        replacement.PatientEmail.ShouldBeNull();
    }

    [Fact]
    public void CopyPartySnapshotFrom_RejectsANullSource()
    {
        var replacement = NewAppointment();

        Should.Throw<ArgumentNullException>(() => replacement.CopyPartySnapshotFrom(null!));
    }
}
