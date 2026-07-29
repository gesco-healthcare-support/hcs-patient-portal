using System;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// One outstanding dead letter, as the admin screen shows it.
///
/// <para>Carries NO patient field, deliberately. The confirmation number identifies the appointment for
/// a human, and the screen is an operations tool -- there is no reason for it to render PHI, and section
/// I2 of the integration contract requires it not to.</para>
/// </summary>
public class CaseTrackerDeadLetterDto
{
    public Guid Id { get; set; }

    /// <summary>Which office's database this row lives in. Needed to retry it.</summary>
    public Guid OfficeId { get; set; }

    public string OfficeName { get; set; } = string.Empty;

    public Guid AppointmentId { get; set; }

    /// <summary>Human reference staff can search on, e.g. <c>A00065</c>. Empty if the appointment is gone.</summary>
    public string ConfirmationNumber { get; set; } = string.Empty;

    /// <summary><c>Intake</c> or <c>DocumentUpdate</c>.</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>The relative path the push was addressed to; useful when diagnosing a 404.</summary>
    public string TargetPath { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    /// <summary>The receiver's own error text, already truncated when it was recorded.</summary>
    public string? LastError { get; set; }

    public DateTime FailedAt { get; set; }

    /// <summary>When staff were emailed about it; null if the alert has not run yet.</summary>
    public DateTime? AlertedAt { get; set; }
}

/// <summary>Outcome of a retry, so the screen can report what happened without a refetch.</summary>
public class CaseTrackerDeadLetterRetryResultDto
{
    /// <summary>The outbox row the retry created or collapsed onto.</summary>
    public Guid QueuedOutboxItemId { get; set; }

    /// <summary>The dead letter that was marked resolved.</summary>
    public Guid ResolvedOutboxItemId { get; set; }
}
