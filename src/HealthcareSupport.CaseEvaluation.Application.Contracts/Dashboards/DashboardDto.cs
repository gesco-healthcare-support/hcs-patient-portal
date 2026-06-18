using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// Composite payload for the redesigned internal dashboard (Prompt 9). One call
/// serves every role; <see cref="IsHost"/> selects host vs tenant rendering and
/// the frontend role key (Supervisor vs Intake) decides which tenant sections to
/// show. Aggregates of PHI rows are not PHI under HIPAA Safe Harbor; per-tenant
/// scope is applied server-side (tenant DataFilter for Tenant callers, disabled
/// for Host callers). Patient NAMES appear only in the staff-facing deadline
/// list (staff are authorized to see them); no SSN/DOB is included anywhere.
/// </summary>
public class DashboardDto
{
    /// <summary>True for the host (cross-tenant) view; false for a tenant view.</summary>
    public bool IsHost { get; set; }

    // ---- Tenant hero KPIs (value + prior-period value for the delta badge) ----
    public DashboardKpiDto PendingRequests { get; set; } = new();
    public DashboardKpiDto PendingChangeRequests { get; set; } = new();
    public DashboardKpiDto ApprovedRequests { get; set; } = new();
    public DashboardKpiDto RejectedRequests { get; set; } = new();

    // ---- Host hero KPIs ----
    public int TotalTenants { get; set; }
    public int TotalDoctors { get; set; }
    public int TotalAppointments { get; set; }
    public int PendingAcrossTenants { get; set; }

    // ---- Tenant sections ----
    /// <summary>Pending requests approaching the decision deadline (most urgent first).</summary>
    public List<DashboardDeadlineItemDto> Deadlines { get; set; } = new();

    /// <summary>Total count of pending requests approaching the deadline (the alert header).</summary>
    public int DeadlineApproachingCount { get; set; }

    /// <summary>Requests created per week for the last 6 weeks (trend chart).</summary>
    public List<DashboardTrendPointDto> Trend { get; set; } = new();

    /// <summary>Current status distribution across the six UI pills (donut).</summary>
    public List<DashboardStatusSliceDto> StatusBreakdown { get; set; } = new();

    /// <summary>Today's appointments (time, type, location).</summary>
    public List<DashboardScheduleItemDto> TodaySchedule { get; set; } = new();

    /// <summary>Recent appointment activity (icon, text, when).</summary>
    public List<DashboardActivityItemDto> RecentActivity { get; set; } = new();

    // ---- Host section ----
    /// <summary>Per-tenant rollup (host view only).</summary>
    public List<DashboardTenantRowDto> Tenants { get; set; } = new();
}

/// <summary>A KPI value plus the comparable prior-period value for the delta badge.
/// For snapshot metrics (live Pending counts) PreviousValue == Value (no delta).</summary>
public class DashboardKpiDto
{
    public int Value { get; set; }
    public int PreviousValue { get; set; }
}

/// <summary>One donut slice. Pill is one of the six UI keys (Pending, InfoRequested,
/// Approved, Rescheduled, Cancelled, Rejected).</summary>
public class DashboardStatusSliceDto
{
    public string Pill { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardTrendPointDto
{
    public string Label { get; set; } = string.Empty;

    /// <summary>UTC start (Monday) of the week this point covers, for the chart's real-date axis.</summary>
    public DateTime WeekStart { get; set; }

    public int Count { get; set; }
}

public class DashboardDeadlineItemDto
{
    public Guid AppointmentId { get; set; }
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime DueDate { get; set; }
    /// <summary>Days until the decision deadline (0 = due today; negative = overdue).</summary>
    public int DaysRemaining { get; set; }
}

public class DashboardScheduleItemDto
{
    public DateTime AppointmentDate { get; set; }
    public string AppointmentType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class DashboardActivityItemDto
{
    /// <summary>Icon key (matches the app icon registry: check / refresh / doc / x / user / alert).</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Tone class (tint-green / tint-amber / tint-blue / tint-red / tint-purple).</summary>
    public string Tint { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
    public DateTime When { get; set; }
}

public class DashboardTenantRowDto
{
    public string TenantName { get; set; } = string.Empty;
    public int Appointments { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int ThisWeek { get; set; }
}
