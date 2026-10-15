using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Timing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// The host intake operator's office assignments, on real per-office databases: assigning and
/// unassigning (with the operator's shadow user in each office), the admin lists and pickers, the
/// operator's own offices and per-office metrics, and who is behind an impersonation.
/// </summary>
/// <remarks>
/// <para>
/// Production isolates offices by DATABASE, and this service crosses offices on purpose. This
/// harness gives each office its own SQLite database, so a count taken on the wrong connection
/// fails here where a single-database tenant filter would hide it. The real shadow-user
/// provisioner runs, and its user is read back from the office's own database.
/// </para>
/// <para>
/// The two offices and the host database are shared by every MultiOffice test in the process and
/// never roll back. So every user carries a per-test token, list assertions are scoped to this
/// test's own operators, and the per-office metrics are asserted as a CHANGE from a baseline taken
/// in the same test rather than as absolute counts other tests also move.
/// </para>
/// <para>All names and emails below are synthetic.</para>
/// </remarks>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeIntakeAssignmentsAppServiceTests : CaseEvaluationMultiOfficeTestBase
{
    private const string IntakeStaff = InternalUserRoleDataSeedContributor.IntakeStaffRoleName;

    private readonly IIntakeAssignmentsAppService _service;
    private readonly IdentityUserManager _users;
    private readonly InternalUserRoleDataSeedContributor _roleSeeder;
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignments;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequests;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    public MultiOfficeIntakeAssignmentsAppServiceTests()
    {
        _service = GetRequiredService<IIntakeAssignmentsAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _roleSeeder = GetRequiredService<InternalUserRoleDataSeedContributor>();
        _assignments = GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _changeRequests = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    // ------------------------------------------------------------------ harness

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>The seeded offices, with the internal roles present in the host and in each office.</summary>
    private async Task<(SeededOffice A, SeededOffice B)> OfficesAsync()
    {
        var offices = await GetSeededOfficesAsync();
        foreach (var officeId in new Guid?[] { null, offices.A.OfficeId, offices.B.OfficeId })
        {
            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(officeId))
                {
                    await _roleSeeder.SeedAsync(new DataSeedContext(officeId));
                }
            }, requiresNew: true);
        }

        return offices;
    }

    /// <summary>A host user named "{first} {token}", in the Intake Staff role when asked.</summary>
    private Task<IdentityUser> HostUserAsync(string first, string token, bool intake) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var email = $"{first.ToLowerInvariant()}.{token}@example.test";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null) { Name = first, Surname = token };
                (await _users.CreateAsync(user, "Synthetic-Passw0rd!")).Succeeded.ShouldBeTrue();
                if (intake)
                {
                    (await _users.AddToRoleAsync(user, IntakeStaff)).Succeeded.ShouldBeTrue();
                }

                return user;
            }
        }, requiresNew: true);

    private Task AssignAsync(Guid operatorId, Guid officeId) =>
        _service.AssignAsync(new AssignIntakeOfficeDto { OperatorUserId = operatorId, OfficeId = officeId });

    private Task<List<IntakeOfficeAssignment>> AssignmentsOfAsync(Guid operatorId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await _assignments.GetListAsync(a => a.OperatorUserId == operatorId);
            }
        }, requiresNew: true);

    /// <summary>The operator's shadow user as the office's own database holds it, or null.</summary>
    private Task<(IdentityUser? User, bool InIntakeRole)> ShadowAsync(Guid officeId, string email) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var shadow = await _users.FindByEmailAsync(email);
                return (shadow, shadow != null && await _users.IsInRoleAsync(shadow, IntakeStaff));
            }
        }, requiresNew: true);

    private async Task<T> As<T>(Guid userId, Func<Task<T>> call, Guid? impersonator = null)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        if (impersonator.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.ImpersonatorUserId, impersonator.Value.ToString()));
        }

        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))))
        {
            return await call();
        }
    }

    private Task<Dictionary<Guid, IntakeOfficeMetricsDto>> MetricsAsync(Guid operatorId) =>
        As(operatorId, async () => (await _service.GetMyOfficeMetricsAsync()).Items.ToDictionary(m => m.OfficeId));

    // ------------------------------------------------------------------ assigning

    [Fact]
    public async Task Assigning_records_the_office_once_and_provisions_an_active_shadow_user_in_that_office_only()
    {
        var (a, b) = await OfficesAsync();
        var op = await HostUserAsync("Ivy", NewToken(), intake: true);

        await AssignAsync(op.Id, a.OfficeId);
        await AssignAsync(op.Id, a.OfficeId);

        (await AssignmentsOfAsync(op.Id)).ShouldHaveSingleItem().OfficeId.ShouldBe(a.OfficeId);
        var (shadow, inRole) = await ShadowAsync(a.OfficeId, op.Email!);
        shadow.ShouldNotBeNull();
        shadow.TenantId.ShouldBe(a.OfficeId);
        shadow.IsActive.ShouldBeTrue();
        inRole.ShouldBeTrue();
        (await ShadowAsync(b.OfficeId, op.Email!)).User.ShouldBeNull();
    }

    [Fact]
    public async Task Only_an_existing_host_intake_operator_can_be_assigned_to_an_existing_office()
    {
        var (a, _) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync("Ivy", token, intake: true);
        var notIntake = await HostUserAsync("Rae", token, intake: false);

        (await Should.ThrowAsync<BusinessException>(() => AssignAsync(Guid.NewGuid(), a.OfficeId)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserNotFound);
        (await Should.ThrowAsync<BusinessException>(() => AssignAsync(notIntake.Id, a.OfficeId)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserInvalidRole);
        await Should.ThrowAsync<EntityNotFoundException>(() => AssignAsync(op.Id, Guid.NewGuid()));

        (await AssignmentsOfAsync(op.Id)).ShouldBeEmpty();
        (await AssignmentsOfAsync(notIntake.Id)).ShouldBeEmpty();
        (await ShadowAsync(a.OfficeId, op.Email!)).User.ShouldBeNull();
        (await ShadowAsync(a.OfficeId, notIntake.Email!)).User.ShouldBeNull();
    }

    [Fact]
    public async Task Unassigning_removes_the_office_and_disables_the_shadow_user_and_reassigning_restores_it()
    {
        var (a, b) = await OfficesAsync();
        var op = await HostUserAsync("Ivy", NewToken(), intake: true);
        await AssignAsync(op.Id, a.OfficeId);

        await _service.UnassignAsync(op.Id, a.OfficeId);
        await _service.UnassignAsync(op.Id, b.OfficeId);

        (await AssignmentsOfAsync(op.Id)).ShouldBeEmpty();
        (await ShadowAsync(a.OfficeId, op.Email!)).User!.IsActive.ShouldBeFalse();
        (await ShadowAsync(b.OfficeId, op.Email!)).User.ShouldBeNull();

        await AssignAsync(op.Id, a.OfficeId);

        (await ShadowAsync(a.OfficeId, op.Email!)).User!.IsActive.ShouldBeTrue();
    }

    // ------------------------------------------------------------------ admin lists

    [Fact]
    public async Task The_assignment_lists_name_the_operator_and_the_office()
    {
        var (a, b) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync("Ivy", token, intake: true);
        await AssignAsync(op.Id, a.OfficeId);
        await AssignAsync(op.Id, b.OfficeId);

        var list = (await _service.GetListAsync()).Items.Where(r => r.OperatorUserId == op.Id).ToList();
        var paged = await _service.GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = token, MaxResultCount = 10 });

        foreach (var rows in new[] { list, paged.Items.ToList() })
        {
            rows.Select(r => r.OfficeName).ShouldBe(new[] { OfficeAName, OfficeBName });
            rows.ShouldAllBe(r => r.OperatorName == $"Ivy {token}" && r.OperatorEmail == op.Email);
        }

        paged.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task The_paged_list_filters_sorts_and_pages()
    {
        var (a, b) = await OfficesAsync();
        var token = NewToken();
        var ivy = await HostUserAsync("Ivy", token, intake: true);
        var ada = await HostUserAsync("Ada", token, intake: true);
        await AssignAsync(ivy.Id, a.OfficeId);
        await AssignAsync(ada.Id, b.OfficeId);

        Task<Volo.Abp.Application.Dtos.PagedResultDto<IntakeOfficeAssignmentDto>> Page(string? sorting, int skip = 0, int take = 10) =>
            _service.GetPagedListAsync(new GetIntakeAssignmentsInput { Filter = $" {token} ", Sorting = sorting, SkipCount = skip, MaxResultCount = take });

        (await Page("operatorEmail desc")).Items.Select(r => r.OperatorEmail).ShouldBe(new[] { ivy.Email, ada.Email });
        (await Page("office desc")).Items.Select(r => r.OfficeName).ShouldBe(new[] { OfficeBName, OfficeAName });
        (await Page("operatorName DESC")).Items.Select(r => r.OperatorName).ShouldBe(new[] { $"Ivy {token}", $"Ada {token}" });
        var second = await Page(null, skip: 1, take: 1);
        second.TotalCount.ShouldBe(2);
        second.Items.ShouldHaveSingleItem().OperatorName.ShouldBe($"Ivy {token}");
    }

    [Fact]
    public async Task The_pickers_offer_host_intake_operators_and_every_office()
    {
        var (a, b) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync("Ivy", token, intake: true);
        var notIntake = await HostUserAsync("Rae", token, intake: false);

        var operators = await _service.GetAssignableOperatorsAsync();
        var offices = await _service.GetOfficeOptionsAsync();

        operators.Items.Single(o => o.Id == op.Id).DisplayName.ShouldBe($"Ivy {token} ({op.Email})");
        operators.Items.ShouldNotContain(o => o.Id == notIntake.Id);
        offices.Items.Select(o => o.Id).ShouldContain(a.OfficeId);
        offices.Items.Select(o => o.Id).ShouldContain(b.OfficeId);
    }

    // ------------------------------------------------------------------ the operator's view

    /// <summary>
    /// THE ISOLATION GUARANTEE. The operator is assigned to both offices, and one pending
    /// appointment dated today plus one pending change request are added to office A ONLY. Office
    /// A's three counts must each rise by exactly one and office B's must not move. A count taken
    /// without the office's own connection or filter would move B as well.
    /// </summary>
    [Fact]
    public async Task An_operator_sees_each_assigned_office_counted_inside_that_office_only()
    {
        var (a, b) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync("Ivy", token, intake: true);
        await AssignAsync(op.Id, a.OfficeId);
        await AssignAsync(op.Id, b.OfficeId);
        var before = await MetricsAsync(op.Id);

        // LOAD-BEARING DECOYS, office A only: the reason the assertions below can fail.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(a.OfficeId))
            {
                var decoy = await _appointments.InsertAsync(new Appointment(
                    id: Guid.NewGuid(),
                    patientId: a.PatientId,
                    identityUserId: null,
                    appointmentTypeId: a.AppointmentTypeId,
                    locationId: a.LocationId,
                    doctorAvailabilityId: a.DoctorAvailabilityId,
                    appointmentDate: PacificTime.TodayFrom(DateTime.UtcNow).AddHours(10),
                    requestConfirmationNumber: $"INT-{token}",
                    appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
                await _changeRequests.InsertAsync(new AppointmentChangeRequest(
                    Guid.NewGuid(), a.OfficeId, decoy.Id, ChangeRequestType.Cancel,
                    cancellationReason: "Synthetic decoy", reScheduleReason: null, newDoctorAvailabilityId: null), autoSave: true);
            }
        }, requiresNew: true);

        var offices = await As(op.Id, () => _service.GetMyOfficesAsync());
        var after = await MetricsAsync(op.Id);

        offices.Items.Select(o => o.Id).ShouldBe(new[] { a.OfficeId, b.OfficeId });
        after[a.OfficeId].OfficeName.ShouldBe(OfficeAName);
        (after[a.OfficeId].PendingRequests - before[a.OfficeId].PendingRequests).ShouldBe(1);
        (after[a.OfficeId].TodayAppointments - before[a.OfficeId].TodayAppointments).ShouldBe(1);
        (after[a.OfficeId].PendingChangeRequests - before[a.OfficeId].PendingChangeRequests).ShouldBe(1);
        (after[b.OfficeId].PendingRequests - before[b.OfficeId].PendingRequests).ShouldBe(0);
        (after[b.OfficeId].TodayAppointments - before[b.OfficeId].TodayAppointments).ShouldBe(0);
        (after[b.OfficeId].PendingChangeRequests - before[b.OfficeId].PendingChangeRequests).ShouldBe(0);
    }

    [Fact]
    public async Task An_anonymous_caller_has_no_offices_and_no_metrics()
    {
        await OfficesAsync();

        (await _service.GetMyOfficesAsync()).Items.ShouldBeEmpty();
        (await _service.GetMyOfficeMetricsAsync()).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task While_impersonating_the_operator_can_switch_between_their_offices_and_is_named()
    {
        var (a, _) = await OfficesAsync();
        var token = NewToken();
        var op = await HostUserAsync("Ivy", token, intake: true);
        await AssignAsync(op.Id, a.OfficeId);
        var shadowId = (await ShadowAsync(a.OfficeId, op.Email!)).User!.Id;

        var switchable = await As(shadowId, () => _service.GetSwitchableOfficesAsync(), impersonator: op.Id);
        var info = await As(shadowId, () => _service.GetImpersonatorInfoAsync(), impersonator: op.Id);

        switchable.Items.ShouldHaveSingleItem().Id.ShouldBe(a.OfficeId);
        info.IsImpersonating.ShouldBeTrue();
        info.Name.ShouldBe($"Ivy {token}");
        info.Roles.ShouldBe(new[] { IntakeStaff });
    }

    [Fact]
    public async Task Without_an_impersonation_there_is_nothing_to_switch_to_and_nobody_behind_it()
    {
        await OfficesAsync();

        var plain = await As(Guid.NewGuid(), () => _service.GetImpersonatorInfoAsync());
        var unknown = await As(Guid.NewGuid(), () => _service.GetImpersonatorInfoAsync(), impersonator: Guid.NewGuid());
        var switchable = await As(Guid.NewGuid(), () => _service.GetSwitchableOfficesAsync());

        plain.IsImpersonating.ShouldBeFalse();
        unknown.IsImpersonating.ShouldBeFalse();
        switchable.Items.ShouldBeEmpty();
    }
}
