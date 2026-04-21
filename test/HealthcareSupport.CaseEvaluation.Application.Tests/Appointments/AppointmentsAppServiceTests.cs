using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Abstract base class for AppointmentsAppService integration tests.
/// The concrete EfCoreAppointmentsAppServiceTests subclass lives under
/// EntityFrameworkCore.Tests and supplies the TStartupModule that wires in
/// SQLite + full ABP module graph.
///
/// Phase B-6 Tier-1 PR-1A scope: validation-layer coverage only.
/// Happy-path CRUD and nav-property tests are deferred to PR-1B (slot seed),
/// PR-1C (patient seed), and the eventual IdentityUsers seed contributor.
/// </summary>
public abstract class AppointmentsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentsAppService _appointmentsAppService;

    protected AppointmentsAppServiceTests()
    {
        _appointmentsAppService = GetRequiredService<IAppointmentsAppService>();
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
}
