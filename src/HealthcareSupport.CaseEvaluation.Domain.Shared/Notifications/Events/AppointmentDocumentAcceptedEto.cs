using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when clinic staff accepts an uploaded
/// appointment document. Mirrors OLD <c>EmailTemplate.PatientDocumentAccepted</c>
/// + <c>JointAgreementLetterAccepted</c> + <c>PatientNewDocumentAccepted</c>
/// trigger points.
///
/// <para>Phase 14 (Document review) emits this from
/// <c>AppointmentDocumentsAppService.AcceptDocumentAsync</c>.</para>
/// </summary>
public class AppointmentDocumentAcceptedEto
{
    public Guid AppointmentId { get; set; }

    public Guid AppointmentDocumentId { get; set; }

    public Guid? TenantId { get; set; }

    public bool IsAdHoc { get; set; }

    public bool IsJointDeclaration { get; set; }

    public Guid AcceptedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
