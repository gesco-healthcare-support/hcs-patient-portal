using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Part 5 (2026-07-28) -- raised once per internal staff member when Case Tracker pushes have
/// dead-lettered in their office and nobody has been told yet.
///
/// <para>Carries a BATCH rather than a single failure on purpose. The likeliest cause of a dead letter
/// is systemic -- a wrong token, or their service being down -- which fails every queued row at once.
/// One email saying twelve failed is more useful than twelve emails, and far less likely to be muted.</para>
///
/// <para>Deliberately carries NO PHI: appointment id and confirmation number identify the case, and the
/// error text is the receiver's own response. No patient name, date of birth or document content.</para>
/// </summary>
public class CaseTrackerPushFailedEto
{
    public Guid? TenantId { get; set; }

    public string OfficeName { get; set; } = string.Empty;

    public Guid StaffUserId { get; set; }

    public string StaffEmail { get; set; } = string.Empty;

    public string? StaffFirstName { get; set; }

    /// <summary>Total dead letters in this batch, which may exceed <see cref="Failures"/>' length.</summary>
    public int FailureCount { get; set; }

    /// <summary>The listed failures. Capped, so this can be shorter than <see cref="FailureCount"/>.</summary>
    public List<CaseTrackerPushFailureSummary> Failures { get; set; } = new();

    public DateTime OccurredAt { get; set; }
}

/// <summary>One dead-lettered push, identified without any patient field.</summary>
public class CaseTrackerPushFailureSummary
{
    public Guid AppointmentId { get; set; }

    /// <summary>Human reference staff can search on, e.g. <c>A00065</c>.</summary>
    public string ConfirmationNumber { get; set; } = string.Empty;

    /// <summary><c>Intake</c> or <c>DocumentUpdate</c>.</summary>
    public string MessageType { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    /// <summary>The receiver's own error text, already truncated by the outbox row.</summary>
    public string? LastError { get; set; }
}
