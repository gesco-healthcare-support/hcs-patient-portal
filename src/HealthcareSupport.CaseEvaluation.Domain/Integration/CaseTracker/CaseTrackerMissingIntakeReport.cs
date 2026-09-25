using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>How the missing-intake report (#944) reads an appointment that has no intake outbox row.</summary>
public enum CaseTrackerMissingIntakeKind
{
    /// <summary>Approved after the office's first intake row, packet set settled: its enqueue was lost.</summary>
    LikelyLost = 1,

    /// <summary>Approved before the office's integration wrote any intake row: excluded by design.</summary>
    BeforeIntegration = 2,

    /// <summary>Approved after the first intake row, packet set still settling: no row yet is normal.</summary>
    Settling = 3,
}

/// <summary>One appointment in the missing-intake report. Ids, confirmation number, status and date only.</summary>
public sealed class CaseTrackerMissingIntakeItem
{
    public Guid AppointmentId { get; init; }

    public string ConfirmationNumber { get; init; } = string.Empty;

    public AppointmentStatusType Status { get; init; }

    /// <summary><c>AppointmentApproveDate</c>, or the creation time when no approval date was stored.</summary>
    public DateTime ApprovedAt { get; init; }
}

/// <summary>One office's section of the missing-intake report.</summary>
public sealed class CaseTrackerMissingIntakeOffice
{
    public Guid OfficeId { get; init; }

    public string OfficeName { get; init; } = string.Empty;

    /// <summary>True when the office could not be read; its lists are then empty and say nothing.</summary>
    public bool Failed { get; init; }

    /// <summary>When the office first wrote an intake row, in any state. Null: it never has.</summary>
    public DateTime? FirstIntakeRowAt { get; init; }

    /// <summary>Every likely-lost appointment, newest approval first. Never truncated.</summary>
    public IReadOnlyList<CaseTrackerMissingIntakeItem> LikelyLost { get; init; } = [];

    /// <summary>Appointments whose packet set is still settling, newest approval first.</summary>
    public IReadOnlyList<CaseTrackerMissingIntakeItem> Settling { get; init; } = [];

    /// <summary>
    /// Appointments approved before the office's first intake row, newest approval first, at most
    /// <see cref="CaseTrackerMissingIntakeReporter.BeforeIntegrationListLimit"/>.
    /// </summary>
    public IReadOnlyList<CaseTrackerMissingIntakeItem> BeforeIntegration { get; init; } = [];

    /// <summary>The true number approved before the first intake row, whatever the list holds.</summary>
    public int BeforeIntegrationCount { get; init; }
}
