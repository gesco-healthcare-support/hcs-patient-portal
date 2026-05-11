using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- top-level facade for sending a templated
/// notification. Resolves the template by code, substitutes
/// <c>##Var##</c> placeholders against the supplied variables, and
/// enqueues one email job per recipient via the existing
/// <c>SendAppointmentEmailJob</c> Hangfire pipeline. SMS leg dispatches
/// synchronously through ABP's <see cref="Volo.Abp.Sms.ISmsSender"/>
/// when the template carries a non-empty <c>BodySms</c> AND the
/// recipient supplies a phone number.
///
/// <para>Per-feature handlers (Phase 11/12/14/17) replace ~30 lines of
/// inline HTML with a single call to this dispatcher. Strict-parity
/// constraint: the rendered Subject + BodyEmail must contain the OLD
/// content verbatim once the template seed migrates OLD's HTML bodies
/// (Phase 4 work).</para>
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Render the template identified by <paramref name="templateCode"/>
    /// (one of <c>NotificationTemplateConsts.Codes.*</c>) with
    /// <paramref name="variables"/> as the substitution map, then
    /// enqueue an email + send an SMS for every recipient that has the
    /// matching channel configured.
    /// </summary>
    /// <param name="templateCode">
    /// Template-code key resolved via
    /// <c>INotificationTemplateRepository.FindByCodeAsync</c>. Throws
    /// <c>BusinessException(NotificationTemplateNotFound)</c> if the
    /// template row is missing or inactive.
    /// </param>
    /// <param name="recipients">
    /// One row per recipient. Empty list short-circuits without
    /// rendering the template (zero-cost no-op).
    /// </param>
    /// <param name="variables">
    /// Map of <c>"VariableName" -> value</c>; the substitutor wraps each
    /// key in <c>##Key##</c> at render time. Null-valued entries render
    /// as empty strings (mirrors OLD line 230-233 fallback). Pass an
    /// empty dictionary if the template has no variables.
    /// </param>
    /// <param name="contextTag">
    /// Free-form correlation tag stamped on the
    /// <c>SendAppointmentEmailArgs.Context</c> field for log
    /// correlation. Convention: <c>"{Phase}/{TemplateCode}/{EntityId}"</c>.
    /// </param>
    /// <param name="packetRef">
    /// Phase 4 (Category 4, 2026-05-10): when set, applied to EVERY recipient
    /// in this dispatch. <c>SendAppointmentEmailJob</c> fetches the rendered
    /// DOCX bytes from <c>AppointmentPacketsContainer</c> at send time, attaches
    /// via MailMessage, and calls
    /// <c>IPacketAttachmentProvider.NotifySendCompletedAsync</c> after the
    /// transport returns. Null = no attachment (normal email path).
    /// </param>
    Task DispatchAsync(
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null,
        CancellationToken cancellationToken = default);
}
