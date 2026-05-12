using System;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Background-job payload for <c>SendAppointmentEmailJob</c>. Carries a fully
/// rendered email so the job worker only needs SMTP credentials, not domain
/// repositories.
/// </summary>
[Serializable]
public class SendAppointmentEmailArgs
{
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsBodyHtml { get; set; } = true;

    /// <summary>
    /// Free-text label for log correlation. Conventional shape:
    /// "Submission/Office/{appointmentId}", "Transition/Approved/{appointmentId}", etc.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// W2-10: party role tag attached when the recipient was resolved by
    /// <c>IAppointmentRecipientResolver</c>. Nullable for backward-compat with
    /// W1-2 single-recipient call sites that did not set a role. Future
    /// per-recipient template-layer changes branch on this field.
    /// </summary>
    public RecipientRole? Role { get; set; }

    /// <summary>
    /// S-6.1: true when an IdentityUser with this email already exists in the
    /// tenant under the matching role. False when the email was captured at
    /// booking time on the appointment row but no user has registered yet.
    /// Drives the per-recipient template branch in SubmissionEmailHandler:
    /// registered -> "log in to view" body; not registered -> "register as
    /// [role]" body with a pre-filled register URL.
    ///
    /// Defaults to true for backward-compat: any caller that did not run
    /// through the resolver's email-column walk (office mailbox, booker
    /// fallback paths) is implicitly a real user account.
    /// </summary>
    public bool IsRegistered { get; set; } = true;

    /// <summary>
    /// S-6.1: tenant name carried alongside the email so the
    /// "register as [role]" body can build a tenant-pre-filled register URL
    /// (`/Account/Register?__tenant=&lt;TenantName&gt;&amp;email=&lt;email&gt;`)
    /// without the handler needing a separate tenant lookup. Null for the
    /// host-tenant context (host should never receive party-email fan-out).
    /// </summary>
    public string? TenantName { get; set; }

    /// <summary>
    /// 2026-05-11 (Bug A fix): the originating tenant. The Hangfire worker
    /// must re-enter this tenant via <c>ICurrentTenant.Change</c> before
    /// it queries the packet row in <c>IPacketAttachmentProvider</c>,
    /// otherwise the automatic <c>IMultiTenant</c> filter at the host
    /// level excludes the row and the job logs "packet is not Generated;
    /// skipping" even when the row is fully generated. Null = host scope
    /// (the same fallback ABP's default UoW uses when CurrentTenant is
    /// unset).
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Phase 4 (Category 4, 2026-05-10): packet attachment reference. When set,
    /// <c>SendAppointmentEmailJob</c> fetches the rendered DOCX bytes via
    /// <c>IPacketAttachmentProvider.GetAttachmentAsync</c> at send time and
    /// attaches it as a MailMessage attachment. After successful send the job
    /// calls <c>IPacketAttachmentProvider.NotifySendCompletedAsync</c> so
    /// AttyCE-kind rows are pruned (Patient/Doctor rows persist).
    ///
    /// <para>Storing the reference (small Guid+enum) instead of the bytes
    /// keeps Hangfire serialized args lean -- a 1 MB DOCX would otherwise
    /// inflate the job table per recipient. Bytes already live in
    /// <c>AppointmentPacketsContainer</c> blob storage.</para>
    /// </summary>
    public PacketAttachmentRef? PacketRef { get; set; }
}

/// <summary>
/// Phase 4 (Category 4, 2026-05-10) -- reference to a packet attachment
/// the email job should fetch from blob storage at send time. Carries
/// only identifiers so the serialized Hangfire job stays small.
/// </summary>
[Serializable]
public class PacketAttachmentRef
{
    public Guid AppointmentId { get; set; }

    public Guid PacketId { get; set; }

    public PacketKind Kind { get; set; }
}
