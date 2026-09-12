using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- default impl of
/// <see cref="INotificationTemplateRenderer"/>. Loads via
/// <see cref="INotificationTemplateRepository.FindByCodeAsync"/>,
/// substitutes through <see cref="TemplateVariableSubstitutor"/>, returns
/// the rendered output.
///
/// <para>Tenant scope: handled upstream by the repository's
/// <c>ICurrentTenant</c>-aware query filter -- we don't need to
/// <c>using (CurrentTenant.Change(...))</c> here because every Phase 1
/// caller (per-feature handler) already runs inside a tenant-scoped
/// unit of work.</para>
/// </summary>
public class NotificationTemplateRenderer : INotificationTemplateRenderer, ITransientDependency
{
    private readonly INotificationTemplateRepository _templateRepository;

    public NotificationTemplateRenderer(INotificationTemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public virtual async Task<RenderedNotification> RenderAsync(
        string templateCode,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));

        var template = await _templateRepository.FindByCodeAsync(templateCode, cancellationToken);
        if (template == null || !template.IsActive)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
                .WithData("templateCode", templateCode);
        }

        var subject = TemplateVariableSubstitutor.Substitute(template.Subject, variables);
        var bodyEmail = TemplateVariableSubstitutor.Substitute(template.BodyEmail, variables);
        var bodySms = string.IsNullOrWhiteSpace(template.BodySms)
            ? null
            : TemplateVariableSubstitutor.Substitute(template.BodySms, variables);

        return new RenderedNotification(subject, bodyEmail, bodySms);
    }
}
