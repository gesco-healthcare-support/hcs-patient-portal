using System;
using System.Threading;
using System.Threading.Tasks;
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
/// Phase 6 T4 (2026-08-08) -- the patient's address on the outbound payload.
///
/// <para>The load-bearing assertion is the MAPPING, not the plumbing: <c>Patient.Street</c> is
/// street line 1 and <c>Patient.Address</c> is the UNIT number, despite the column names suggesting
/// the reverse. Verified from the booking form, which labels them "Street" and "Unit #". Getting
/// this backwards would publish a bare "4B" as a street address.</para>
///
/// <para>All fixture data is synthetic.</para>
/// </summary>
public class PartyResolverAddressTests
{
    private static readonly Guid PatientId = new("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
    private static readonly Guid TenantId = new("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e");
    private static readonly Guid StateId = new("c3d4e5f6-a7b8-4c9d-8e1f-2a3b4c5d6e7f");

    private static Patient PatientWith(Guid? stateId, string? street, string? address)
        => new(
            id: PatientId,
            stateId: stateId,
            appointmentLanguageId: null,
            identityUserId: null,
            tenantId: TenantId,
            firstName: "Testadora",
            lastName: "Synthetica",
            email: "testadora@example.test",
            genderId: Gender.Female,
            dateOfBirth: new DateTime(1985, 4, 12, 0, 0, 0, DateTimeKind.Utc),
            phoneNumberTypeId: PhoneNumberType.Home,
            address: address,
            city: "Sample City",
            zipCode: "90210",
            street: street);

    private static (PartyResolver Resolver, Appointment Appointment) Build(
        Guid? stateId = null,
        string? street = "1200 Sample Street",
        string? address = "4B",
        string? stateName = "California")
    {
        var patientRepo = Substitute.For<IRepository<Patient, Guid>>();
        patientRepo.FindAsync(PatientId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Patient?>(PatientWith(stateId, street, address)));

        var stateRepo = Substitute.For<IRepository<State, Guid>>();
        if (stateId is { } id && stateName != null)
        {
            stateRepo.FindAsync(id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<State?>(new State(id, stateName)));
        }

        var appointment = new Appointment(
            id: new Guid("d4e5f6a7-b8c9-4d0e-8f1a-2b3c4d5e6f70"),
            patientId: PatientId,
            identityUserId: null,
            appointmentTypeId: new Guid("e5f6a7b8-c9d0-4e1f-8a2b-3c4d5e6f7081"),
            locationId: new Guid("f6a7b8c9-d0e1-4f2a-8b3c-4d5e6f708192"),
            doctorAvailabilityId: new Guid("a7b8c9d0-e1f2-4a3b-8c4d-5e6f70819203"),
            appointmentDate: new DateTime(2027, 5, 6, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00099",
            appointmentStatus: AppointmentStatusType.Approved);

        return (new PartyResolver(patientRepo, Substitute.For<IRepository<Doctor, Guid>>(), stateRepo),
                appointment);
    }

    [Fact]
    public async Task StreetIsLineOne_AndAddressIsTheUnitNumber()
    {
        // THE test this task exists for. Patient.Address is the "Unit #" field on the booking form;
        // Patient.Street is line 1. Swapping them publishes a bare unit number as a street.
        var (resolver, appointment) = Build(street: "1200 Sample Street", address: "4B");

        var section = await resolver.ResolvePatientAsync(appointment);

        section.Street.ShouldBe("1200 Sample Street");
        section.Unit.ShouldBe("4B");
    }

    [Fact]
    public async Task TheUnitNumberIsNeverPublishedAsTheStreet()
    {
        var (resolver, appointment) = Build(street: "1200 Sample Street", address: "4B");

        var section = await resolver.ResolvePatientAsync(appointment);

        section.Street.ShouldNotBe("4B");
    }

    [Fact]
    public async Task TheStateIsSentAsItsNameNotItsId()
    {
        var (resolver, appointment) = Build(stateId: StateId, stateName: "California");

        var section = await resolver.ResolvePatientAsync(appointment);

        section.State.ShouldBe("California");
        section.State.ShouldNotBe(StateId.ToString());
    }

    [Fact]
    public async Task CityAndZipComeStraightAcross()
    {
        var (resolver, appointment) = Build();

        var section = await resolver.ResolvePatientAsync(appointment);

        section.City.ShouldBe("Sample City");
        section.ZipCode.ShouldBe("90210");
    }

    [Fact]
    public async Task AnAbsentAddressPartIsNull_AndTheOthersStillArrive()
    {
        // A patient with no unit is normal, not an error.
        var (resolver, appointment) = Build(street: "1200 Sample Street", address: null);

        var section = await resolver.ResolvePatientAsync(appointment);

        section.Unit.ShouldBeNull();
        section.Street.ShouldBe("1200 Sample Street");
    }

    [Fact]
    public async Task AnUnknownOrAbsentStateYieldsNull_NotAnEmptyStringOrAnId()
    {
        var (resolver, appointment) = Build(stateId: null);

        var section = await resolver.ResolvePatientAsync(appointment);

        section.State.ShouldBeNull();
    }
}
