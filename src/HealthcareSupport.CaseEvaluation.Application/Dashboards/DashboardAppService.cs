using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

[Authorize]
[RemoteService(IsEnabled = false)]
public class DashboardAppService : CaseEvaluationAppService, IDashboardAppService
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;

    public DashboardAppService(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentChangeRequest, Guid> changeRequestRepository,
        IRepository<Doctor, Guid> doctorRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        IRepository<Location, Guid> locationRepository,
        ISystemParameterRepository systemParameterRepository,
        ITenantWorkRunner tenantWorkRunner,
        IAuthorizationService authorizationService,
        IRepository<IdentityUser, Guid> identityUserRepository)
    {
        _appointmentRepository = appointmentRepository;
        _changeRequestRepository = changeRequestRepository;
        _doctorRepository = doctorRepository;
        _tenantRepository = tenantRepository;
        _patientRepository = patientRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
        _systemParameterRepository = systemParameterRepository;
        _tenantWorkRunner = tenantWorkRunner;
        _authorizationService = authorizationService;
        _identityUserRepository = identityUserRepository;
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
    /// Host branch: cross-office aggregate view. Database-per-office means there is
    /// no single connection that sees every office, so the host totals are the SUM
    /// of each office's counters (each computed inside its own database via the
    /// tenant work runner). TotalTenants is the registry count taken once -- it is
    /// the same for every office, so it must not be summed.
    /// </summary>
    private async Task<DashboardCountersDto> GetHostCountersAsync()
    {
        var perOffice = await _tenantWorkRunner.AggregateAcrossOfficesAsync(
            _ => BuildAsync(scopedToTenant: false));

        return new DashboardCountersDto
        {
            PendingRequests = perOffice.Sum(c => c.PendingRequests),
            ApprovedThisWeek = perOffice.Sum(c => c.ApprovedThisWeek),
            RejectedThisWeek = perOffice.Sum(c => c.RejectedThisWeek),
            PendingChangeRequests = perOffice.Sum(c => c.PendingChangeRequests),
            RequestsApproachingLegalDeadline = perOffice.Sum(c => c.RequestsApproachingLegalDeadline),
            DecisionOverdue = perOffice.Sum(c => c.DecisionOverdue),
            // 8 placeholders -- populated when day-of-exam states ship.
            BilledThisMonth = 0,
            NoShowThisMonth = 0,
            RescheduledThisMonth = 0,
            CancelledThisWeek = 0,
            CheckedInToday = 0,
            CheckedOutToday = 0,
            TotalDoctors = perOffice.Sum(c => c.TotalDoctors),
            TotalTenants = await _tenantRepository.CountAsync(),
        };
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

        // 2026-06-11: Pending requests past the per-tenant decision deadline
        // (default 3 days, below the legal 5-day limit). In host (cross-tenant)
        // scope there is no single tenant setting, so GetCurrentTenantAsync
        // returns null and we fall back to the default. The cutoff matches the
        // daily-digest per-row predicate so the tile equals the digest's
        // overdue count.
        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        var decisionDueDays = systemParameter?.PendingAppointmentOverDueNotificationDays
            ?? SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays;
        var decisionOverdueCutoffUtc = DecisionSlaPolicy.OverdueCreationCutoff(DateTime.UtcNow, decisionDueDays);
        var decisionOverdue = await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Pending
                 && a.CreationTime < decisionOverdueCutoffUtc);

        // Real pending change-request count (formerly a placeholder 0). Scoped
        // like the appointment counts: tenant-filtered for a Tenant caller,
        // cross-tenant for a Host caller (BuildAsync runs inside the
        // Disable<IMultiTenant> scope on the host branch).
        var pendingChangeRequests = await _changeRequestRepository.CountAsync(
            cr => cr.RequestStatus == RequestStatusType.Pending);

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
            PendingChangeRequests = pendingChangeRequests,
            RequestsApproachingLegalDeadline = requestsApproachingLegalDeadline,
            DecisionOverdue = decisionOverdue,
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

    // ===================== Redesigned composite dashboard (Prompt 9) =====================

    private const int TrendWeeks = 6;
    private const int DeadlineListSize = 5;
    private const int ApproachWindowDays = 2;
    private const int ScheduleListSize = 8;
    private const int ActivityListSize = 6;

    /// <summary>
    /// Rich payload for the redesigned internal dashboard. Host callers get
    /// cross-tenant KPIs + a per-tenant table; tenant callers get the hero KPIs
    /// (with prior-period deltas for the range-based Approved/Rejected), the
    /// decision-deadline list, a 6-week trend, the status breakdown, today's
    /// schedule, and recent activity. The legacy <see cref="GetAsync"/> stays for
    /// the nav badge.
    /// </summary>
    [Authorize]
    public virtual async Task<DashboardDto> GetDashboardAsync(DashboardRange range)
    {
        var isHost = await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Host);
        var isTenant = await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Tenant);
        if (!isHost && !isTenant)
        {
            throw new AbpAuthorizationException(L["Forbidden"]);
        }

        if (isHost)
        {
            return await BuildHostDashboardAsync();
        }

        return await BuildTenantDashboardAsync(range);
    }

    /// <summary>
    /// 2026-06-16 (Prompt 16, A-B4) -- host-only per-office user + appointment counts
    /// for the Tenants management table. Database-per-office: each office's counts are
    /// taken inside its own database via the tenant work runner (the IMultiTenant
    /// filter naturally scopes each count to that office); only aggregate counts (not
    /// PHI) leave the server. Gated by <c>Saas.Tenants</c> (the same permission the
    /// host Tenants page uses), which IT Admin holds.
    /// </summary>
    [Authorize("Saas.Tenants")]
    public virtual async Task<List<TenantSummaryDto>> GetTenantSummariesAsync()
    {
        var tenants = await _tenantRepository.GetListAsync();
        var nameById = tenants.ToDictionary(t => t.Id, t => t.Name);

        var summaries = await _tenantWorkRunner.AggregateAcrossOfficesAsync(async officeId =>
            new TenantSummaryDto
            {
                TenantId = officeId,
                Name = nameById[officeId],
                UserCount = await _identityUserRepository.CountAsync(),
                AppointmentCount = await _appointmentRepository.CountAsync(),
            });

        return summaries.OrderBy(s => s.Name).ToList();
    }

    private async Task<DashboardDto> BuildHostDashboardAsync()
    {
        var lastMondayUtc = GetLastMondayUtc();
        var tenants = await _tenantRepository.GetListAsync();
        var nameById = tenants.ToDictionary(t => t.Id, t => t.Name);

        // Database-per-office: count each office's appointments + doctors inside its
        // own database (the IMultiTenant filter scopes each count), then roll the host
        // totals up from the per-office results instead of disabling the filter on one
        // shared connection (which would see no other office's database).
        var perOffice = await _tenantWorkRunner.AggregateAcrossOfficesAsync(async officeId =>
            new
            {
                OfficeId = officeId,
                Appointments = await _appointmentRepository.CountAsync(),
                Pending = await _appointmentRepository.CountAsync(
                    a => a.AppointmentStatus == AppointmentStatusType.Pending),
                Approved = await _appointmentRepository.CountAsync(
                    a => a.AppointmentStatus == AppointmentStatusType.Approved),
                ThisWeek = await _appointmentRepository.CountAsync(
                    a => a.CreationTime >= lastMondayUtc),
                Doctors = await _doctorRepository.CountAsync(),
            });

        var rows = perOffice
            .Select(o => new DashboardTenantRowDto
            {
                TenantName = nameById[o.OfficeId],
                Appointments = o.Appointments,
                Pending = o.Pending,
                Approved = o.Approved,
                ThisWeek = o.ThisWeek,
            })
            .OrderByDescending(r => r.Appointments)
            .ToList();

        return new DashboardDto
        {
            IsHost = true,
            TotalTenants = tenants.Count,
            TotalDoctors = perOffice.Sum(o => o.Doctors),
            TotalAppointments = perOffice.Sum(o => o.Appointments),
            PendingAcrossTenants = perOffice.Sum(o => o.Pending),
            Tenants = rows,
        };
    }

    private async Task<DashboardDto> BuildTenantDashboardAsync(DashboardRange range)
    {
        var nowUtc = DateTime.UtcNow;
        var (currentStart, previousStart) = GetRangeWindows(range, nowUtc);
        var dto = new DashboardDto { IsHost = false };

        // Hero KPIs. Pending counts are live snapshots (no period delta);
        // Approved/Rejected are range-windowed with a prior-period comparison.
        dto.PendingRequests = Snapshot(await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Pending));
        dto.PendingChangeRequests = Snapshot(await _changeRequestRepository.CountAsync(
            cr => cr.RequestStatus == RequestStatusType.Pending));

        dto.ApprovedRequests = new DashboardKpiDto
        {
            Value = await _appointmentRepository.CountAsync(
                a => a.AppointmentStatus == AppointmentStatusType.Approved
                     && a.AppointmentApproveDate != null
                     && a.AppointmentApproveDate >= currentStart),
            PreviousValue = await _appointmentRepository.CountAsync(
                a => a.AppointmentStatus == AppointmentStatusType.Approved
                     && a.AppointmentApproveDate != null
                     && a.AppointmentApproveDate >= previousStart
                     && a.AppointmentApproveDate < currentStart),
        };

        dto.RejectedRequests = new DashboardKpiDto
        {
            Value = await _appointmentRepository.CountAsync(
                a => a.AppointmentStatus == AppointmentStatusType.Rejected
                     && a.LastModificationTime != null
                     && a.LastModificationTime >= currentStart),
            PreviousValue = await _appointmentRepository.CountAsync(
                a => a.AppointmentStatus == AppointmentStatusType.Rejected
                     && a.LastModificationTime != null
                     && a.LastModificationTime >= previousStart
                     && a.LastModificationTime < currentStart),
        };

        await BuildDeadlineSectionAsync(dto, nowUtc);
        dto.Trend = await BuildTrendAsync();
        dto.StatusBreakdown = await BuildStatusBreakdownAsync();
        dto.TodaySchedule = await BuildTodayScheduleAsync(nowUtc);
        dto.RecentActivity = await BuildRecentActivityAsync();
        return dto;
    }

    /// <summary>
    /// Pending requests near (or just past) the per-tenant decision deadline,
    /// most urgent first. Sets both the list (top N) and the header count. The
    /// "approaching" band is requests whose decision due date (creation +
    /// decisionDueDays) falls within the next <see cref="ApproachWindowDays"/>.
    /// </summary>
    private async Task BuildDeadlineSectionAsync(DashboardDto dto, DateTime nowUtc)
    {
        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        var decisionDueDays = systemParameter?.PendingAppointmentOverDueNotificationDays
            ?? SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays;

        var today = nowUtc.Date;
        var lower = today.AddDays(-decisionDueDays);
        var upperExcl = today.AddDays(ApproachWindowDays - decisionDueDays + 1);

        dto.DeadlineApproachingCount = await _appointmentRepository.CountAsync(
            a => a.AppointmentStatus == AppointmentStatusType.Pending
                 && a.CreationTime >= lower && a.CreationTime < upperExcl);

        var q = await _appointmentRepository.GetQueryableAsync();
        var pq = await _patientRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            (from a in q
             where a.AppointmentStatus == AppointmentStatusType.Pending
                   && a.CreationTime >= lower && a.CreationTime < upperExcl
             join p in pq on a.PatientId equals p.Id
             orderby a.CreationTime
             select new
             {
                 a.Id,
                 a.RequestConfirmationNumber,
                 a.CreationTime,
                 p.FirstName,
                 p.LastName,
             }).Take(DeadlineListSize));

        dto.Deadlines = rows.Select(r =>
        {
            var due = DecisionSlaPolicy.DecisionDueDate(r.CreationTime, decisionDueDays);
            return new DashboardDeadlineItemDto
            {
                AppointmentId = r.Id,
                ConfirmationNumber = r.RequestConfirmationNumber,
                PatientName = $"{r.FirstName} {r.LastName}".Trim(),
                RequestedAt = r.CreationTime,
                DueDate = due,
                DaysRemaining = (due - today).Days,
            };
        }).ToList();
    }

    private async Task<List<DashboardTrendPointDto>> BuildTrendAsync()
    {
        var lastMondayUtc = GetLastMondayUtc();
        var trend = new List<DashboardTrendPointDto>();
        for (var i = TrendWeeks - 1; i >= 0; i--)
        {
            var weekStart = lastMondayUtc.AddDays(-7 * i);
            var weekEnd = weekStart.AddDays(7);
            // Volume bar: requests RECEIVED that week (by creation date).
            var count = await _appointmentRepository.CountAsync(
                a => a.CreationTime >= weekStart && a.CreationTime < weekEnd);
            // Completion line: requests APPROVED that week (by approve date), mirroring
            // the ApprovedRequests hero KPI so "received in" and "approved out" compare.
            var completedCount = await _appointmentRepository.CountAsync(
                a => a.AppointmentStatus == AppointmentStatusType.Approved
                     && a.AppointmentApproveDate != null
                     && a.AppointmentApproveDate >= weekStart
                     && a.AppointmentApproveDate < weekEnd);
            trend.Add(new DashboardTrendPointDto
            {
                Label = $"Wk {TrendWeeks - i}",
                WeekStart = weekStart,
                Count = count,
                CompletedCount = completedCount,
            });
        }
        return trend;
    }

    private async Task<List<DashboardStatusSliceDto>> BuildStatusBreakdownAsync()
    {
        var q = await _appointmentRepository.GetQueryableAsync();
        var byStatus = await AsyncExecuter.ToListAsync(
            q.GroupBy(a => a.AppointmentStatus)
             .Select(g => new { Status = g.Key, Count = g.Count() }));

        var counts = StatusPillPolicy.DonutOrder.ToDictionary(p => p, _ => 0);
        foreach (var row in byStatus)
        {
            var pill = StatusPillPolicy.ToPill(row.Status);
            if (pill != null)
            {
                counts[pill] += row.Count;
            }
        }

        return StatusPillPolicy.DonutOrder
            .Select(p => new DashboardStatusSliceDto { Pill = p, Count = counts[p] })
            .ToList();
    }

    private async Task<List<DashboardScheduleItemDto>> BuildTodayScheduleAsync(DateTime nowUtc)
    {
        var todayStart = nowUtc.Date;
        var todayEnd = todayStart.AddDays(1);
        var q = await _appointmentRepository.GetQueryableAsync();
        var tq = await _appointmentTypeRepository.GetQueryableAsync();
        var lq = await _locationRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            (from a in q
             where a.AppointmentDate >= todayStart && a.AppointmentDate < todayEnd
             join t in tq on a.AppointmentTypeId equals t.Id
             join l in lq on a.LocationId equals l.Id
             orderby a.AppointmentDate
             select new
             {
                 a.AppointmentDate,
                 TypeName = t.Name,
                 LocationName = l.Name,
             }).Take(ScheduleListSize));

        return rows.Select(r => new DashboardScheduleItemDto
        {
            AppointmentDate = r.AppointmentDate,
            AppointmentType = r.TypeName,
            Location = r.LocationName,
        }).ToList();
    }

    private async Task<List<DashboardActivityItemDto>> BuildRecentActivityAsync()
    {
        var q = await _appointmentRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(a => a.LastModificationTime ?? a.CreationTime)
             .Take(ActivityListSize)
             .Select(a => new
             {
                 a.RequestConfirmationNumber,
                 a.AppointmentStatus,
                 a.CreationTime,
                 a.LastModificationTime,
             }));

        return rows.Select(r =>
        {
            var (icon, tint, verb) = DescribeActivity(r.AppointmentStatus);
            return new DashboardActivityItemDto
            {
                Icon = icon,
                Tint = tint,
                Text = $"{r.RequestConfirmationNumber} {verb}",
                When = r.LastModificationTime ?? r.CreationTime,
            };
        }).ToList();
    }

    private static (string Icon, string Tint, string Verb) DescribeActivity(AppointmentStatusType status) =>
        status switch
        {
            AppointmentStatusType.Approved => ("check", "tint-green", "was approved"),
            AppointmentStatusType.Rejected => ("x", "tint-red", "was rejected"),
            AppointmentStatusType.InfoRequested => ("help", "tint-amber", "needs more information"),
            AppointmentStatusType.RescheduleRequested or AppointmentStatusType.CancellationRequested
                => ("refresh", "tint-amber", "has a pending change request"),
            AppointmentStatusType.RescheduledNoBill or AppointmentStatusType.RescheduledLate
                => ("refresh", "tint-blue", "was rescheduled"),
            AppointmentStatusType.CancelledNoBill or AppointmentStatusType.CancelledLate
                => ("x", "tint-red", "was cancelled"),
            AppointmentStatusType.Pending => ("user", "tint-purple", "is a new request"),
            _ => ("doc", "tint-blue", "was updated"),
        };

    private static DashboardKpiDto Snapshot(int value) => new() { Value = value, PreviousValue = value };

    private (DateTime CurrentStart, DateTime PreviousStart) GetRangeWindows(DashboardRange range, DateTime nowUtc)
    {
        switch (range)
        {
            case DashboardRange.Month:
                {
                    var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return (monthStart, monthStart.AddMonths(-1));
                }
            case DashboardRange.Quarter:
                {
                    var quarterStartMonth = ((nowUtc.Month - 1) / 3 * 3) + 1;
                    var quarterStart = new DateTime(nowUtc.Year, quarterStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                    return (quarterStart, quarterStart.AddMonths(-3));
                }
            case DashboardRange.Week:
            default:
                {
                    var weekStart = GetLastMondayUtc();
                    return (weekStart, weekStart.AddDays(-7));
                }
        }
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
