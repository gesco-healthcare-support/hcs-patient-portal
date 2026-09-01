namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Sanitizes IT-Admin-authored email-body HTML at the write boundary
/// (#6, 2026-06-19). The notification renderer returns stored bodies
/// verbatim, so the value persisted by
/// <see cref="NotificationTemplatesAppService.UpdateAsync"/> must already
/// be XSS-safe -- sanitize once on write, not on every send.
/// </summary>
public interface IEmailBodySanitizer
{
    /// <summary>
    /// Returns an XSS-safe copy of <paramref name="html"/>, stripping
    /// scripts, event handlers, and disallowed URL schemes while preserving
    /// common email formatting and bare <c>##Token##</c> merge placeholders
    /// in link/image URLs. Null or empty input is returned unchanged.
    /// </summary>
    string Sanitize(string html);
}
