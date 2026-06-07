using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
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

        // 2026-05-13: AppointmentCreateDtoValidator (#181) now fires before the
        // AppService's manual UserFriendlyException check; AbpValidationException
        // wins. Assertion checks the validator-produced ValidationErrors collection.
        var ex = await Should.ThrowAsync<AbpValidationException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.ValidationErrors.ShouldContain(e => e.ErrorMessage != null && e.ErrorMessage.Contains("Patient"));
    }

    [Fact]
    public async Task CreateAsync_WhenIdentityUserIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.IdentityUserId = Guid.Empty;

        // 2026-05-13: AppointmentCreateDtoValidator wins; its WithMessage
        // produces "Booker (IdentityUser) is required."
        var ex = await Should.ThrowAsync<AbpValidationException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.ValidationErrors.ShouldContain(e => e.ErrorMessage != null && e.ErrorMessage.Contains("IdentityUser"));
    }

    [Fact]
    public async Task CreateAsync_WhenAppointmentTypeIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.AppointmentTypeId = Guid.Empty;

        // 2026-05-13: AppointmentCreateDtoValidator wins; message is
        // "Appointment type is required." (lowercase 'type').
        var ex = await Should.ThrowAsync<AbpValidationException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.ValidationErrors.ShouldContain(e => e.ErrorMessage != null && e.ErrorMessage.Contains("Appointment type"));
    }

    [Fact]
    public async Task CreateAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.LocationId = Guid.Empty;

        // 2026-05-13: AppointmentCreateDtoValidator wins.
        var ex = await Should.ThrowAsync<AbpValidationException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.ValidationErrors.ShouldContain(e => e.ErrorMessage != null && e.ErrorMessage.Contains("Location"));
    }

    [Fact]
    public async Task CreateAsync_WhenDoctorAvailabilityIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.DoctorAvailabilityId = Guid.Empty;

        // 2026-05-13: AppointmentCreateDtoValidator wins; message is
        // "Time slot is required." (the user-facing label the validator picked).
        var ex = await Should.ThrowAsync<AbpValidationException>(
            async () => await _appointmentsAppService.CreateAsync(input));

        ex.ValidationErrors.ShouldContain(e => e.ErrorMessage != null && e.ErrorMessage.Contains("Time slot"));
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

    // Permission-gate coverage for Create/Update moved to the deterministic
    // reflection guard in AppointmentsAppServiceAuthorizationTests (the SQLite
    // harness does not seed role->permission grants, so behavioral denial here
    // could only ever be a Skip stub). UpdateAsync is now gated by
    // Appointments.Edit; the gap those stubs tracked is closed.

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
        // Phase 11b (G6 follow-up) -- BookingPolicyValidator now enforces
        // [Today + leadTime, Today + maxTime] on CreateAsync. AppointmentType1
        // ("TEST-IME-Eval") routes to the OTHER 60-day cap. Use Today + 7
        // (>= leadTime 3, <= 60) and stagger across tests to keep slots distinct.
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(7),
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

    // R2 (Phase 9, 2026-05-04): pin that AppointmentCreateDto.IsPatientAlreadyExist
    // round-trips to the persisted Appointment row. Mirrors OLD
    // AppointmentDomain.cs:210, 217 where the dedup outcome lands on the entity
    // at booking time. The Angular booking form populates this from the
    // PatientWithNavigationPropertiesDto.IsExisting flag.

    [Fact]
    public async Task CreateAsync_WhenInputHasIsPatientAlreadyExistTrue_PersistsTrue()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(8),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));
            input.IsPatientAlreadyExist = true;

            var created = await _appointmentsAppService.CreateAsync(input);

            var persisted = await _appointmentRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
            persisted!.IsPatientAlreadyExist.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CreateAsync_WhenInputOmitsIsPatientAlreadyExist_DefaultsToFalseOnEntity()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(9),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));
            // input.IsPatientAlreadyExist intentionally left at default (false)

            var created = await _appointmentsAppService.CreateAsync(input);

            var persisted = await _appointmentRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
            persisted!.IsPatientAlreadyExist.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CreateAsync_GeneratesConfirmationNumberInAFormat()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(10),
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
            scratchDate: DateTime.Today.AddDays(11),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));
        var scratchB = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(12),
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
    public async Task CreateAsync_LeavesSlotInAvailable_UnderCapacityModel()
    {
        // 2026-05-15 (slot rework plan 3): under capacity-aware booking,
        // CreateAsync does NOT mutate the slot's BookingStatusId. The slot
        // stays Available; the active-appointment-count probe drives the
        // bookable predicate. Reserved is now a manual-close override
        // written only by an admin via DoctorAvailabilitiesAppService.
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(13),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));

            await _appointmentsAppService.CreateAsync(input);

            var slotAfter = await _doctorAvailabilityRepository.GetAsync(scratchSlot.Id);
            slotAfter.BookingStatusId.ShouldBe(BookingStatus.Available);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenSlotIsReserved_Throws()
    {
        // 2026-05-15 slot rework (plan 3): Reserved = manually closed by
        // doctor's-admin. Attempting to book a Reserved slot must reject
        // with AppointmentBookingSlotClosed. (Previously this test pinned
        // the Booked-blocks behaviour, which is gone -- Booked is legacy
        // and bookable subject to the capacity gate now.)
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot1 = await _doctorAvailabilityRepository.GetAsync(DoctorAvailabilitiesTestData.Slot1Id);
            slot1.BookingStatusId = BookingStatus.Reserved;
            await _doctorAvailabilityRepository.UpdateAsync(slot1, autoSave: true);

            var input = BuildScratchCreateDto(
                DoctorAvailabilitiesTestData.Slot1Id,
                DoctorAvailabilitiesTestData.Slot1AvailableDate.Date.AddHours(9).AddMinutes(15));
            input.LocationId = LocationsTestData.Location1Id;
            input.AppointmentTypeId = LocationsTestData.AppointmentType1Id;

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateAsync(input));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed);
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
                availableDate: scratchDate,
                fromTime: scratchFromTime,
                toTime: scratchToTime,
                bookingStatusId: BookingStatus.Available);
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
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

    // =====================================================================
    // BUG-042 (T2): attorney name is stored on the master record so a
    // booked attorney who never registered (IdentityUserId == null) still
    // has a persisted First/Last name. Tests the domain managers directly.
    // =====================================================================

    [Fact]
    public async Task ApplicantAttorneyManager_CreateAsync_PersistsFirstAndLastName_WithoutIdentityUser()
    {
        var manager = GetRequiredService<ApplicantAttorneyManager>();
        var repository = GetRequiredService<IApplicantAttorneyRepository>();

        var created = await manager.CreateAsync(
            stateId: null,
            identityUserId: null,
            firmName: "Stone & Associates",
            firmAddress: null,
            phoneNumber: null,
            webAddress: null,
            faxNumber: null,
            street: null,
            city: null,
            zipCode: null,
            email: "aria.synthetic@test.local",
            firstName: "Aria",
            lastName: "Stone");

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var persisted = await repository.GetAsync(created.Id);
            persisted.FirstName.ShouldBe("Aria");
            persisted.LastName.ShouldBe("Stone");
            persisted.IdentityUserId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task DefenseAttorneyManager_CreateAsync_PersistsFirstAndLastName_WithoutIdentityUser()
    {
        var manager = GetRequiredService<DefenseAttorneyManager>();
        var repository = GetRequiredService<IDefenseAttorneyRepository>();

        var created = await manager.CreateAsync(
            stateId: null,
            identityUserId: null,
            firmName: "Shield Defense Group",
            firmAddress: null,
            phoneNumber: null,
            webAddress: null,
            faxNumber: null,
            street: null,
            city: null,
            zipCode: null,
            email: "dana.synthetic@test.local",
            firstName: "Dana",
            lastName: "Defense");

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var persisted = await repository.GetAsync(created.Id);
            persisted.FirstName.ShouldBe("Dana");
            persisted.LastName.ShouldBe("Defense");
            persisted.IdentityUserId.ShouldBeNull();
        }
    }

    // =====================================================================
    // BUG-042 (T3): the appointment attorney getters return the stored
    // (booked) name even when the attorney never registered (no
    // IdentityUser). Previously the getter returned null in that case,
    // leaving the section blank in the view.
    // =====================================================================

    [Fact]
    public async Task GetAppointmentApplicantAttorneyAsync_ReturnsStoredName_WhenAttorneyHasNoIdentityUser()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(28),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var createInput = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));
            var appointment = await _appointmentsAppService.CreateAsync(createInput);

            await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(appointment.Id, new ApplicantAttorneyDetailsDto
            {
                ApplicantAttorneyId = null,
                IdentityUserId = Guid.Empty,
                FirstName = "Aria",
                LastName = "Stone",
                Email = "aria.synthetic@test.local",
                FirmName = "Stone & Associates",
            });

            var result = await _appointmentsAppService.GetAppointmentApplicantAttorneyAsync(appointment.Id);

            result.ShouldNotBeNull();
            result!.FirstName.ShouldBe("Aria");
            result.LastName.ShouldBe("Stone");
            result.FirmName.ShouldBe("Stone & Associates");
            result.Email.ShouldBe("aria.synthetic@test.local");
            result.IdentityUserId.ShouldBe(Guid.Empty);
        }
    }

    [Fact]
    public async Task GetAppointmentDefenseAttorneyAsync_ReturnsStoredName_WhenAttorneyHasNoIdentityUser()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(35),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var createInput = BuildScratchCreateDto(scratchSlot.Id, scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15));
            var appointment = await _appointmentsAppService.CreateAsync(createInput);

            await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(appointment.Id, new DefenseAttorneyDetailsDto
            {
                DefenseAttorneyId = null,
                IdentityUserId = Guid.Empty,
                FirstName = "Dana",
                LastName = "Defense",
                Email = "dana.synthetic@test.local",
                FirmName = "Shield Defense Group",
            });

            var result = await _appointmentsAppService.GetAppointmentDefenseAttorneyAsync(appointment.Id);

            result.ShouldNotBeNull();
            result!.FirstName.ShouldBe("Dana");
            result.LastName.ShouldBe("Defense");
            result.FirmName.ShouldBe("Shield Defense Group");
            result.Email.ShouldBe("dana.synthetic@test.local");
            result.IdentityUserId.ShouldBe(Guid.Empty);
        }
    }

    // =====================================================================
    // BUG-043 (T8): approval-time defense-in-depth. The Pending -> Approved
    // transition is blocked unless the appointment carries at least one
    // Claim Information (injury detail) row. Mirrors the client-side guard
    // (T7) so a direct API approve cannot bypass the requirement. The gate
    // lives in AppointmentManager.ApplyTransitionAsync's Approve branch --
    // the single chokepoint both approve surfaces funnel through.
    // =====================================================================

    [Fact]
    public async Task ApproveAsync_Throws_WhenAppointmentHasNoInjuryDetail()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(42),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));
        var appointment = await InsertPendingAppointmentInTenantAAsync(
            scratchSlot.Id,
            scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15),
            "A-T8-NOINJURY");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var manager = GetRequiredService<AppointmentManager>();

            var ex = await Should.ThrowAsync<BusinessException>(
                () => manager.ApproveAsync(appointment.Id, Guid.NewGuid()));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresInjuryDetail);
        }
    }

    [Fact]
    public async Task ApproveAsync_Succeeds_WhenAppointmentHasInjuryAndClaimExaminer()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(49),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));
        var appointment = await InsertPendingAppointmentInTenantAAsync(
            scratchSlot.Id,
            scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15),
            "A-T8-WITHINJURY");

        // CI1: the inserts below use autoSave; without an ambient UoW the
        // repository DbContext is disposed before manager.ApproveAsync runs.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var injuryRepository = GetRequiredService<IAppointmentInjuryDetailRepository>();
                await injuryRepository.InsertAsync(
                    new AppointmentInjuryDetail(
                        Guid.NewGuid(),
                        appointment.Id,
                        dateOfInjury: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        claimNumber: "CLM-TEST-0001",
                        isCumulativeInjury: false,
                        bodyPartsSummary: "Lower back",
                        wcabAdj: "ADJ-CI3"),
                    autoSave: true);

                // CI1 (2026-06-05): approval also requires an active Claim Examiner.
                var claimExaminerRepository = GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>();
                await claimExaminerRepository.InsertAsync(
                    new AppointmentClaimExaminer(Guid.NewGuid(), appointment.Id, isActive: true)
                    {
                        Name = "Jane Examiner",
                        Email = "ce@gesco.com",
                    },
                    autoSave: true);

                var manager = GetRequiredService<AppointmentManager>();
                var approved = await manager.ApproveAsync(appointment.Id, Guid.NewGuid());

                approved.AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
            }
        });
    }

    [Fact]
    public async Task ApproveAsync_Throws_WhenAppointmentHasNoClaimExaminer()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(63),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));
        var appointment = await InsertPendingAppointmentInTenantAAsync(
            scratchSlot.Id,
            scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15),
            "A-CI1-NOCE");

        // CI1: the insert below uses autoSave; without an ambient UoW the
        // repository DbContext is disposed before manager.ApproveAsync runs.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                // Inject an injury so the injury-detail gate passes and the
                // claim-examiner gate is the one under test.
                var injuryRepository = GetRequiredService<IAppointmentInjuryDetailRepository>();
                await injuryRepository.InsertAsync(
                    new AppointmentInjuryDetail(
                        Guid.NewGuid(),
                        appointment.Id,
                        dateOfInjury: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        claimNumber: "CLM-CI1-0001",
                        isCumulativeInjury: false,
                        bodyPartsSummary: "Lower back",
                        wcabAdj: "ADJ-CI3"),
                    autoSave: true);

                var manager = GetRequiredService<AppointmentManager>();

                var ex = await Should.ThrowAsync<BusinessException>(
                    () => manager.ApproveAsync(appointment.Id, Guid.NewGuid()));

                ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresClaimExaminer);
            }
        });
    }

    // =====================================================================
    // 2026-05-15 -- slot rework plan 3: capacity-aware booking gate.
    // ValidateDoctorAvailabilityForBooking now rejects on Reserved (slot
    // closed), capacity exhausted (active count >= Capacity), and type
    // not in non-empty AppointmentTypes set. Race-to-last-seat test (#8)
    // is deferred per the wave-wide invariant -- SQLite cannot honor the
    // T-SQL row-lock hint.
    // =====================================================================

    [Fact]
    public async Task CreateAsync_WhenSlotIsReserved_ThrowsSlotClosed()
    {
        var date = DateTime.Today.AddDays(7);
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: date,
            scratchFromTime: new TimeOnly(9, 0),
            scratchToTime: new TimeOnly(10, 0));

        // Flip the seeded Available slot to manually-closed Reserved.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var loaded = await _doctorAvailabilityRepository.GetAsync(scratchSlot.Id);
            loaded.BookingStatusId = BookingStatus.Reserved;
            await _doctorAvailabilityRepository.UpdateAsync(loaded, autoSave: true);
        }

        var input = BuildScratchCreateDto(scratchSlot.Id, date.AddHours(9).AddMinutes(15));

        BusinessException ex;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateAsync(input));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed);
    }

    [Fact]
    public async Task CreateAsync_WhenSlotCapacityIsExhausted_ThrowsSlotFull()
    {
        var date = DateTime.Today.AddDays(8);
        var slotId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available,
                capacity: 2);
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);
        }

        await InsertPendingAppointmentInTenantAAsync(slotId, date.AddHours(9).AddMinutes(10), "A-CAP-1");
        await InsertPendingAppointmentInTenantAAsync(slotId, date.AddHours(9).AddMinutes(20), "A-CAP-2");

        var input = BuildScratchCreateDto(slotId, date.AddHours(9).AddMinutes(30));

        BusinessException ex;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateAsync(input));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull);
        ex.Data["capacity"].ShouldBe(2);
        ex.Data["activeCount"].ShouldBe(2L);
    }

    [Fact]
    public async Task CreateAsync_WhenSlotHasFreedAppointments_DoesNotCountThem()
    {
        var date = DateTime.Today.AddDays(9);
        var slotId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available,
                capacity: 1);
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);

            // Rejected appointment -- does NOT count toward active.
            await _appointmentRepository.InsertAsync(new Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: slotId,
                appointmentDate: date.AddHours(9).AddMinutes(10),
                requestConfirmationNumber: "A-FREED-1",
                appointmentStatus: AppointmentStatusType.Rejected), autoSave: true);
        }

        var input = BuildScratchCreateDto(slotId, date.AddHours(9).AddMinutes(30));

        AppointmentDto result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appointmentsAppService.CreateAsync(input);
        }

        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_WhenSlotTypesEmpty_AnyTypeWorks()
    {
        var date = DateTime.Today.AddDays(10);
        var slotId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available);
            // No AddAppointmentType -- empty set = any type accepted.
            await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);
        }

        var input = BuildScratchCreateDto(slotId, date.AddHours(9).AddMinutes(15));
        // input requests AppointmentType1; loose-mode slot accepts any type.

        AppointmentDto result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appointmentsAppService.CreateAsync(input);
        }

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenRequestedTypeNotInSlotTypes_ThrowsTypeMismatch()
    {
        var date = DateTime.Today.AddDays(11);
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: date,
            scratchFromTime: new TimeOnly(9, 0),
            scratchToTime: new TimeOnly(10, 0));
        // Helper adds AppointmentType1. Input requests AppointmentType2.

        var input = BuildScratchCreateDto(scratchSlot.Id, date.AddHours(9).AddMinutes(15));
        input.AppointmentTypeId = AppointmentTypesTestData.AppointmentType2Id;

        BusinessException ex;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateAsync(input));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotTypeMismatch);
    }

    [Fact]
    public async Task CreateAsync_WhenRequestedTypeInSlotTypes_Succeeds()
    {
        var date = DateTime.Today.AddDays(12);
        var slotId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available);
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            slot.AddAppointmentType(AppointmentTypesTestData.AppointmentType2Id);
            await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);
        }

        var input = BuildScratchCreateDto(slotId, date.AddHours(9).AddMinutes(15));
        input.AppointmentTypeId = AppointmentTypesTestData.AppointmentType2Id;

        AppointmentDto result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appointmentsAppService.CreateAsync(input);
        }

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenLeadTimeBlocks_RaisesLeadTimeNotCapacity()
    {
        // Verify lead-time still fires for a non-full slot when the
        // requested date is in the past. Slot is Available + non-full +
        // correct type; the capacity gate passes; BookingPolicyValidator's
        // EnsureAppointmentDateNotInPast then throws.
        var pastDate = DateTime.Today.AddDays(-5);
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: pastDate,
            scratchFromTime: new TimeOnly(9, 0),
            scratchToTime: new TimeOnly(10, 0));

        var input = BuildScratchCreateDto(scratchSlot.Id, pastDate.AddHours(9).AddMinutes(15));

        BusinessException ex;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateAsync(input));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime);
    }

    /// <summary>
    /// Inserts a Pending appointment directly via the repository (bypassing
    /// the AppService CreateAsync, whose internal-caller fast-path stamps
    /// Approved in the always-allow test harness). Mirrors the
    /// <c>new Appointment(...)</c> seeding precedent in
    /// AppointmentApprovalValidatorUnitTests; reuses seeded FK targets so
    /// the SQLite FK constraints are satisfied.
    /// </summary>
    private async Task<Appointment> InsertPendingAppointmentInTenantAAsync(
        Guid doctorAvailabilityId,
        DateTime appointmentDate,
        string requestConfirmationNumber)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var appointment = new Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: doctorAvailabilityId,
                appointmentDate: appointmentDate,
                requestConfirmationNumber: requestConfirmationNumber,
                appointmentStatus: AppointmentStatusType.Pending);
            return await _appointmentRepository.InsertAsync(appointment, autoSave: true);
        }
    }

    // =====================================================================
    // Issue 6 (T6 / 2026-05-27): the appointments list path must load
    // AppointmentInjuryDetails for each row so the external "My Appointments
    // Requests" home grid renders Claim # + Date Of Injury (data already
    // present, list query previously omitted it). Previously, only the
    // single-item GetWithNavigationPropertiesAsync ran the injury loader;
    // the list ran only the base 5-way join. Batched fetch -- one query per
    // sub-table for the whole page (no N+1).
    // =====================================================================

    [Fact]
    public async Task GetListWithNavigationPropertiesAsync_LoadsInjuryDetails_ForEachRow()
    {
        var scratchSlot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: DateTime.Today.AddDays(56),
            scratchFromTime: new TimeOnly(10, 0),
            scratchToTime: new TimeOnly(11, 0));
        var appointment = await InsertPendingAppointmentInTenantAAsync(
            scratchSlot.Id,
            scratchSlot.AvailableDate.Date.AddHours(10).AddMinutes(15),
            "A-T6-LISTINJURY");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var injuryRepository = GetRequiredService<IAppointmentInjuryDetailRepository>();
            await injuryRepository.InsertAsync(
                new AppointmentInjuryDetail(
                    Guid.NewGuid(),
                    appointment.Id,
                    dateOfInjury: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    claimNumber: "CLM-T6-LIST",
                    isCumulativeInjury: false,
                    bodyPartsSummary: "Test",
                    wcabAdj: "ADJ-CI3"),
                autoSave: true);

            var items = await _appointmentRepository.GetListWithNavigationPropertiesAsync(
                appointmentDateMin: scratchSlot.AvailableDate.Date,
                appointmentDateMax: scratchSlot.AvailableDate.Date.AddDays(1));

            var row = items.FirstOrDefault(r => r.Appointment.Id == appointment.Id);
            row.ShouldNotBeNull();
            row!.AppointmentInjuryDetails.Count.ShouldBe(1);
            row.AppointmentInjuryDetails[0].AppointmentInjuryDetail.ClaimNumber.ShouldBe("CLM-T6-LIST");
        }
    }
}
