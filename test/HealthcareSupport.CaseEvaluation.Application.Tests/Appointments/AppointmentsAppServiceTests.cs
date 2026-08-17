using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
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
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected AppointmentsAppServiceTests()
    {
        _appointmentsAppService = GetRequiredService<IAppointmentsAppService>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
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
    // Prompt 10 (2026-06-14) -- GetStatusCountsAsync (chip counts) + the
    // multi-status list filter that backs the pill chips. Each test isolates
    // its scratch rows from the shared seed via a unique appointment date range.
    // =====================================================================

    [Fact]
    public async Task GetStatusCountsAsync_ReturnsPerStatusTotals_HonoringDateFilter()
    {
        var date = DateTime.Today.AddDays(70);
        var slotId = await InsertScratchSlotForStatusCountsAsync(date);

        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(5), "A-CNT-P1", AppointmentStatusType.Pending);
        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(10), "A-CNT-P2", AppointmentStatusType.Pending);
        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(15), "A-CNT-A1", AppointmentStatusType.Approved);
        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(20), "A-CNT-C1", AppointmentStatusType.CancelledLate);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var counts = await _appointmentsAppService.GetStatusCountsAsync(new GetAppointmentsInput
            {
                AppointmentDateMin = date,
                AppointmentDateMax = date.AddDays(1),
            });

            CountOf(counts, AppointmentStatusType.Pending).ShouldBe(2);
            CountOf(counts, AppointmentStatusType.Approved).ShouldBe(1);
            CountOf(counts, AppointmentStatusType.CancelledLate).ShouldBe(1);
            CountOf(counts, AppointmentStatusType.Rejected).ShouldBe(0);
        }
    }

    [Fact]
    public async Task GetStatusCountsAsync_IgnoresStatusFilter_SoChipsStayIndependent()
    {
        var date = DateTime.Today.AddDays(77);
        var slotId = await InsertScratchSlotForStatusCountsAsync(date);

        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(5), "A-IND-P1", AppointmentStatusType.Pending);
        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(10), "A-IND-A1", AppointmentStatusType.Approved);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Selecting the Approved chip (status filter) must NOT zero the Pending count.
            var counts = await _appointmentsAppService.GetStatusCountsAsync(new GetAppointmentsInput
            {
                AppointmentDateMin = date,
                AppointmentDateMax = date.AddDays(1),
                AppointmentStatus = AppointmentStatusType.Approved,
                AppointmentStatuses = new List<AppointmentStatusType> { AppointmentStatusType.Approved },
            });

            CountOf(counts, AppointmentStatusType.Pending).ShouldBe(1);
            CountOf(counts, AppointmentStatusType.Approved).ShouldBe(1);
        }
    }

    [Fact]
    public async Task GetListAsync_WithAppointmentStatuses_ReturnsOnlyThoseStatuses()
    {
        var date = DateTime.Today.AddDays(84);
        var slotId = await InsertScratchSlotForStatusCountsAsync(date);

        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(5), "A-MS-P", AppointmentStatusType.Pending);
        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(10), "A-MS-C5", AppointmentStatusType.CancelledNoBill);
        await InsertAppointmentWithStatusAsync(slotId, date.AddHours(9).AddMinutes(15), "A-MS-C6", AppointmentStatusType.CancelledLate);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // The "Cancelled" pill spans several raw statuses -> sent as a set.
            var result = await _appointmentsAppService.GetListAsync(new GetAppointmentsInput
            {
                AppointmentDateMin = date,
                AppointmentDateMax = date.AddDays(1),
                AppointmentStatuses = new List<AppointmentStatusType>
                {
                    AppointmentStatusType.CancelledNoBill,
                    AppointmentStatusType.CancelledLate,
                },
            });

            result.Items.ShouldContain(x => x.Appointment.RequestConfirmationNumber == "A-MS-C5");
            result.Items.ShouldContain(x => x.Appointment.RequestConfirmationNumber == "A-MS-C6");
            result.Items.ShouldNotContain(x => x.Appointment.RequestConfirmationNumber == "A-MS-P");
        }
    }

    private static int CountOf(List<AppointmentStatusCountDto> counts, AppointmentStatusType status)
    {
        return counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;
    }

    private async Task<Guid> InsertScratchSlotForStatusCountsAsync(DateTime date)
    {
        var slot = await CreateScratchAvailableSlotInTenantAAsync(
            scratchDate: date,
            scratchFromTime: new TimeOnly(9, 0),
            scratchToTime: new TimeOnly(17, 0));
        return slot.Id;
    }

    private async Task InsertAppointmentWithStatusAsync(
        Guid doctorAvailabilityId,
        DateTime appointmentDate,
        string requestConfirmationNumber,
        AppointmentStatusType status)
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
                appointmentStatus: status);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);
        }
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

    // R2 (Phase 9, 2026-05-04): pin that AppointmentCreateDto.IsPatientAlreadyExist
    // round-trips to the persisted Appointment row. Mirrors OLD
    // AppointmentDomain.cs:210, 217 where the dedup outcome lands on the entity
    // at booking time. The Angular booking form populates this from the
    // PatientWithNavigationPropertiesDto.IsExisting flag.

    // F-M05 (2026-06-25): a re-evaluation child must link back to its source
    // appointment via OriginalAppointmentId (reschedule children already do).
    // Before the fix the reval child's OriginalAppointmentId stayed NULL, so a
    // re-evaluation was untraceable to the appointment it follows up.
    // Skipped on the epic for the same reason as the sibling create-flow tests:
    // db-per-office makes catalogs IMultiTenant per office and the shared-SQLite
    // test rig can't seed per-tenant catalogs (Phase F harness restore).
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
    // T5 (2026-08-14) -- caller-linkage on the confirmation-number create
    // flows. Confirmation numbers are sequential (A00005, A00036, A00065) and
    // therefore guessable. Before this gate, ReSubmitAsync and CreateRevalAsync
    // validated the source's STATUS only: the accessor/creator check lived
    // solely in GetByConfirmationNumberAsync, the read the UI happens to call
    // first, so a caller who skipped the UI could create against a stranger's
    // appointment.
    //
    // These assert on EntityNotFoundException's EntityType + Id rather than the
    // bare type, because an unseeded catalog FK also raises
    // EntityNotFoundException further down the create path -- asserting the type
    // alone would pass for the wrong reason.
    // =====================================================================

    private static readonly Guid UnlinkedCallerUserId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd10");

    [Fact]
    public async Task ReSubmitAsync_WhenCallerIsNotLinkedToSource_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _currentPrincipalAccessor,
                   UnlinkedCallerUserId,
                   IdentityUsersTestData.PatientRoleName))
        {
            var ex = await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _appointmentsAppService.ReSubmitAsync(
                    AppointmentsTestData.Appointment1RequestConfirmationNumber,
                    BuildValidCreateDto()));

            ex.EntityType.ShouldBe(typeof(Appointment));
            ex.Id.ShouldBe(AppointmentsTestData.Appointment1RequestConfirmationNumber);
        }
    }

    // Appointment2 / A90002 is seeded in TENANT B (Approved, the only
    // reval-eligible status). Running these in TenantA would make the lookup
    // miss and raise EntityNotFoundException(Appointment, "A90002") from the
    // manager's not-found branch -- indistinguishable from the gate's refusal,
    // so the test would pass without the gate existing at all.
    [Fact]
    public async Task CreateRevalAsync_WhenCallerIsNotLinkedToSource_IsRefused()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        using (WithCurrentUser.Run(
                   _currentPrincipalAccessor,
                   UnlinkedCallerUserId,
                   IdentityUsersTestData.PatientRoleName))
        {
            var ex = await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _appointmentsAppService.CreateRevalAsync(
                    AppointmentsTestData.Appointment2RequestConfirmationNumber,
                    BuildValidCreateDto()));

            ex.EntityType.ShouldBe(typeof(Appointment));
            ex.Id.ShouldBe(AppointmentsTestData.Appointment2RequestConfirmationNumber);
        }
    }

    [Fact]
    public async Task CreateRevalAsync_UnknownAndUnlinkedSources_AreIndistinguishable()
    {
        // The refusal must not reveal whether the confirmation number exists,
        // otherwise the endpoint is an oracle for enumerating them.
        const string unknownConfirmationNumber = "A99999";

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        using (WithCurrentUser.Run(
                   _currentPrincipalAccessor,
                   UnlinkedCallerUserId,
                   IdentityUsersTestData.PatientRoleName))
        {
            var unlinked = await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _appointmentsAppService.CreateRevalAsync(
                    AppointmentsTestData.Appointment2RequestConfirmationNumber,
                    BuildValidCreateDto()));

            var unknown = await Should.ThrowAsync<EntityNotFoundException>(
                async () => await _appointmentsAppService.CreateRevalAsync(
                    unknownConfirmationNumber,
                    BuildValidCreateDto()));

            unlinked.GetType().ShouldBe(unknown.GetType());
            unlinked.EntityType.ShouldBe(unknown.EntityType);

            // A90002 is seeded Approved; the refusal must not disclose that.
            unlinked.Message.ShouldNotContain(
                AppointmentsTestData.Appointment2Status.ToString());
        }
    }

    [Fact]
    public async Task ReSubmitAsync_WhenCallerIsInternal_StillReachesTheStatusGate()
    {
        // Regression guard against over-tightening: the default fake principal is
        // the internal "admin" role, so linkage passes and the STATUS gate is what
        // refuses -- Appointment1 is seeded Pending, not Rejected.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.ReSubmitAsync(
                    AppointmentsTestData.Appointment1RequestConfirmationNumber,
                    BuildValidCreateDto()));

            ex.Code.ShouldBe(
                CaseEvaluationDomainErrorCodes.AppointmentReSubmitSourceNotRejected);
        }
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

    // =====================================================================
    // Item 4 (2026-08-17) -- the re-book gates. Both refusals run BEFORE any
    // create work, so they are reachable in this harness even though the
    // success path is not (the SQLite rig cannot seed per-tenant catalogs).
    // =====================================================================

    [Fact]
    public async Task CreateReBookAsync_WhenSourceDidHappen_IsRefused()
    {
        // Appointment1 is seeded Pending: it is still expected to take place, so there is
        // nothing to replace. Re-booking it would strand a live appointment.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateReBookAsync(
                    AppointmentsTestData.Appointment1RequestConfirmationNumber,
                    BuildValidCreateDto()));

            // The default fake principal is the internal "admin" role, so the staff-facing
            // variant is expected -- but it is still a refusal, not an override.
            ex.Code.ShouldBe(
                CaseEvaluationDomainErrorCodes.AppointmentReBookSourceNotEligibleStaffHint);
        }
    }

    [Fact]
    public async Task CreateReBookAsync_WhenSourceWasAlreadyReBooked_IsRefused()
    {
        // Guards an invariant the Case Tracker payload depends on: it finds the successor by
        // querying for appointments pointing back at this one and takes the first match, on
        // the documented assumption that at most one exists. Two would make that choice
        // arbitrary AND unstable between pushes.
        var sourceId = new Guid("e1a2b3c4-d5e6-4f70-8a1b-2c3d4e5f6a70");
        var existingReBookId = new Guid("e1a2b3c4-d5e6-4f70-8a1b-2c3d4e5f6a71");
        const string sourceConfirmation = "A90777";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentRepository.InsertAsync(
                new Appointment(
                    id: sourceId,
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.Patient1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                    appointmentDate: new DateTime(2027, 3, 1, 9, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: sourceConfirmation,
                    appointmentStatus: AppointmentStatusType.NoShow),
                autoSave: true);

            var alreadyReBooked = new Appointment(
                id: existingReBookId,
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2027, 4, 1, 9, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: "A90778",
                appointmentStatus: AppointmentStatusType.Pending)
            {
                RescheduledFromAppointmentId = sourceId,
            };
            await _appointmentRepository.InsertAsync(alreadyReBooked, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _appointmentsAppService.CreateReBookAsync(
                    sourceConfirmation,
                    BuildValidCreateDto()));

            ex.Code.ShouldBe(
                CaseEvaluationDomainErrorCodes.AppointmentReBookSourceAlreadyReBooked);
        }
    }
}
