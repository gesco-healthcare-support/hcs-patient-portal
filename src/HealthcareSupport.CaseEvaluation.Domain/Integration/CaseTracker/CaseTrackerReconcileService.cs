using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Serves the reconcile read (contract section F): one appointment's complete intake payload, for the
/// Case Tracker to recover a dead-lettered push or refresh a case on open.
///
/// <para>A DOMAIN service, deliberately, not an application service.
/// <c>ConventionalControllers.Create(typeof(CaseEvaluationApplicationModule).Assembly)</c> in the host
/// module auto-exposes every app service over HTTP; an app service here would therefore gain a SECOND
/// route that skips the token check the controller performs. Domain services are not exposed, so the
/// token-gated controller is the only way in.</para>
///
/// <para>The office must be supplied by the caller because the portal is database-per-office and this
/// request has no other tenant signal -- no logged-in user, and a shared host name rather than an
/// office subdomain.</para>
/// </summary>
public class CaseTrackerReconcileService : ITransientDependency
{
    private readonly IIntakePayloadBuilder _payloadBuilder;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<CaseTrackerReconcileService> _logger;

    public CaseTrackerReconcileService(
        IIntakePayloadBuilder payloadBuilder,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<CaseTrackerReconcileService> logger)
    {
        _payloadBuilder = payloadBuilder;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>
    /// The appointment's envelope, or <c>null</c> when it cannot be served.
    ///
    /// <para>Null is deliberately AMBIGUOUS: unknown appointment, office switched off, unknown office
    /// and broken office all return it. The caller maps null to 404, so an anonymous holder of the
    /// token cannot use response codes to discover which appointments or which offices exist.</para>
    /// </summary>
    public virtual async Task<IntakeEnvelope?> GetAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        // Enter the office FIRST. Both the setting read and the payload queries must resolve against
        // that office's database -- and reading the setting inside the scope is what makes the gate
        // per-office, so a live office cannot expose one that is still switched off.
        using (_currentTenant.Change(tenantId))
        {
            try
            {
                if (!await _settingProvider.IsTrueAsync(
                        CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled))
                {
                    _logger.LogDebug(
                        "CaseTrackerReconcileService: office {TenantId} has the Case Tracker integration disabled; reconcile refused.",
                        tenantId);
                    return null;
                }

                return await _payloadBuilder.BuildAsync(appointmentId, cancellationToken);
            }
            catch (Exception ex)
            {
                // Everything collapses to null. An unknown appointment throws from the builder's
                // GetAsync, and an unrecognised office fails when its connection string is resolved;
                // neither may become a 500, because a 500 tells the caller it guessed a real office.
                // Identifiers only in the log -- never a patient field.
                _logger.LogWarning(
                    ex,
                    "CaseTrackerReconcileService: could not serve appointment {AppointmentId} for office {TenantId}; answering not-found.",
                    appointmentId, tenantId);
                return null;
            }
        }
    }
}
