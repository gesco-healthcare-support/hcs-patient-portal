using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Raised by the weekly missing-intake report job (#944, Domain) when at least one office has an appointment that
/// was approved after the office's first intake row and still has no intake row; emailed to the technical list by
/// a handler in Application, where the dispatcher lives. Confirmation numbers, ids, statuses and dates only -- no
/// patient field, so the report cannot be what puts PHI in an inbox.
/// </summary>
public class CaseTrackerMissingIntakesEto
{
    /// <summary>When the job ran. It also dates the email's context tag, so each week's repeat is sent.</summary>
    public DateTime RunAt { get; set; }

    /// <summary>Only the offices with at least one likely-lost appointment.</summary>
    public List<CaseTrackerMissingIntakesOfficeEto> Offices { get; set; } = [];

    /// <summary>Offices that could not be read this run, so the email never implies they were checked.</summary>
    public List<string> FailedOfficeNames { get; set; } = [];
}

/// <summary>One office's likely-lost appointments.</summary>
public class CaseTrackerMissingIntakesOfficeEto
{
    public Guid TenantId { get; set; }

    /// <summary>From the tenant store, never <c>ICurrentTenant.Name</c>, which is null inside a changed scope.</summary>
    public string OfficeName { get; set; } = string.Empty;

    /// <summary>When the office first wrote an intake row; everything listed was approved after it.</summary>
    public DateTime FirstIntakeRowAt { get; set; }

    public List<CaseTrackerMissingIntakeLineEto> Items { get; set; } = [];
}

/// <summary>One likely-lost appointment.</summary>
public class CaseTrackerMissingIntakeLineEto
{
    public Guid AppointmentId { get; set; }

    public string ConfirmationNumber { get; set; } = string.Empty;

    public AppointmentStatusType Status { get; set; }

    public DateTime ApprovedAt { get; set; }
}
