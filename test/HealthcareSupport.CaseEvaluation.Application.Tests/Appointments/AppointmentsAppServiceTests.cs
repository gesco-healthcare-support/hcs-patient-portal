using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Abstract base class for AppointmentsAppService integration tests.
/// The concrete EfCoreAppointmentsAppServiceTests subclass lives under
/// EntityFrameworkCore.Tests and supplies the TStartupModule that wires in
/// SQLite + full ABP module graph.
///
/// Phase B-6 Tier-1 PR-1A: validation-layer coverage (the original 12 active
/// Facts + 4 Skip-encoded gap markers below).
/// Phase B-6 Wave-2 PR-W2A: happy-path CRUD using seeded Appointment1/2 +
/// Slot1/2/3 + the slot-state intent gap (Available -> Reserved -> Booked).
/// </summary>
public abstract class AppointmentsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentsAppService _appointmentsAppService;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentsAppServiceTests()
    {
        _appointmentsAppService = GetRequiredService<IAppointmentsAppService>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // =====================================================================
    // CreateAsync — Guid.Empty guard clauses (ValidateCreateGuids).
    // Each test flips exactly one FK to Guid.Empty and confirms the
    // UserFriendlyException names that field. Tests that the 5 guards fire
    // in sequence and that the right one throws for each.
    // =====================================================================

    [Fact]
    public async Task CreateAsync_WhenPatientIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.PatientId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.Message.ShouldContain("Patient");
    }

    [Fact]
    public async Task CreateAsync_WhenIdentityUserIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.IdentityUserId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        // ABP's L["..."] localizer inserts a space between CamelCase words, so
        // the localized field label is "Identity User" rather than "IdentityUser".
        ex.Message.ShouldContain("Identity User");
    }

    [Fact]
    public async Task CreateAsync_WhenAppointmentTypeIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.AppointmentTypeId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.Message.ShouldContain("Appointment Type");
    }

    [Fact]
    public async Task CreateAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.LocationId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task CreateAsync_WhenDoctorAvailabilityIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.DoctorAvailabilityId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        // Note: the localized label for `L["DoctorAvailability"]` resolves to
        // "Availability & Time Slots" (the user-facing display name), not a
        // CamelCase-split of the key.
        ex.Message.ShouldContain("Availability & Time Slots");
    }

    // =====================================================================
    // CreateAsync — FK target not found.
    // Only the Patient-not-found branch is testable without seeded upstream
    // entities: the AppService checks Patient first, throws before reaching
    // IdentityUser / AppointmentType / Location / DoctorAvailability lookups.
    // Additional FK-not-found paths land in PR-1B+ once the orchestrator
    // seeds Patient / IdentityUser / Location / AppointmentType entities.
    // =====================================================================

    [Fact]
    public async Task CreateAsync_WhenPatientDoesNotExist_Throws()
    {
        var input = BuildValidCreateDto();

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.Message.ShouldContain("patient");
    }

    // =====================================================================
    // UpdateAsync — Guid.Empty guard clauses.
    // The AppService checks each FK for Guid.Empty BEFORE loading the
    // appointment by id, so we can use any unseeded id and still exercise
    // the five validation branches deterministically.
    // =====================================================================

    [Fact]
    public async Task UpdateAsync_WhenPatientIdIsEmpty_Throws()
    {
        var input = BuildValidUpdateDto();
        input.PatientId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.UpdateAsync(AppointmentsTestData.Appointment1Id, input));

        ex.Message.ShouldContain("Patient");
    }

    [Fact]
    public async Task UpdateAsync_WhenIdentityUserIdIsEmpty_Throws()
    {
        var input = BuildValidUpdateDto();
        input.IdentityUserId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.UpdateAsync(AppointmentsTestData.Appointment1Id, input));

        ex.Message.ShouldContain("Identity User");
    }

    [Fact]
    public async Task UpdateAsync_WhenAppointmentTypeIdIsEmpty_Throws()
    {
        var input = BuildValidUpdateDto();
        input.AppointmentTypeId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.UpdateAsync(AppointmentsTestData.Appointment1Id, input));

        ex.Message.ShouldContain("Appointment Type");
    }

    [Fact]
    public async Task UpdateAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = BuildValidUpdateDto();
        input.LocationId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.UpdateAsync(AppointmentsTestData.Appointment1Id, input));

        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task UpdateAsync_WhenDoctorAvailabilityIdIsEmpty_Throws()
    {
        var input = BuildValidUpdateDto();
        input.DoctorAvailabilityId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appointmentsAppService.UpdateAsync(AppointmentsTestData.Appointment1Id, input));

        // Note: the localized label for `L["DoctorAvailability"]` resolves to
        // "Availability & Time Slots" (the user-facing display name), not a
        // CamelCase-split of the key.
        ex.Message.ShouldContain("Availability & Time Slots");
    }

    // =====================================================================
    // GetListAsync — empty-state coverage (no appointments seeded yet in Tier-1).
    // =====================================================================

    [Fact]
    public async Task GetListAsync_WhenNoAppointmentsSeeded_ReturnsZeroCount()
    {
        var result = await _appointmentsAppService.GetListAsync(new GetAppointmentsInput());

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    // =====================================================================
    // Gap-encoding tests (Skip= with tracking references).
    // Each [Fact(Skip="KNOWN GAP: ...")] documents an intended behaviour that
    // the current code does NOT enforce. When the gap is closed in a future
    // PR, flip Skip to null and the test runs; failure then forces a decision.
    // =====================================================================

    [Fact(Skip = "KNOWN GAP: DeleteAsync does not release DoctorAvailability.BookingStatusId back to Available. Tracked in src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md under 'Business Rules' rule 2.")]
    public Task DeleteAsync_ReleasesSlotBackToAvailable()
    {
        // Expected behaviour (not yet implemented):
        // 1. Seed DoctorAvailability with BookingStatusId=Available
        // 2. Seed Appointment linking to that slot (slot becomes Booked)
        // 3. Call DeleteAsync on the appointment
        // 4. Assert the DoctorAvailability row now has BookingStatusId=Available again
        return Task.CompletedTask;
    }

    [Fact(Skip = "KNOWN GAP: No enforced state-machine on AppointmentStatus. Any code path can set any status directly. Tracked in src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md under 'State Machine' warning.")]
    public Task UpdateAsync_TransitionFromBilledToPending_ShouldThrow()
    {
        // Expected behaviour (not yet implemented):
        // Changing status from a terminal state (Billed=11) back to Pending=1
        // should be rejected by a domain-level transition guard. Current
        // AppointmentManager.UpdateAsync does not touch AppointmentStatus at all,
        // and the entity's setter is public, so any caller can bypass.
        return Task.CompletedTask;
    }

    [Fact(Skip = "KNOWN GAP: CreateAsync only requires [Authorize] (any authenticated user), not [Authorize(CaseEvaluationPermissions.Appointments.Create)]. UI checks the permission but API does not. Tracked in src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md under 'Business Rules' rule 5.")]
    public Task CreateAsync_WithoutAppointmentsCreatePermission_ShouldThrow()
    {
        // Expected behaviour (not yet implemented):
        // Invoking CreateAsync as an authenticated user who does NOT have
        // CaseEvaluation.Appointments.Create should throw AbpAuthorizationException.
        return Task.CompletedTask;
    }

    [Fact(Skip = "KNOWN GAP: UpdateAsync only requires [Authorize]. Mirror of the Create permission gap. Tracked in the same CLAUDE.md section.")]
    public Task UpdateAsync_WithoutAppointmentsEditPermission_ShouldThrow()
    {
        // Expected behaviour (not yet implemented):
        // Invoking UpdateAsync as an authenticated user who does NOT have
        // CaseEvaluation.Appointments.Edit should throw AbpAuthorizationException.
        return Task.CompletedTask;
    }

    // =====================================================================
    // Wave-2 PR-W2A: happy-path CRUD using seeded Appointment1/2 + slot
    // booking + confirmation-number format. Read tests use seeded rows
    // directly. Create tests insert a scratch DoctorAvailability slot first
    // so they don't mutate the shared seed (Slot1/2/3 stay intact across
    // test order).
    // =====================================================================

    [Fact]
    public async Task GetListAsync_FromHostContextWithFilterDisabled_ReturnsBothSeededAppointments()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _appointmentsAppService.GetListAsync(new GetAppointmentsInput());

            result.Items.Any(x => x.Appointment.Id == AppointmentsTestData.Appointment1Id).ShouldBeTrue();
            result.Items.Any(x => x.Appointment.Id == AppointmentsTestData.Appointment2Id).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantAContext_ReturnsOnlyAppointment1()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appointmentsAppService.GetListAsync(new GetAppointmentsInput());

            result.Items.Any(x => x.Appointment.Id == AppointmentsTestData.Appointment1Id).ShouldBeTrue();
            result.Items.Any(x => x.Appointment.Id == AppointmentsTestData.Appointment2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantBContext_ReturnsOnlyAppointment2()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var result = await _appointmentsAppService.GetListAsync(new GetAppointmentsInput());

            result.Items.Any(x => x.Appointment.Id == AppointmentsTestData.Appointment2Id).ShouldBeTrue();
            result.Items.Any(x => x.Appointment.Id == AppointmentsTestData.Appointment1Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsAppointment1_WhenInTenantAContext()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appointmentsAppService.GetAsync(AppointmentsTestData.Appointment1Id);

            result.ShouldNotBeNull();
            result.Id.ShouldBe(AppointmentsTestData.Appointment1Id);
            result.RequestConfirmationNumber.ShouldBe(AppointmentsTestData.Appointment1RequestConfirmationNumber);
            result.AppointmentStatus.ShouldBe(AppointmentsTestData.Appointment1Status);
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ResolvesPatientLocationTypeAndSlot()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appointmentsAppService.GetWithNavigationPropertiesAsync(AppointmentsTestData.Appointment1Id);

            result.ShouldNotBeNull();
            result.Appointment.Id.ShouldBe(AppointmentsTestData.Appointment1Id);
            // Patient is NOT IMultiTenant (FEAT-09) but the FK still resolves.
            result.Patient.ShouldNotBeNull();
            result.Patient!.Id.ShouldBe(PatientsTestData.Patient1Id);
            result.AppointmentType.ShouldNotBeNull();
            result.AppointmentType!.Id.ShouldBe(LocationsTestData.AppointmentType1Id);
            result.Location.ShouldNotBeNull();
            result.Location!.Id.ShouldBe(LocationsTestData.Location1Id);
            result.DoctorAvailability.ShouldNotBeNull();
            result.DoctorAvailability!.Id.ShouldBe(DoctorAvailabilitiesTestData.Slot1Id);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenInputValid_PersistsAppointmentAndReturnsDto()
    {
        // Insert a scratch Available slot in TenantA (don't mutate Slot2 from
        // the shared seed). Then book it via the AppService.
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));

            var created = await _appointmentsAppService.CreateAsync(input);

            created.ShouldNotBeNull();
            created.PatientId.ShouldBe(input.PatientId);
            created.DoctorAvailabilityId.ShouldBe(scratchSlot.Id);
            created.Id.ShouldNotBe(Guid.Empty);

            var persisted = await _appointmentRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task CreateAsync_GeneratesConfirmationNumberInAFormat()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 2, 5, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(30));

            var created = await _appointmentsAppService.CreateAsync(input);

            // Format confirmed mechanically; race window between MAX read and
            // INSERT is documented in src/.../Appointments/CLAUDE.md gotcha 5
            // and is encoded as an inherited skipped Fact, NOT asserted here.
            Regex.IsMatch(created.RequestConfirmationNumber, @"^A\d{5}$").ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CreateAsync_TwoSequentialCreates_ProduceIncreasingNumbers()
    {
        var scratchA = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));
        var scratchB = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var firstInput = BuildScratchCreateDto(scratchA.Id, scratchA.AvailableDate.Date.AddHours(10).AddMinutes(15));
            var first = await _appointmentsAppService.CreateAsync(firstInput);

            var secondInput = BuildScratchCreateDto(scratchB.Id, scratchB.AvailableDate.Date.AddHours(10).AddMinutes(15));
            var second = await _appointmentsAppService.CreateAsync(secondInput);

            int firstNum = int.Parse(first.RequestConfirmationNumber.Substring(1));
            int secondNum = int.Parse(second.RequestConfirmationNumber.Substring(1));
            secondNum.ShouldBeGreaterThan(firstNum);
        }
    }

    [Fact]
    public async Task CreateAsync_FlipsSlotOutOfAvailable_ButNotEnshrineBooked()
    {
        // W2-1 lock: assert the slot's status is no longer Available after
        // booking, but do NOT assert it is Booked. Product intent (per
        // docs/product/doctor-availabilities.md) is Available -> Reserved
        // (pending office review) -> Booked. Current code skips Reserved.
        // The Skip Fact below pins the divergence; this live Fact passes
        // either way the production code resolves it.
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 4, 7, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));

            await _appointmentsAppService.CreateAsync(input);

            var slotAfter = await _doctorAvailabilityRepository.GetAsync(scratchSlot.Id);
            slotAfter.BookingStatusId.ShouldNotBe(BookingStatus.Available);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenSlotIsNotAvailable_Throws()
    {
        // Slot1 is seeded as Booked; attempting to book it again must reject.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(
                DoctorAvailabilitiesTestData.Slot1Id,
                DoctorAvailabilitiesTestData.Slot1AvailableDate.Date.AddHours(9).AddMinutes(15));
            input.LocationId = LocationsTestData.Location1Id;
            input.AppointmentTypeId = LocationsTestData.AppointmentType1Id;

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _appointmentsAppService.CreateAsync(input));

            ex.Message.ShouldContain("no longer available");
        }
    }

    [Fact]
    public async Task CreateAsync_WhenSlotLocationMismatch_Throws()
    {
        // Scratch slot at Location2; input asks for Location1.
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 5, 5, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0),
            locationId: LocationsTestData.Location2Id);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));
            input.LocationId = LocationsTestData.Location1Id;

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _appointmentsAppService.CreateAsync(input));

            ex.Message.ShouldContain("location");
        }
    }

    [Fact]
    public async Task CreateAsync_WhenSlotDateMismatch_Throws()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, new DateTime(2027, 6, 8, 10, 15, 0, DateTimeKind.Utc));

            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _appointmentsAppService.CreateAsync(input));

            ex.Message.ShouldContain("date");
        }
    }

    [Fact(Skip = "KNOWN GAP: AppointmentsAppService.CreateAsync should transition slot Available -> Reserved (pending office review) -> Booked, but currently flips directly to Booked. Tracked: docs/product/doctor-availabilities.md slot-lifecycle section AND src/.../Domain/Appointments/CLAUDE.md Business Rule 4 (slot booking is one-way). When production code is fixed to emit Reserved as the post-create state, this Fact flips live.")]
    public Task CreateAsync_BookingTransitionsSlotToReserved_NotBookedDirectly()
    {
        // Expected behaviour (not yet implemented):
        // After CreateAsync, the slot's BookingStatusId should be `Reserved`,
        // representing "pending office review" per the product intent doc.
        // Current code sets it to `Booked` immediately, which conflates the
        // pending-review and confirmed-booking states.
        return Task.CompletedTask;
    }

    // =====================================================================
    // Helpers.
    // =====================================================================

    private static AppointmentCreateDto BuildValidCreateDto()
    {
        return new AppointmentCreateDto
        {
            PatientId = AppointmentsTestData.NonExistentPatientId,
            IdentityUserId = AppointmentsTestData.NonExistentIdentityUserId,
            AppointmentTypeId = AppointmentsTestData.NonExistentAppointmentTypeId,
            LocationId = AppointmentsTestData.NonExistentLocationId,
            DoctorAvailabilityId = AppointmentsTestData.NonExistentDoctorAvailabilityId,
            AppointmentDate = new DateTime(2030, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            RequestConfirmationNumber = "A00001",
            AppointmentStatus = AppointmentStatusType.Pending,
            PanelNumber = null,
            DueDate = null,
        };
    }

    private static AppointmentUpdateDto BuildValidUpdateDto()
    {
        return new AppointmentUpdateDto
        {
            PatientId = AppointmentsTestData.NonExistentPatientId,
            IdentityUserId = AppointmentsTestData.NonExistentIdentityUserId,
            AppointmentTypeId = AppointmentsTestData.NonExistentAppointmentTypeId,
            LocationId = AppointmentsTestData.NonExistentLocationId,
            DoctorAvailabilityId = AppointmentsTestData.NonExistentDoctorAvailabilityId,
            AppointmentDate = new DateTime(2030, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            RequestConfirmationNumber = "A00001",
            PanelNumber = null,
            DueDate = null,
            ConcurrencyStamp = string.Empty,
        };
    }

    /// <summary>
    /// Inserts an Available scratch DoctorAvailability slot in TenantA.
    /// Tests that need to exercise the booking flow without mutating the
    /// shared seed (Slot1/2/3) call this to get a fresh, unique slot they
    /// can flip to Booked without affecting other tests.
    /// </summary>
    private async Task<DoctorAvailability> CreateScratchAvailableSlotInTenantAAsync(
        DateTime scratchDate,
        TimeOnly scratchFromTime,
        TimeOnly scratchToTime,
        Guid? locationId = null)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: Guid.NewGuid(),
                locationId: locationId ?? LocationsTestData.Location1Id,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                availableDate: scratchDate,
                fromTime: scratchFromTime,
                toTime: scratchToTime,
                bookingStatusId: BookingStatus.Available);
            return await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);
        }
    }

    /// <summary>
    /// Builds a TenantA-scoped CreateDto pre-populated with seeded FK targets
    /// (Patient1, Patient1's IdentityUser, AppointmentType1, Location1) plus
    /// the caller-supplied scratch slot id and AppointmentDate. Tests override
    /// individual fields to drive specific validation paths.
    /// </summary>
    private static AppointmentCreateDto BuildScratchCreateDto(Guid scratchSlotId, DateTime appointmentDate)
    {
        return new AppointmentCreateDto
        {
            PatientId = PatientsTestData.Patient1Id,
            IdentityUserId = IdentityUsersTestData.Patient1UserId,
            AppointmentTypeId = LocationsTestData.AppointmentType1Id,
            LocationId = LocationsTestData.Location1Id,
            DoctorAvailabilityId = scratchSlotId,
            AppointmentDate = appointmentDate,
            RequestConfirmationNumber = "ignored-by-server",
            AppointmentStatus = AppointmentStatusType.Pending,
            PanelNumber = null,
            DueDate = null,
        };
    }
}
