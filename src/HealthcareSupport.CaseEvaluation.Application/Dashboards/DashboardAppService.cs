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
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Saas;
using Volo.Saas.Editions;
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
    private readonly IRepository<Edition, Guid> _editionRepository;

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
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<Edition, Guid> editionRepository)
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
        _editionRepository = editionRepository;
    }

    [Authorize]
    public virtual async Task<DashboardCountersDto> GetAsync()
    {
        // Branch on the request's tenant SCOPE (not which permission is held), and
        // probe ONLY the scope-appropriate permission. Probing Dashboard.Host in
        // tenant context (e.g. the 60s nav-badge poll after a host operator switches
        // into an office) logged a benign-but-noisy "Dashboard.Host not granted" auth
        // failure on every tick even though the request succeeded via Dashboard.Tenant.
        if (CurrentTenant.Id == null)
        {
            if (!await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Host))
            {
                throw new AbpAuthorizationException(L["Forbidden"]);
            }
            return await GetHostCountersAsync();
        }

        if (!await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Tenant))
        {
            throw new AbpAuthorizationException(L["Forbidden"]);
        }
        return await GetTenantCountersAsync();
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

    private const int MaxTrendBuckets = 14;
    private const int DeadlineListSize = 5;
    private const int ApproachWindowDays = 2;
    private const int ScheduleListSize = 8;
    private const int ActivityListSize = 6;

    /// <summary>
    /// Rich payload for the redesigned internal dashboard. Host callers get
    /// cross-tenant KPIs + a per-tenant table; tenant callers get the hero KPIs
    /// (with prior-period deltas for the range-based Approved/Rejected), plus a
    /// range-windowed trend, status breakdown, and recent activity. The
    /// decision-deadline list and today's schedule stay point-in-time. The legacy
    /// <see cref="GetAsync"/> stays for the nav badge.
    /// </summary>
    [Authorize]
    public virtual async Task<DashboardDto> GetDashboardAsync(DashboardRange range)
    {
        // Scope-based branch (see GetAsync): probe only the scope-appropriate
        // permission so a tenant-context call never logs a benign Dashboard.Host
        // authorization failure.
        if (CurrentTenant.Id == null)
        {
            if (!await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Host))
            {
                throw new AbpAuthorizationException(L["Forbidden"]);
            }
            return await BuildHostDashboardAsync();
        }

        if (!await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.Dashboard.Tenant))
        {
            throw new AbpAuthorizationException(L["Forbidden"]);
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

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged Offices/Tenants list. See
    /// <see cref="IDashboardAppService.GetOfficesAsync"/>. Reads the office
    /// registry + editions host-side, then computes user/appointment counts only
    /// for the page's offices (each inside its own database). The office registry
    /// is bounded (one row per office), so the name/edition filter + sort + paging
    /// are applied in memory before the per-office count hops.
    /// </summary>
    [Authorize("Saas.Tenants")]
    public virtual async Task<PagedResultDto<OfficeListDto>> GetOfficesAsync(GetOfficesInput input)
    {
        Check.NotNull(input, nameof(input));

        using (CurrentTenant.Change(null))
        {
            var tenants = await _tenantRepository.GetListAsync();
            var editions = await _editionRepository.GetListAsync();
            var editionNameById = editions.ToDictionary(e => e.Id, e => e.DisplayName ?? string.Empty);

            var filter = input.Filter?.Trim();
            var filtered = tenants
                .Where(t => string.IsNullOrEmpty(filter)
                    || (t.Name != null && t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var totalCount = filtered.Count;

            var pageTenants = SortOffices(filtered, editionNameById, input.Sorting)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();

            var rows = new List<OfficeListDto>(pageTenants.Count);
            foreach (var tenant in pageTenants)
            {
                int userCount;
                int appointmentCount;
                // Database-per-office: each count runs inside that office's own
                // database (the IMultiTenant filter scopes it naturally).
                using (CurrentTenant.Change(tenant.Id))
                {
                    userCount = await _identityUserRepository.CountAsync();
                    appointmentCount = await _appointmentRepository.CountAsync();
                }

                rows.Add(new OfficeListDto
                {
                    Id = tenant.Id,
                    Name = tenant.Name ?? string.Empty,
                    Subdomain = (tenant.Name ?? string.Empty).ToLowerInvariant(),
                    EditionId = tenant.EditionId,
                    EditionName = ResolveEditionName(tenant, editionNameById),
                    UserCount = userCount,
                    AppointmentCount = appointmentCount,
                    IsActive = tenant.ActivationState != TenantActivationState.Passive,
                    ConcurrencyStamp = tenant.ConcurrencyStamp,
                });
            }

            return new PagedResultDto<OfficeListDto>(totalCount, rows);
        }
    }

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged host per-office breakdown. See
    /// <see cref="IDashboardAppService.GetTenantBreakdownAsync"/>. Mirrors
    /// <see cref="BuildHostDashboardAsync"/>'s per-office counting (each office in
    /// its own database via the tenant work runner); the result set is bounded by
    /// office count, so the name filter + sort + paging run in memory after the
    /// aggregate.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.Dashboard.Host)]
    public virtual async Task<PagedResultDto<DashboardTenantRowDto>> GetTenantBreakdownAsync(
        GetTenantBreakdownInput input)
    {
        Check.NotNull(input, nameof(input));

        var lastMondayUtc = GetLastMondayUtc();
        var tenants = await _tenantRepository.GetListAsync();
        var nameById = tenants.ToDictionary(t => t.Id, t => t.Name);

        var perOffice = await _tenantWorkRunner.AggregateAcrossOfficesAsync(async officeId =>
            new DashboardTenantRowDto
            {
                TenantName = nameById[officeId] ?? string.Empty,
                Appointments = await _appointmentRepository.CountAsync(),
                Pending = await _appointmentRepository.CountAsync(
                    a => a.AppointmentStatus == AppointmentStatusType.Pending),
                Approved = await _appointmentRepository.CountAsync(
                    a => a.AppointmentStatus == AppointmentStatusType.Approved),
                ThisWeek = await _appointmentRepository.CountAsync(
                    a => a.CreationTime >= lastMondayUtc),
            });

        var filter = input.Filter?.Trim();
        var filtered = perOffice
            .Where(r => string.IsNullOrEmpty(filter)
                || r.TenantName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var totalCount = filtered.Count;

        var page = SortTenantBreakdown(filtered, input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<DashboardTenantRowDto>(totalCount, page);
    }

    private static string ResolveEditionName(
        Tenant tenant, IReadOnlyDictionary<Guid, string> editionNameById) =>
        tenant.EditionId.HasValue && editionNameById.TryGetValue(tenant.EditionId.Value, out var name)
            ? name
            : string.Empty;

    private static List<Tenant> SortOffices(
        List<Tenant> tenants,
        IReadOnlyDictionary<Guid, string> editionNameById,
        string? sorting)
    {
        var parts = (sorting ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts.Length > 0 ? parts[0].ToLowerInvariant() : "name";
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<Tenant> ordered = field switch
        {
            "editionname" or "edition" => descending
                ? tenants.OrderByDescending(t => ResolveEditionName(t, editionNameById), StringComparer.OrdinalIgnoreCase)
                : tenants.OrderBy(t => ResolveEditionName(t, editionNameById), StringComparer.OrdinalIgnoreCase),
            _ => descending
                ? tenants.OrderByDescending(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : tenants.OrderBy(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase),
        };
        return ordered.ToList();
    }

    private static List<DashboardTenantRowDto> SortTenantBreakdown(
        List<DashboardTenantRowDto> rows, string? sorting)
    {
        var parts = (sorting ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasSort = parts.Length > 0;
        var field = hasSort ? parts[0].ToLowerInvariant() : "appointments";
        // No client sort -> most-active office first (mirrors the dashboard's own order).
        var descending = hasSort
            ? parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
            : true;

        IOrderedEnumerable<DashboardTenantRowDto> ordered = field switch
        {
            "tenantname" or "name" or "tenant" => descending
                ? rows.OrderByDescending(r => r.TenantName, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(r => r.TenantName, StringComparer.OrdinalIgnoreCase),
            "pending" => descending ? rows.OrderByDescending(r => r.Pending) : rows.OrderBy(r => r.Pending),
            "approved" => descending ? rows.OrderByDescending(r => r.Approved) : rows.OrderBy(r => r.Approved),
            "thisweek" => descending ? rows.OrderByDescending(r => r.ThisWeek) : rows.OrderBy(r => r.ThisWeek),
            _ => descending ? rows.OrderByDescending(r => r.Appointments) : rows.OrderBy(r => r.Appointments),
        };
        return ordered.ToList();
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
        // QA item G: the donut, trend, and recent activity now follow the selected
        // range window so the whole tenant dashboard reflects one period. Live
        // Pending tiles, the decision-deadline SLA list, and Today's schedule stay
        // point-in-time by design (a quarter-long "today" is meaningless).
        dto.Trend = await BuildTrendAsync(currentStart, nowUtc);
        dto.StatusBreakdown = await BuildStatusBreakdownAsync(currentStart);
        dto.TodaySchedule = await BuildTodayScheduleAsync(nowUtc);
        dto.RecentActivity = await BuildRecentActivityAsync(currentStart);
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

    // Weekly buckets spanning the selected range window [currentStart, now] so the
    // trend matches the period the KPI tiles + donut reflect (QA item G). The
    // window length sets the bucket count: Week -> 1, Month -> ~5, Quarter -> ~13,
    // capped at MaxTrendBuckets.
    private async Task<List<DashboardTrendPointDto>> BuildTrendAsync(DateTime currentStart, DateTime nowUtc)
    {
        var trend = new List<DashboardTrendPointDto>();
        var weekStart = currentStart;
        for (var index = 0; weekStart < nowUtc && index < MaxTrendBuckets; index++)
        {
            var bucketStart = weekStart;
            var bucketEnd = bucketStart.AddDays(7);
            // Volume bar: requests RECEIVED that week (by creation date).
            var count = await _appointmentRepository.CountAsync(
                a => a.CreationTime >= bucketStart && a.CreationTime < bucketEnd);
            // Completion line: requests APPROVED that week (by approve date), mirroring
            // the ApprovedRequests hero KPI so "received in" and "approved out" compare.
            var completedCount = await _appointmentRepository.CountAsync(
                a => a.AppointmentStatus == AppointmentStatusType.Approved
                     && a.AppointmentApproveDate != null
                     && a.AppointmentApproveDate >= bucketStart
                     && a.AppointmentApproveDate < bucketEnd);
            trend.Add(new DashboardTrendPointDto
            {
                Label = $"Wk {index + 1}",
                WeekStart = bucketStart,
                Count = count,
                CompletedCount = completedCount,
            });
            weekStart = bucketEnd;
        }
        return trend;
    }

    // QA item G: window the status breakdown to requests CREATED in the selected
    // range so the donut reflects the same period as the KPI tiles.
    private async Task<List<DashboardStatusSliceDto>> BuildStatusBreakdownAsync(DateTime currentStart)
    {
        var q = await _appointmentRepository.GetQueryableAsync();
        var byStatus = await AsyncExecuter.ToListAsync(
            q.Where(a => a.CreationTime >= currentStart)
             .GroupBy(a => a.AppointmentStatus)
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

    // QA item G: only activity within the selected range, so the feed matches the
    // period the rest of the tenant dashboard reflects.
    private async Task<List<DashboardActivityItemDto>> BuildRecentActivityAsync(DateTime currentStart)
    {
        var q = await _appointmentRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            q.Where(a => (a.LastModificationTime ?? a.CreationTime) >= currentStart)
             .OrderByDescending(a => a.LastModificationTime ?? a.CreationTime)
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
