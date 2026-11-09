using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

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
///
/// <para>Office name (issue #1014): the renderer fills <c>##ClinicName##</c> itself from the tenant
/// STORE, keyed by the current office id. Handlers used to pass <c>ICurrentTenant.Name</c>, but ABP's
/// <c>Change(id)</c> sets only the id and leaves the name null, so every office-scoped email went out
/// with a blank office name. Resolving it here, once, covers every handler, including ones that never
/// passed the key.</para>
///
/// <para>2026-11-09: the name is the office's PRACTICE name (<see cref="IOfficeDisplayNameResolver"/>:
/// the letterhead's practice name, else its doctor), with the tenant store's name -- the web-address
/// slug, e.g. "falkinstein" -- only as the last resort.</para>
/// </summary>
public class NotificationTemplateRenderer : INotificationTemplateRenderer, ITransientDependency
{
    /// <summary>The template variable holding the office (tenant) display name.</summary>
    public const string ClinicNameVariable = "ClinicName";

    private const string ClinicNamePlaceholder = "##" + ClinicNameVariable + "##";

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IOfficeDisplayNameResolver _officeDisplayNames;
    private readonly ICurrentTenant _currentTenant;

    public NotificationTemplateRenderer(
        INotificationTemplateRepository templateRepository,
        IOfficeDisplayNameResolver officeDisplayNames,
        ICurrentTenant currentTenant)
    {
        _templateRepository = templateRepository;
        _officeDisplayNames = officeDisplayNames;
        _currentTenant = currentTenant;
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

        variables = await WithClinicNameAsync(template, variables);

        var subject = TemplateVariableSubstitutor.Substitute(template.Subject, variables);
        var bodyEmail = TemplateVariableSubstitutor.Substitute(template.BodyEmail, variables);
        var bodySms = string.IsNullOrWhiteSpace(template.BodySms)
            ? null
            : TemplateVariableSubstitutor.Substitute(template.BodySms, variables);

        return new RenderedNotification(subject, bodyEmail, bodySms);
    }

    /// <summary>
    /// Returns <paramref name="variables"/> with <c>ClinicName</c> set to the current office's practice
    /// name. A non-empty name the caller passed wins; a template without the placeholder skips the
    /// lookup entirely. Outside an office scope, or for an office with no name at all, the name is
    /// empty -- the same blank the substitutor gives a missing key, never an exception, because a
    /// missing office name must not stop the email.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, object?>> WithClinicNameAsync(
        NotificationTemplate template,
        IReadOnlyDictionary<string, object?> variables)
    {
        if (!UsesClinicName(template))
        {
            return variables;
        }

        if (variables.TryGetValue(ClinicNameVariable, out var passed)
            && passed is string passedName
            && !string.IsNullOrEmpty(passedName))
        {
            return variables;
        }

        var officeName = _currentTenant.Id.HasValue
            ? (await _officeDisplayNames.ResolveCurrentAsync()).PracticeName
            : string.Empty;

        return new Dictionary<string, object?>(variables, StringComparer.Ordinal)
        {
            [ClinicNameVariable] = officeName,
        };
    }

    private static bool UsesClinicName(NotificationTemplate template) =>
        Contains(template.Subject) || Contains(template.BodyEmail) || Contains(template.BodySms);

    private static bool Contains(string? text) =>
        text != null && text.Contains(ClinicNamePlaceholder, StringComparison.Ordinal);
}
