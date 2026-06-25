using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- daily per-internal-staff
/// queue-counts email. Mirrors OLD <c>SchedulerDomain.cs</c>:87 --
/// each Staff Supervisor / Intake Staff user receives an email with the
/// tenant-wide PendingAppointmentCount + ApprovedAppointmentCount.
///
/// <para>Cron: 09:15 PT daily -- after the
/// <c>PendingDailyDigestJob</c> so the counts reflect the same
/// post-auto-cancel snapshot the intake-staff inbox saw.</para>
///
/// <para>Scope decision (Adrian 2026-05-10): email only; OLD's SMS leg
/// (Twilio at OLD :105) is dropped for Phase 1.</para>
/// </summary>
public class InternalStaffQueueDigestJob : ITransientDependency
{
    public const string RecurringJobId = "appt-internal-staff-queue-digest";
    public const string CronExpression = "15 9 * * *";

    // OLD :92 hardcoded AppointmentStatusId=1 (Pending). Per BookingSubmissionEmailHandler
    // line 85-89 these are the only internal-staff roles that receive ApproveReject /
    // queue-counts emails -- admin / IT Admin / Doctor intentionally excluded.
    private static readonly string[] InternalStaffRoles =
    {
        "Staff Supervisor",
        "Intake Staff",
    };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<InternalStaffQueueDigestJob> _logger;

    public InternalStaffQueueDigestJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IdentityUserManager userManager,
        ITenantWorkRunner tenantWorkRunner,
        ILocalEventBus localEventBus,
        ILogger<InternalStaffQueueDigestJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _userManager = userManager;
        _tenantWorkRunner = tenantWorkRunner;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("InternalStaffQueueDigestJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;

        await _tenantWorkRunner.ForEachOfficeAsync(officeId =>
            ProcessTenantAsync(officeId, nowUtc));
    }

    private async Task ProcessTenantAsync(Guid? tenantId, DateTime nowUtc)
    {
        var queryable = await _appointmentRepository.GetQueryableAsync();
        var pendingCount = queryable.Count(a => a.AppointmentStatus == AppointmentStatusType.Pending);
        var approvedCount = queryable.Count(a => a.AppointmentStatus == AppointmentStatusType.Approved);

        var staff = await ResolveInternalStaffAsync();
        if (staff.Count == 0)
        {
            _logger.LogInformation(
                "InternalStaffQueueDigestJob: tenant {TenantId} has no internal staff (Intake Staff / Staff Supervisor); skipping.",
                tenantId);
            return;
        }

        foreach (var user in staff)
        {
            await _localEventBus.PublishAsync(new InternalStaffQueueDigestEto
            {
                TenantId = tenantId,
                StaffUserId = user.Id,
                StaffEmail = user.Email,
                StaffFirstName = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name,
                PendingAppointmentCount = pendingCount,
                ApprovedAppointmentCount = approvedCount,
                OccurredAt = nowUtc,
            });
        }

        _logger.LogInformation(
            "InternalStaffQueueDigestJob: tenant {TenantId} published {Count} digest event(s) (pending={Pending}, approved={Approved}).",
            tenantId,
            staff.Count,
            pendingCount,
            approvedCount);
    }

    private async Task<List<IdentityUser>> ResolveInternalStaffAsync()
    {
        var byId = new Dictionary<Guid, IdentityUser>();
        foreach (var roleName in InternalStaffRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }
                byId[user.Id] = user;
            }
        }
        return byId.Values.ToList();
    }
}
