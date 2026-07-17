using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Issue #3 (2026-07-16) -- subscribes to <see cref="AppointmentAccessorAddedEto"/>
/// (an accessor who ALREADY has a tenant account was added to an appointment) and
/// dispatches the "you have been added" email through <see cref="INotificationDispatcher"/>
/// (template <c>AccessorAppointmentAdded</c>), routed via the notification outbox like
/// every other email so it is durable/idempotent.
///
/// <para>Unlike <see cref="AccessorInvitedEmailHandler"/> there is NO password-setup
/// link -- the recipient already has an account and signs in at the portal URL. Fires
/// for every appointment type, including re-evaluation, where carried-forward accessors
/// always take the existing-account path (and previously received nothing).</para>
/// </summary>
public class AccessorAddedEmailHandler :
    ILocalEventHandler<AppointmentAccessorAddedEto>,
    ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly DocumentEmailContextResolver _contextResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AccessorAddedEmailHandler> _logger;
    private readonly ITenantStore _tenantStore;

    public AccessorAddedEmailHandler(
        INotificationDispatcher dispatcher,
        DocumentEmailContextResolver contextResolver,
        ICurrentTenant currentTenant,
        ILogger<AccessorAddedEmailHandler> logger,
        ITenantStore tenantStore)
    {
        _dispatcher = dispatcher;
        _contextResolver = contextResolver;
        _currentTenant = currentTenant;
        _logger = logger;
        _tenantStore = tenantStore;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentAccessorAddedEto eventData)
    {
        if (eventData == null)
        {
            return;
        }

        using (_currentTenant.Change(eventData.TenantId))
        {
            var ctx = await _contextResolver.ResolveAsync(eventData.AppointmentId, appointmentDocumentId: null);
            if (ctx == null)
            {
                _logger.LogWarning(
                    "AccessorAddedEmailHandler: appointment {AppointmentId} not found; skipping.",
                    eventData.AppointmentId);
                return;
            }

            // The practice name for the template's "... at {ClinicName}" line. Resolved
            // from the tenant store because ICurrentTenant.Name is null inside the
            // Change(TenantId) scope (same fix as AccessorInvitedEmailHandler).
            var practiceName = eventData.TenantId.HasValue
                ? (await _tenantStore.FindAsync(eventData.TenantId.Value))?.Name
                : null;

            var recipients = new[]
            {
                new NotificationRecipient(
                    email: eventData.Email,
                    role: AccessorInvitedEmailHandler.MapRoleName(eventData.RoleName),
                    isRegistered: true),
            };

            var variables = BuildAddedEmailVariables(ctx, practiceName, eventData.Email);

            await _dispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.AccessorAppointmentAdded,
                recipients: recipients,
                variables: variables,
                contextTag: $"AccessorAdded/{eventData.AccessorUserId}");
        }
    }

    /// <summary>
    /// Builds the "you were added" template variable bag. Existing account -> no
    /// password-setup <c>##URL##</c>; the body links to the portal via
    /// <c>##PortalUrl##</c> and shows the recipient's <c>##Email##</c>. The "Email" key
    /// is BARE (<see cref="TemplateVariableSubstitutor"/> wraps each key as
    /// "##" + key + "##"). Internal for render-test coverage.
    /// </summary>
    internal static Dictionary<string, object?> BuildAddedEmailVariables(
        DocumentEmailContext ctx, string? practiceName, string accessorEmail)
    {
        return new Dictionary<string, object?>(
            DocumentNotificationContext.BuildVariables(
                patientFirstName: ctx.PatientFirstName,
                patientLastName: ctx.PatientLastName,
                patientEmail: ctx.PatientEmail,
                requestConfirmationNumber: ctx.RequestConfirmationNumber,
                appointmentDate: ctx.AppointmentDate,
                claimNumber: ctx.ClaimNumber,
                wcabAdj: ctx.WcabAdj,
                documentName: null,
                rejectionNotes: null,
                clinicName: practiceName,
                portalUrl: ctx.PortalBaseUrl))
        {
            ["Email"] = accessorEmail,
        };
    }
}
