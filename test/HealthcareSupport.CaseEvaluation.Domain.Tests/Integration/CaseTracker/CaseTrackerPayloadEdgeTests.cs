using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The edges of the Case Tracker payload that the builder tests do not reach: an enum value with no
/// agreed wire value must fail loudly rather than serialize a guess; a server-local timestamp must go
/// out as UTC with a <c>Z</c>; the envelope's error shape defaults to empty strings, not nulls; and a
/// patient section with no patient behind it is the empty default rather than a crash. Each edge sits
/// beside the ordinary value it departs from. All data is synthetic.
/// </summary>
public class CaseTrackerPayloadEdgeTests
{
    [Fact]
    public void EvaluationKind_WithAnAgreedValue_MapsToIt()
    {
        EvaluationKindWire.ToWire(EvaluationKind.ReEvaluation).ShouldBe("RE_EVAL");
    }

    [Fact]
    public void EvaluationKind_WithNoAgreedValue_Throws_RatherThanSerializingAGuess()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => EvaluationKindWire.ToWire((EvaluationKind)99));
        ex.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void ChangeRequestType_WithAnAgreedValue_MapsToIt()
    {
        ChangeRequestTypeWire.ToWire(ChangeRequestType.Cancel).ShouldBe("CANCEL");
    }

    [Fact]
    public void ChangeRequestType_WithNoAgreedValue_Throws_RatherThanSerializingAGuess()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => ChangeRequestTypeWire.ToWire((ChangeRequestType)99));
        ex.ParamName.ShouldBe("type");
    }

    [Fact]
    public void AUtcTimestamp_GoesOutUnchanged_WithAZ()
    {
        var utc = new DateTime(2026, 9, 24, 19, 30, 15, 250, DateTimeKind.Utc);

        IntegrationTimestamp.ToIsoUtc(utc).ShouldBe("2026-09-24T19:30:15.2500000Z");
    }

    [Fact]
    public void AServerLocalTimestamp_IsConvertedToUtc_AndGoesOutWithAZ_NotAnOffset()
    {
        // "O" on a Local value would print the machine's offset (e.g. -07:00); the receiver's
        // skip-if-older guard compares instants, so the wire must carry the UTC instant with a Z.
        var local = new DateTime(2026, 9, 24, 12, 30, 15, 250, DateTimeKind.Local);

        var wire = IntegrationTimestamp.ToIsoUtc(local);

        wire.ShouldEndWith("Z");
        DateTime.Parse(wire, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ShouldBe(local.ToUniversalTime());
    }

    [Fact]
    public void AnEnvelopeError_DefaultsToEmptyStrings_NotNulls()
    {
        // The contract's error shape is non-null for code and message; only the field is optional.
        var error = new IntakeError();

        error.Code.ShouldBe(string.Empty);
        error.Message.ShouldBe(string.Empty);
        error.Field.ShouldBeNull();
    }

    [Fact]
    public async Task APatientSection_WithAPatientBehindIt_CarriesTheirDetails()
    {
        var (resolver, appointment) = BuildPartyResolver(patientExists: true);

        var section = await resolver.ResolvePatientAsync(appointment);

        section.FirstName.ShouldBe("TEST-Edge");
        section.LastName.ShouldBe("TEST-Patient");
    }

    [Fact]
    public async Task APatientSection_WithNoPatientBehindIt_IsTheEmptyDefault()
    {
        // No booked-time snapshot and no live row: the section is the wire default, not an exception.
        var (resolver, appointment) = BuildPartyResolver(patientExists: false);

        var section = await resolver.ResolvePatientAsync(appointment);

        section.FirstName.ShouldBe(string.Empty);
        section.LastName.ShouldBe(string.Empty);
        section.Email.ShouldBe(string.Empty);
    }

    // ------------------------------------------------------------------------

    private static readonly Guid PatientId = new("0f1e2d3c-4b5a-4697-8877-665544332211");
    private static readonly Guid Office = new("1e2d3c4b-5a69-4788-9766-554433221100");

    private static (PartyResolver Resolver, Appointment Appointment) BuildPartyResolver(bool patientExists)
    {
        var patients = Substitute.For<IRepository<Patient, Guid>>();
        patients.FindAsync(PatientId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Patient?>(patientExists
                ? new Patient(
                    PatientId,
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: null,
                    tenantId: Office,
                    firstName: "TEST-Edge",
                    lastName: "TEST-Patient",
                    email: "TEST-edge-patient@test.local",
                    genderId: default,
                    dateOfBirth: new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    phoneNumberTypeId: default)
                : null));

        // No PatientLastName on the appointment, so there is no booked-time snapshot and the resolver
        // falls back to the live patient row -- present or absent per the Fact.
        var appointment = new Appointment(
            new Guid("2d3c4b5a-6978-4899-a655-443322110099"),
            PatientId,
            identityUserId: null,
            appointmentTypeId: new Guid("3c4b5a69-7889-49aa-b544-332211009988"),
            locationId: new Guid("4b5a6978-899a-4abb-8433-221100998877"),
            doctorAvailabilityId: new Guid("5a697889-9aab-4bcc-9322-110099887766"),
            appointmentDate: new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A90101",
            appointmentStatus: AppointmentStatusType.Approved)
        {
            TenantId = Office,
        };

        return (
            new PartyResolver(
                new AppointmentPatientSnapshotResolver(patients),
                Substitute.For<IRepository<Doctor, Guid>>(),
                Substitute.For<IRepository<State, Guid>>()),
            appointment);
    }
}
