using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Item 5 (2026-08-14) -- the patient snapshot on the appointment.
///
/// <para>The load-bearing assertion is
/// <see cref="EditingThePatientDoesNotMoveASnapshottedAppointment"/>: that IS the feature.
/// Everything else here guards the edges around it. Before the snapshot, editing a patient
/// rewrote what every one of their prior appointments reported to the Case Tracker and rendered
/// on any regenerated packet -- an appointment is a legal trail of what was served on a date, so
/// it has to stop moving.</para>
///
/// <para>The fallback matters just as much and is NOT transitional: appointments booked before
/// this shipped were deliberately not backfilled, because their real values at booking time are
/// unknowable and stamping today's data onto a legal record would assert a history we cannot
/// support. Those rows keep reading live for good.</para>
///
/// <para>All fixture data is synthetic. Identifier-shaped fields carry word placeholders rather
/// than digit patterns, so nothing here can be mistaken for real data.</para>
/// </summary>
public class AppointmentPatientSnapshotResolverTests
{
    private static readonly Guid PatientId = new("b1c2d3e4-f5a6-4b7c-8d9e-a1b2c3d4e5f6");
    private static readonly Guid BookedTimeStateId = new("c2d3e4f5-a6b7-4c8d-9e1f-b2c3d4e5f6a7");
    private static readonly Guid EditedStateId = new("d3e4f5a6-b7c8-4d9e-8f1a-c3d4e5f6a7b8");

    private const string BookedTimeRedactedId = "TEST-REDACTED-BOOKED";
    private const string EditedRedactedId = "TEST-REDACTED-EDITED";

