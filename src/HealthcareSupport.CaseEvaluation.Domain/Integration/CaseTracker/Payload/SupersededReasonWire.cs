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

    public static string ToWire(AppointmentStatusType status) => status switch
    {
        AppointmentStatusType.RescheduledNoBill => Rescheduled,
        AppointmentStatusType.RescheduledLate => Rescheduled,
        _ => throw new ArgumentOutOfRangeException(
            nameof(status), status, "This status supersedes nothing, so it has no superseded-reason wire value."),
    };
}
