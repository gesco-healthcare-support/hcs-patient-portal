using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Applies an attendance outcome reported by the Case Tracker (phase 5, 2026-08-07): the patient did
/// not arrive (<c>NoShow</c>), or arrived but was not evaluated (<c>NotSeen</c>). Intake staff record
/// these on THEIR side, so this is the only way either status enters the portal.
///
/// <para>A DOMAIN service, deliberately, for the same reason as
/// <see cref="CaseTrackerReconcileService"/>:
/// <c>ConventionalControllers.Create(typeof(CaseEvaluationApplicationModule).Assembly)</c> in the
/// host module auto-exposes every application service over HTTP, so an app service here would gain a
/// SECOND route that skips the token check the controller performs. Domain services are not exposed.
/// </para>
///
/// <para>The office must be supplied by the caller because the portal is database-per-office and this
/// request carries no other tenant signal -- no logged-in user, and a shared host name rather than an
/// office subdomain.</para>
/// </summary>
public class CaseTrackerAttendanceService : ITransientDependency
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly AppointmentManager _appointmentManager;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<CaseTrackerAttendanceService> _logger;

    public CaseTrackerAttendanceService(
        IAppointmentRepository appointmentRepository,
        AppointmentManager appointmentManager,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<CaseTrackerAttendanceService> logger)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>
    /// Records <paramref name="outcome"/> against the appointment.
    ///
    /// <para>Publishing the status change is what makes the ALREADY-BUILT staff notification fire,
    /// so this phase adds no notification code of its own.</para>
    ///
    /// <para>Identifiers only in every log line -- never a patient field.</para>
    /// </summary>
    /// <param name="outcome">
    /// Must be <c>NoShow</c> or <c>NotSeen</c>. The controller guarantees this by parsing the wire
    /// value through <see cref="AttendanceOutcomeWire"/>, so anything else is a programming error and
    /// is allowed to surface rather than be quietly turned into a not-found.
    /// </param>
    public virtual async Task<CaseTrackerAttendanceOutcome> ApplyAsync(
        Guid tenantId,
        Guid appointmentId,
        AppointmentStatusType outcome,
        CancellationToken cancellationToken = default)
    {
        // Enter the office FIRST. Both the setting read and the appointment lookup must resolve
        // against that office's database -- and reading the setting inside the scope is what makes
        // the gate per-office, so a live office cannot expose one that is still switched off.
        using (_currentTenant.Change(tenantId))
        {
            try
            {
                if (!await _settingProvider.IsTrueAsync(
                        CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled))
                {
                    // WARNING, not Debug. The bodyless 404 is deliberately ambiguous so a token
                    // holder cannot enumerate offices, but that reasoning is about what the CALLER
                    // is told. It does not apply to our own record, and Debug is not emitted at all
                    // in production (Release selects MinimumLevel.Information), so this arm used to
                    // leave no trace anywhere. An authenticated partner reporting against an office
                    // we have switched off is a live disagreement between the two systems.
                    _logger.LogWarning(
                        "CaseTrackerAttendanceService: office {TenantId} has the Case Tracker integration disabled; attendance report refused.",
                        tenantId);
                    return CaseTrackerAttendanceOutcome.NotFound;
                }

                var appointment = await _appointmentRepository.FindAsync(
                    appointmentId, cancellationToken: cancellationToken);
                if (appointment == null)
                {
                    // WARNING for the same reason as the arm above: the ambiguity is owed to the
                    // caller, not to us. This is the arm that strands an appointment -- the report
                    // is discarded, the status never moves, and nobody in either system has
                    // anything to look at.
                    _logger.LogWarning(
                        "CaseTrackerAttendanceService: appointment {AppointmentId} not found in office {TenantId}; answering not-found.",
                        appointmentId, tenantId);
                    return CaseTrackerAttendanceOutcome.NotFound;
                }

                // Idempotency (T6). A retry carrying the SAME outcome is a no-op success: returning
                // early means no second status-changed event, so no duplicate staff email. Checked
                // before the state machine because NoShow permits no transitions at all -- letting
                // it through would report a retry as a conflict.
                if (appointment.AppointmentStatus == outcome)
                {
                    // Stays DEBUG deliberately, unlike the two arms above: this is a successful
                    // retry of a report we already applied, which is ordinary and expected traffic.
                    // Raising it would bury the two arms that mean something went wrong.
                    _logger.LogDebug(
                        "CaseTrackerAttendanceService: appointment {AppointmentId} already carries {Outcome}; no-op.",
                        appointmentId, outcome);
                    return CaseTrackerAttendanceOutcome.Applied;
                }

                try
                {
                    await _appointmentManager.MarkAttendanceOutcomeAsync(
                        appointmentId, outcome, reason: null, actingUserId: null);
                    return CaseTrackerAttendanceOutcome.Applied;
                }
                catch (BusinessException ex)
                    when (ex.Code == CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition)
                {
                    // The state machine is the single source of truth for what may follow what, so
                    // the conflict arm asks IT rather than restating the rule. Caught NARROWLY and
                    // ahead of the catch-all below: widen either one and this becomes a 404, which
                    // is exactly the failure the three-way result exists to prevent.
                    _logger.LogInformation(
                        "CaseTrackerAttendanceService: appointment {AppointmentId} in office {TenantId} cannot take {Outcome} from its current status; answering conflict.",
                        appointmentId, tenantId, outcome);
                    return CaseTrackerAttendanceOutcome.Conflict(appointment.AppointmentStatus);
                }
            }
            catch (Exception ex)
            {
                // Everything else collapses to not-found. An unrecognised office fails when its
                // connection string is resolved; that must not become a 500, because a 500 tells the
                // caller it guessed a real office.
                _logger.LogWarning(
                    ex,
                    "CaseTrackerAttendanceService: could not apply {Outcome} to appointment {AppointmentId} for office {TenantId}; answering not-found.",
                    outcome, appointmentId, tenantId);
                return CaseTrackerAttendanceOutcome.NotFound;
            }
        }
    }
}
