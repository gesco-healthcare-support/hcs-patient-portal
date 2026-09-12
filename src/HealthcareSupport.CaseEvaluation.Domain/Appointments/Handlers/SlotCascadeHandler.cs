using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace HealthcareSupport.CaseEvaluation.Appointments.Handlers;

/// <summary>
/// 2026-05-15 (slot rework plan 3) -- under capacity-aware booking the slot's
/// <c>BookingStatusId</c> is a manual-close override, not a derived value.
/// The previous 14-state Appointment-status -> slot-status mapping is gone;
/// this handler is now a log-only stub. The subscription stays so future
/// plans can re-introduce side effects without re-wiring DI; downstream
/// notification + audit handlers continue to receive
/// <see cref="AppointmentStatusChangedEto"/> unmodified.
///
/// The active-appointment-count probe
/// (<c>IAppointmentRepository.GetActiveCountForSlotAsync</c>) is the
/// authoritative source for "is this slot bookable" now.
/// </summary>
public class SlotCascadeHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly ILogger<SlotCascadeHandler> _logger;

    public SlotCascadeHandler(ILogger<SlotCascadeHandler> logger)
    {
        _logger = logger;
    }

    public virtual Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        _logger.LogDebug(
            "SlotCascadeHandler: appointment {AppointmentId} transitioned {From} -> {To}; no slot mutation needed under capacity model.",
            eventData.AppointmentId,
            eventData.FromStatus,
            eventData.ToStatus);
        return Task.CompletedTask;
    }
}
