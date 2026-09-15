using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when an external user (or unauthenticated
/// verification-code recipient) uploads a document against an appointment.
/// Mirrors OLD <c>EmailTemplate.PatientDocumentUploaded</c> /
/// <c>EmailTemplate.PatientNewDocumentUploaded</c> trigger points.
///
/// <para>Phase 14 (Document review) emits this from
/// <c>AppointmentDocumentsAppService.UploadAsync</c> +
/// <c>UploadByVerificationCodeAsync</c>. Forward-declared here at Phase 18
/// so Session B's notification-handler PRs can subscribe without blocking
/// on Session A's domain work.</para>
/// </summary>
public class AppointmentDocumentUploadedEto
{
    public Guid AppointmentId { get; set; }

    public Guid AppointmentDocumentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>true for ad-hoc uploads, false for package-doc uploads.</summary>
    public bool IsAdHoc { get; set; }

    /// <summary>true for AME Joint Declaration Form uploads.</summary>
    public bool IsJointDeclaration { get; set; }

    /// <summary>Identity user who uploaded; null for unauthenticated verification-code path.</summary>
    public Guid? UploadedByUserId { get; set; }

    public DateTime OccurredAt { get; set; }
}
