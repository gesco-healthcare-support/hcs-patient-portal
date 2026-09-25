using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Maps the OLD appointment's terminal status to WHY its case was superseded (phase 4e,
/// 2026-08-06).
///
/// <para>Why the cause is sent at all: <c>supersededByAppointmentId</c> alone says a successor
/// exists but not what kind. Inferring it would mean cross-referencing the successor's own links,
/// which requires that successor to have already arrived -- and a reschedule and a re-evaluation
/// are not the same relationship. A rescheduled appointment did NOT happen and IS replaced; a
/// re-evaluated one happened and is merely followed up, leaving the original untouched.</para>
///
/// <para>Reads <see cref="AppointmentStatusType"/> rather than a dedicated enum because the
/// terminal status already IS the cause -- exactly as <see cref="BillingStatusWire"/> derives
/// billing intent from the same value. A one-member enum would duplicate a distinction the status
/// already carries.</para>
///
/// <para>THROWS on an unmapped status, following <see cref="EvaluationKindWire"/> rather than
/// <see cref="BillingStatusWire"/>: this is only called once a successor is known to exist, so an
/// unmapped status means the CALLER's guard is wrong. A confidently wrong cause is worse for the
/// receiver than no cause at all.</para>
/// </summary>
public static class SupersededReasonWire
{
    /// <summary>The appointment was rescheduled: it did not happen, and a replacement was created.</summary>
    public const string Rescheduled = "RESCHEDULED";

    /// <summary>The patient did not arrive, and the appointment was later booked again.</summary>
    public const string NoShow = "NO_SHOW";

    /// <summary>
    /// The patient arrived but was not evaluated, and the appointment was later booked again.
    /// </summary>
    public const string NotSeen = "NOT_SEEN";

    /// <summary>
    /// The appointment was cancelled and later booked again. Both billing outcomes collapse to
    /// one value: this field explains WHY a case closed, not what it cost, and the billing
    /// split already travels on <c>billingStatus</c>. Mirrors how the two rescheduled statuses
    /// collapse to <see cref="Rescheduled"/>.
    /// </summary>
    public const string Cancelled = "CANCELLED";

    public static string ToWire(AppointmentStatusType status) => status switch
    {
        AppointmentStatusType.RescheduledNoBill => Rescheduled,
        AppointmentStatusType.RescheduledLate => Rescheduled,
        // Item 4 (2026-08-17) -- the re-book flow. A re-booked appointment gains a successor
        // exactly as a rescheduled one does, so AppointmentCoreResolver calls this with the
        // source's status. Before these four, every re-book-eligible status threw here.
        // NoShow / NotSeen are never pushed (the Case Tracker authors them, so echoing them
        // back tells them only what they already know), which made the defect latent for those
        // two -- but a CANCELLED appointment IS pushed, so a re-book from one would have broken
        // that appointment's push outright.
        AppointmentStatusType.NoShow => NoShow,
        AppointmentStatusType.NotSeen => NotSeen,
        AppointmentStatusType.CancelledNoBill => Cancelled,
        AppointmentStatusType.CancelledLate => Cancelled,
        _ => throw new ArgumentOutOfRangeException(
            nameof(status), status, "This status supersedes nothing, so it has no superseded-reason wire value."),
    };
}