    private static Appointment BuildAppointment()
    {
        return new Appointment(
            id: new Guid("e4f5a6b7-c8d9-4e1f-8a2b-d4e5f6a7b8c9"),
            patientId: PatientId,
            identityUserId: null,
            appointmentTypeId: new Guid("f5a6b7c8-d9e1-4f2a-8b3c-e5f6a7b8c9d1"),
            locationId: new Guid("a6b7c8d9-e1f2-4a3b-8c4d-f6a7b8c9d1e2"),
            doctorAvailabilityId: new Guid("b7c8d9e1-f2a3-4b4c-8d5e-a7b8c9d1e2f3"),
            appointmentDate: new DateTime(2027, 5, 6, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00099",
            appointmentStatus: AppointmentStatusType.Approved);
    }

    /// <summary>The patient as they are TODAY, after someone corrected the record.</summary>
    private static Patient BuildEditedPatient()
    {
        return new Patient(
            id: PatientId,
            stateId: EditedStateId,
            appointmentLanguageId: null,
            identityUserId: null,
            tenantId: null,
            firstName: "TESTEDITED",
            lastName: "TESTSURNAMEEDITED",
            email: "TEST-edited@test.local",
            genderId: Gender.Female,
            dateOfBirth: new DateTime(1985, 2, 2, 0, 0, 0, DateTimeKind.Utc),
            phoneNumberTypeId: PhoneNumberType.Work,
            middleName: "EDITEDMIDDLE",
            socialSecurityNumber: EditedRedactedId,
            city: "Editedville",
            zipCode: "90211",
            street: "999 Edited Street",
            interpreterVendorName: "Edited Vendor",
            apptNumber: "SUITE EDITED");
    }

    /// <summary>Stamps a booked-time copy onto the appointment, as the create path does.</summary>
    private static void StampSnapshot(Appointment appointment)
    {
        appointment.PatientFirstName = "TESTBOOKED";
        appointment.PatientMiddleName = "BOOKEDMIDDLE";
        appointment.PatientLastName = "TESTSURNAMEBOOKED";
        appointment.PatientEmail = "TEST-booked@test.local";
        appointment.PatientDateOfBirth = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        appointment.PatientSocialSecurityNumber = BookedTimeRedactedId;
        appointment.PatientPhoneNumberTypeId = PhoneNumberType.Home;
        appointment.PatientStreet = "100 Booked Street";
        appointment.PatientApptNumber = "SUITE BOOKED";
        appointment.PatientCity = "Bookedville";
        appointment.PatientStateId = BookedTimeStateId;
        appointment.PatientZipCode = "90210";
        appointment.PatientGenderId = Gender.Male;
        appointment.PatientInterpreterVendorName = "Booked Vendor";
    }

    private static AppointmentPatientSnapshotResolver BuildResolver(Patient? livePatient)
    {
        var repo = Substitute.For<IRepository<Patient, Guid>>();
        repo.FindAsync(PatientId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(livePatient));
        return new AppointmentPatientSnapshotResolver(repo);
    }

    [Fact]
    public async Task EditingThePatientDoesNotMoveASnapshottedAppointment()
    {
        // THE test this feature exists for. The live patient has been edited to entirely
        // different values; the appointment must still report what it was booked with.
        var appointment = BuildAppointment();
        StampSnapshot(appointment);
        var resolver = BuildResolver(BuildEditedPatient());

        var result = await resolver.ResolveAsync(appointment);

        result.ShouldNotBeNull();
        result!.FirstName.ShouldBe("TESTBOOKED");
        result.LastName.ShouldBe("TESTSURNAMEBOOKED");
        result.Street.ShouldBe("100 Booked Street");
        result.Unit.ShouldBe("SUITE BOOKED");
        result.City.ShouldBe("Bookedville");
        result.ZipCode.ShouldBe("90210");
        result.StateId.ShouldBe(BookedTimeStateId);
        result.SocialSecurityNumber.ShouldBe(BookedTimeRedactedId);
        result.DateOfBirth.ShouldBe(new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.GenderId.ShouldBe(Gender.Male);
        result.PhoneNumberTypeId.ShouldBe(PhoneNumberType.Home);
        result.InterpreterVendorName.ShouldBe("Booked Vendor");
    }

    [Fact]
    public async Task ASnapshottedAppointmentDoesNotReadThePatientAtAll()
    {
        // Not merely "prefers the snapshot" -- it must not touch the patient row, or a deleted or
        // tenant-filtered patient could still blank a historical appointment.
        var appointment = BuildAppointment();
        StampSnapshot(appointment);
        var repo = Substitute.For<IRepository<Patient, Guid>>();
        var resolver = new AppointmentPatientSnapshotResolver(repo);

        var result = await resolver.ResolveAsync(appointment);

        result.ShouldNotBeNull();
        await repo.DidNotReceive().FindAsync(
            Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAppointmentBookedBeforeTheSnapshotReadsTheLivePatient()
    {
        // Pre-2026-08-14 rows were deliberately not backfilled, so they keep their previous
        // behaviour rather than reporting nothing.
        var appointment = BuildAppointment();
        var resolver = BuildResolver(BuildEditedPatient());

        var result = await resolver.ResolveAsync(appointment);

        result.ShouldNotBeNull();
        result!.FirstName.ShouldBe("TESTEDITED");
        result.Street.ShouldBe("999 Edited Street");
        result.Unit.ShouldBe("SUITE EDITED");
        result.StateId.ShouldBe(EditedStateId);
    }

    [Fact]
    public async Task TheLiveFallbackStillPrefersApptNumberOverTheLegacyAddressColumn()
    {
        // The unit lived in two columns. ApptNumber wins because it is where staff corrections
        // land; Address survives only for rows booked before that fix.
        var appointment = BuildAppointment();
        var patient = BuildEditedPatient();
        patient.ApptNumber = null;
        patient.Address = "SUITE LEGACY";
        var resolver = BuildResolver(patient);

        var result = await resolver.ResolveAsync(appointment);

        result!.Unit.ShouldBe("SUITE LEGACY");
    }

    [Fact]
    public async Task AMissingPatientOnAnUnsnapshottedAppointmentResolvesToNull()
    {
        // Callers render an empty section rather than throwing; that behaviour predates this
        // resolver and must survive it.
        var appointment = BuildAppointment();
        var resolver = BuildResolver(livePatient: null);

        var result = await resolver.ResolveAsync(appointment);

        result.ShouldBeNull();
    }
}
