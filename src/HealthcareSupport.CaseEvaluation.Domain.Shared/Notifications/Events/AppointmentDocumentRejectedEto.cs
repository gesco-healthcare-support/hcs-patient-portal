using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when clinic staff rejects an uploaded
/// appointment document with rejection notes. Mirrors OLD
/// <c>EmailTemplate.PatientDocumentRejected</c> +
/// <c>JointAgreementLetterRejected</c> +
/// <c>EmailTemplate.PatientNewDocumentRejected</c> +
/// <c>TemplateCode.RejectedPackageDocument</c> +
/// <c>TemplateCode.RejectedJointDeclarationDocument</c> trigger points.
///
/// <para>Phase 14 (Document review) emits this from
/// <c>AppointmentDocumentsAppService.RejectDocumentAsync</c>.
/// <see cref="RejectionNotes"/> is required (validated upstream by the
/// AppService) so subscribers can render it directly into the email body.</para>
/// </summary>
public class AppointmentDocumentRejectedEto
{
    public Guid AppointmentId { get; set; }

    public Guid AppointmentDocumentId { get; set; }

    public Guid? TenantId { get; set; }

    public bool IsAdHoc { get; set; }

    public bool IsJointDeclaration { get; set; }

    public string RejectionNotes { get; set; } = string.Empty;

    public Guid RejectedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
