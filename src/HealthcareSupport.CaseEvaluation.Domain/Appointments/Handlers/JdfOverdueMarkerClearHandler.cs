using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Appointments.Handlers;

/// <summary>
/// Clears <see cref="Appointment.JointDeclarationOverdueAt"/> when the missing Joint Declaration
/// Form finally arrives (2026-08-08).
///
/// <para>The marker exists to give staff a to-do list, since nothing cancels automatically any more.
/// A to-do that cannot be ticked off is worse than none: without this the flag would latch forever
/// and an appointment whose form arrived the next morning would still read "Joint Declaration Form
/// overdue" months later, which trains people to ignore the flag entirely.</para>
///
/// <para>Wired to the EVENT rather than written into the upload methods because there are two JDF
/// upload paths -- <c>UploadJointDeclarationAsync</c> (the booking attorney) and
/// <c>UploadByVerificationCodeAsync</c> (the emailed link) -- and both publish this event. One
/// subscriber covers both, and a future third path gets it for free.</para>
///
/// <para>Deliberately NOT symmetric with the stamping side: this only clears. If the uploaded form
/// is later REJECTED, the overdue state genuinely returns, and the job's own predicate already
/// ignores rejected documents -- so its next run re-stamps and staff are told again.</para>
/// </summary>
public class JdfOverdueMarkerClearHandler :
    ILocalEventHandler<AppointmentDocumentUploadedEto>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<JdfOverdueMarkerClearHandler> _logger;

    public JdfOverdueMarkerClearHandler(
        IRepository<Appointment, Guid> appointmentRepository,
        ICurrentTenant currentTenant,
        ILogger<JdfOverdueMarkerClearHandler> logger)
    {
        _appointmentRepository = appointmentRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(AppointmentDocumentUploadedEto eventData)
    {
        // Every document upload publishes this event; only the Joint Declaration Form clears the
        // marker. An ad-hoc or package document arriving says nothing about the JDF.
        if (eventData is not { IsJointDeclaration: true })
        {
            return;
        }

        // Local events default to firing on unit-of-work completion, by which point the ambient
        // office scope is not guaranteed. The event carries the office id precisely so the write
        // lands in the right database.
        using (_currentTenant.Change(eventData.TenantId))
        {
            var appointment = await _appointmentRepository.FindAsync(eventData.AppointmentId);
            if (appointment?.JointDeclarationOverdueAt == null)
            {
                // Not overdue (the common case -- most forms arrive on time), or the appointment is
                // gone. Either way there is nothing to clear and nothing worth logging.
                return;
            }

            appointment.JointDeclarationOverdueAt = null;
            await _appointmentRepository.UpdateAsync(appointment, autoSave: true);

            _logger.LogInformation(
                "JdfOverdueMarkerClearHandler: appointment {AppointmentId} (tenant {TenantId}) received its Joint Declaration Form; overdue marker cleared.",
                eventData.AppointmentId,
                eventData.TenantId);
        }
    }
}
