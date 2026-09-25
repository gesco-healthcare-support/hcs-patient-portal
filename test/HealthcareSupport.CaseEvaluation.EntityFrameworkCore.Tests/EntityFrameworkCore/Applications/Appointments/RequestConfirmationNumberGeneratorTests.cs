using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// EF-backed on purpose: the behaviour under test IS an ABP query filter, so a mocked repository
/// would prove nothing, and this half of the 2026-08-19 booking outage is genuinely reproducible
/// here.
///
/// <para>CORRECTED 2026-09-15. This previously claimed a filtered unique index "silently no-ops on
/// the SQLite test runner". That is FALSE. The rig builds its schema from the model, so
/// <c>HasFilter</c> reaches SQLite and a filtered unique index enforces there like any other. The
/// evidence is the slot-identity index on <c>DoctorAvailability</c>, which failed 17 tests across
/// this suite with "SQLite Error 19: UNIQUE constraint failed" -- two of them in THIS class,
/// because the seeder below hardcoded a slot's whole identity and varied only its Id.</para>
///
/// <para>Every test allocates its own throwaway office id rather than using
/// <c>TenantsTestData.TenantARef</c>. The collection shares one seeded SQLite database and rows
/// accumulate across test classes -- a sibling test already seeds A99001-A99006 into TenantA --
/// so asserting an exact number in a shared office would depend on class execution order.</para>
/// </summary>
public class RequestConfirmationNumberGeneratorTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly RequestConfirmationNumberGenerator _generator;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDoctorAvailabilityRepository _slotRepository;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// Moves each seeded slot onto its own calendar day. A slot's identity is
    /// (TenantId, LocationId, AvailableDate, FromTime, ToTime); <see cref="SeedAppointmentAsync"/>
    /// used to hardcode all five and vary only the Guid, so the two tests that seed twice into one
    /// office were writing the SAME slot twice. Harmless until the slot-identity index existed.
    /// </summary>
    private static int _slotCounter;

    public RequestConfirmationNumberGeneratorTests()
    {
        _generator = GetRequiredService<RequestConfirmationNumberGenerator>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _slotRepository = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GenerateAsync_AllocatesAboveASoftDeletedHighestNumber()
    {
        // The 2026-08-19 outage, as a test: deleting the highest-numbered appointment took the
        // office offline for booking. The number must still be treated as spent, so the next
        // allocation clears it -- A00043, not A00008 (the live row) and not A00001.
        var officeId = Guid.NewGuid();
        await SeedAppointmentAsync(officeId, "A00007");
        var deletedAppointmentId = await SeedAppointmentAsync(officeId, "A00042");

        await SoftDeleteAppointmentAsync(officeId, deletedAppointmentId);

        (await GenerateForOfficeAsync(officeId)).ShouldBe("A00043");
    }

    [Fact]
    public async Task GenerateAsync_NumbersEachOfficeIndependently()
    {
        // The IMultiTenant filter stays enabled inside the soft-delete scope. If it were
        // disabled too, the busiest office would drag every other office's numbering up.
        var busyOfficeId = Guid.NewGuid();
        var quietOfficeId = Guid.NewGuid();
        await SeedAppointmentAsync(busyOfficeId, "A00500");

        (await GenerateForOfficeAsync(quietOfficeId)).ShouldBe("A00001");
        (await GenerateForOfficeAsync(busyOfficeId)).ShouldBe("A00501");
    }

    [Fact]
    public async Task GenerateAsync_StartsAtOneForAnOfficeWithNoAppointments()
    {
        (await GenerateForOfficeAsync(Guid.NewGuid())).ShouldBe("A00001");
    }

    [Fact]
    public async Task GenerateAsync_IgnoresNumbersOutsideThePrefixAndWidth()
    {
        // Guards the existing prefix/width Where clause. Disabling the soft-delete filter widens
        // the rows this query sees, so a malformed legacy value must still not skew the max.
        var officeId = Guid.NewGuid();
        await SeedAppointmentAsync(officeId, "B00042");
        await SeedAppointmentAsync(officeId, "A004");

        (await GenerateForOfficeAsync(officeId)).ShouldBe("A00001");
    }

    /// <summary>
    /// Inserts one appointment carrying <paramref name="confirmationNumber"/> into
    /// <paramref name="officeId"/>, with its own scratch slot on its own calendar day. Returns the
    /// new appointment's id.
    ///
    /// <para>The day must differ per call, not just the Guid: repeated calls land in the SAME
    /// office, and a slot is identified by its tenant, location, date and times.</para>
    /// </summary>
    private async Task<Guid> SeedAppointmentAsync(Guid officeId, string confirmationNumber)
    {
        var appointmentId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var slotDay = new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(Interlocked.Increment(ref _slotCounter));

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await _slotRepository.InsertAsync(new DoctorAvailability(
                    id: slotId,
                    locationId: LocationsTestData.Location1Id,
                    availableDate: slotDay,
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available), autoSave: true);

                await _appointmentRepository.InsertAsync(new Appointment(
                    id: appointmentId,
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.Patient1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: slotId,
                    appointmentDate: slotDay.AddHours(9).AddMinutes(15),
                    requestConfirmationNumber: confirmationNumber,
                    appointmentStatus: AppointmentStatusType.Approved), autoSave: true);
            }
        });

        return appointmentId;
    }

    private async Task SoftDeleteAppointmentAsync(Guid officeId, Guid appointmentId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await _appointmentRepository.DeleteAsync(appointmentId, autoSave: true);
            }
        });
    }

    private async Task<string> GenerateForOfficeAsync(Guid officeId)
    {
        var generated = string.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                generated = await _generator.GenerateAsync();
            }
        });

        return generated;
    }
}
