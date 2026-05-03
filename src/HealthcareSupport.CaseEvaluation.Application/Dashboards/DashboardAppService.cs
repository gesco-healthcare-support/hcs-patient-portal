using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

[Authorize]
[RemoteService(IsEnabled = false)]
public class DashboardAppService : CaseEvaluationAppService, IDashboardAppService
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IAuthorizationService _authorizationService;

    public DashboardAppService(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Doctor, Guid> doctorRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IDataFilter dataFilter,
        IAuthorizationService authorizationService)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository = doctorRepository;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
        _authorizationService = authorizationService;
    }

    [Authorize]
    public virtual async Task<DashboardCountersDto> GetAsync()
    {
        var isHost = await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Host);
        var isTenant = await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Tenant);
        if (!isHost && !isTenant)
        {
            throw new AbpAuthorizationException(L["Forbidden"]);
        }

        return isHost ? await GetHostCountersAsync() : await GetTenantCountersAsync();
    }

    /// <summary>
    /// Host branch: cross-tenant aggregate view via DataFilter.Disable.
    /// Tenant + doctor totals are visible host-side; per-tenant counts roll
    /// up to wave-level totals.
    /// </summary>
    private async Task<DashboardCountersDto> GetHostCountersAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await BuildAsync(scopedToTenant: false);
        }
    }

    /// <summary>
    /// Tenant branch: ABP's IMultiTenant filter scopes Appointment +
    /// Doctor counts to the current tenant. TotalTenants stays 0 (host-only).
    /// </summary>
    private async Task<DashboardCountersDto> GetTenantCountersAsync()
    {
        return await BuildAsync(scopedToTenant: true);
    }

    private async Task<DashboardCountersDto> BuildAsync(bool scopedToTenant)
    {
        var lastMondayUtc = GetLastMondayUtc();
        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var legalDeadlineThresholdUtc = DateTime.UtcNow.AddDays(-60);

        var pendingRequests = await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Pending);

        var approvedThisWeek = await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Approved
                 && a.AppointmentApproveDate != null
                 && a.AppointmentApproveDate >= lastMondayUtc);

        var rejectedThisWeek = await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Rejected
                 && a.LastModificationTime != null
                 && a.LastModificationTime >= lastMondayUtc);

        var requestsApproachingLegalDeadline = await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Pending
                 && a.CreationTime <= legalDeadlineThresholdUtc);

        var totalDoctors = scopedToTenant
            ? 0   // Doctor is per-tenant; host view shows the cross-tenant total.
            : await _doctorRepository.CountAsync();

        var totalTenants = scopedToTenant
            ? 0
            : await _tenantRepository.CountAsync();

        return new DashboardCountersDto
        {
            PendingRequests = pendingRequests,
            ApprovedThisWeek = approvedThisWeek,
            RejectedThisWeek = rejectedThisWeek,
            // W3-TODO: replace 0 once AppointmentChangeRequest entity ships.
            PendingChangeRequests = 0,
            RequestsApproachingLegalDeadline = requestsApproachingLegalDeadline,
            // 8 placeholders -- populated when day-of-exam states ship.
            BilledThisMonth = 0,
            NoShowThisMonth = 0,
            RescheduledThisMonth = 0,
            CancelledThisWeek = 0,
            CheckedInToday = 0,
            CheckedOutToday = 0,
            TotalDoctors = scopedToTenant ? 0 : totalDoctors,
            TotalTenants = scopedToTenant ? 0 : totalTenants,
        };
    }

    /// <summary>
    /// "This week" boundary is Monday 00:00 UTC. When today is Monday the
    /// returned value equals today's date; the count includes today.
    /// </summary>
    private static DateTime GetLastMondayUtc()
    {
        var today = DateTime.UtcNow;
        var daysToMonday = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
        if (daysToMonday < 0)
        {
            daysToMonday += 7;
        }
        return today.AddDays(-daysToMonday).Date;
    }
}
