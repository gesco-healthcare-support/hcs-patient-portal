using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The missing-intake report (#944): published appointments with no intake outbox row, per office, with no date
/// floor. Ids, confirmation numbers, statuses and dates only -- no patient field.
/// </summary>
public class CaseTrackerMissingIntakeReportDto
{
    public DateTime GeneratedAt { get; set; }

    public List<CaseTrackerMissingIntakeOfficeDto> Offices { get; set; } = [];
}

/// <summary>One office's section of the report.</summary>
public class CaseTrackerMissingIntakeOfficeDto
{
    public Guid OfficeId { get; set; }

    public string OfficeName { get; set; } = string.Empty;

    /// <summary>True when the office could not be read; its lists are then empty and say nothing.</summary>
    public bool Failed { get; set; }

    /// <summary>When the office first wrote an intake row. Null: it never has.</summary>
    public DateTime? FirstIntakeRowAt { get; set; }

    /// <summary>Approved after the first intake row, packets settled, no intake row. Never truncated.</summary>
    public List<CaseTrackerMissingIntakeItemDto> LikelyLost { get; set; } = [];

    /// <summary>Approved after the first intake row, packets still settling: no row yet is normal.</summary>
    public List<CaseTrackerMissingIntakeItemDto> Settling { get; set; } = [];

    /// <summary>Approved before the first intake row, newest first, at most 200.</summary>
    public List<CaseTrackerMissingIntakeItemDto> BeforeIntegration { get; set; } = [];

    /// <summary>The true number approved before the first intake row.</summary>
    public int BeforeIntegrationCount { get; set; }
}

/// <summary>One appointment in the report.</summary>
public class CaseTrackerMissingIntakeItemDto
{
    public Guid AppointmentId { get; set; }

    public string ConfirmationNumber { get; set; } = string.Empty;

    public AppointmentStatusType Status { get; set; }

    /// <summary>The approval date, or the creation time when none was stored.</summary>
    public DateTime ApprovedAt { get; set; }
}
