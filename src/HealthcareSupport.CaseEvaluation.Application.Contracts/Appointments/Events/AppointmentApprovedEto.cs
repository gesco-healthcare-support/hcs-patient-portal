using System;

namespace HealthcareSupport.CaseEvaluation.Appointments.Events;

/// <summary>
/// Phase 12 (2026-05-04) -- raised by
/// <c>AppointmentApprovalAppService.ApproveAppointmentAsync</c> after the
/// state-machine transition has succeeded and the supplemental fields
/// (the approver comment) have been persisted. Until 2026-11-02 it also
/// carried the responsible user; that concept was retired. Mirrors OLD's approval trigger point in
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
/// </list>
///
/// <para>Distinct from Session A's existing
/// <c>AppointmentStatusChangedEto</c>: this Eto carries the Phase
/// 12-specific approval context (<see cref="ApprovedByUserId"/>)
/// that <c>AppointmentStatusChangedEto</c> intentionally omits because it
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

    public Guid ApprovedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
