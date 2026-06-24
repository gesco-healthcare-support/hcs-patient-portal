using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- pure render-only service: load template by
/// code, substitute variables, return the rendered subject + bodies.
/// Separate from <see cref="INotificationDispatcher"/> so callers that
/// want to inspect or further-transform the rendered output (preview UI,
/// in-app rendering) do not have to enqueue a delivery job.
///
/// <para>Template-resolution rules:</para>
/// <list type="bullet">
///   <item>Look up via
///     <c>INotificationTemplateRepository.FindByCodeAsync</c>; the
///     repository already filters by tenant scope (per the Phase 4
///     seed contract).</item>
///   <item>Throw <c>BusinessException(NotificationTemplateNotFound)</c>
///     when the row is missing or
///     <c>IsActive == false</c>. Per audit Q decision, NEW does NOT
///     fall back to a hardcoded body when the template is missing --
///     a missing template is a seeding bug we want to surface
///     loudly, not paper over with stale strings.</item>
/// </list>
/// </summary>
public interface INotificationTemplateRenderer
{
    Task<RenderedNotification> RenderAsync(
        string templateCode,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);
}
