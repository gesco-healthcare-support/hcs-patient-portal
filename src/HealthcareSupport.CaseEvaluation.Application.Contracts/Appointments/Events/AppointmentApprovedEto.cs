using System;

namespace HealthcareSupport.CaseEvaluation.Appointments.Events;

/// <summary>
/// Phase 12 (2026-05-04) -- raised by
/// <c>AppointmentApprovalAppService.ApproveAppointmentAsync</c> after the
/// state-machine transition has succeeded and the supplemental fields
/// (<c>PrimaryResponsibleUserId</c> + patient-match decision) have been
/// persisted. Mirrors OLD's approval trigger point in
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:560-566
/// where <c>AddAppointmentDocumentsAndSendDocumentToEmail</c> + the
/// stakeholder email loop fire after the status flip + commit.
///
/// <para>Subscribers:</para>
/// <list type="bullet">
///   <item><c>PackageDocumentQueueHandler</c> (Phase 12 stub / Phase 14
///     impl) -- looks up the <c>PackageDetail</c> linked to
///     <see cref="AppointmentTypeId"/> and queues
///     <c>AppointmentDocument</c> rows in <c>Pending</c> status.</item>
///   <item>Future approval-email handler (Phase 14) -- reads
///     <see cref="PrimaryResponsibleUserId"/> for the responsible-user
///     email send and uses the appointment's stakeholder list for the
///     patient + stakeholder fan-out via Phase 18's
///     <c>INotificationDispatcher</c>.</item>
/// </list>
///
/// <para>Distinct from Session A's existing
/// <c>AppointmentStatusChangedEto</c>: this Eto carries the Phase
/// 12-specific approval context (<see cref="PrimaryResponsibleUserId"/>,
/// <see cref="PatientMatchOverridden"/>) that
/// <c>AppointmentStatusChangedEto</c> intentionally omits because it
/// fans out across many transitions. Subscribers needing only the
/// status flip continue to use <c>AppointmentStatusChangedEto</c>.</para>
/// </summary>
public class AppointmentApprovedEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>FK to the appointment type. Lets the package-doc-queue handler
    /// resolve the matching <c>PackageDetail</c> without re-fetching the appointment.</summary>
    public Guid AppointmentTypeId { get; set; }

    /// <summary>Internal staff user assigned as the primary responsible user. Required.</summary>
    public Guid PrimaryResponsibleUserId { get; set; }

    /// <summary>True when the staff approver chose to ignore the dedup match
    /// and create / link a new patient row. False when the staff accepted the
    /// existing match (the default).</summary>
    public bool PatientMatchOverridden { get; set; }

    public Guid ApprovedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
