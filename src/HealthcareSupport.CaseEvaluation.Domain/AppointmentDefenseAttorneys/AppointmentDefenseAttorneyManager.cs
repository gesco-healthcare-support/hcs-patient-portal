using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyManager : DomainService
{
    protected IAppointmentDefenseAttorneyRepository _appointmentDefenseAttorneyRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;

    public AppointmentDefenseAttorneyManager(
        IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository)
    {
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _appointmentRepository = appointmentRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
    }

    public virtual async Task<AppointmentDefenseAttorney> CreateAsync(Guid appointmentId, Guid defenseAttorneyId, Guid? identityUserId)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(defenseAttorneyId, nameof(defenseAttorneyId));
        var appointmentDefenseAttorney = new AppointmentDefenseAttorney(GuidGenerator.Create(), appointmentId, defenseAttorneyId, identityUserId);
        var result = await _appointmentDefenseAttorneyRepository.InsertAsync(appointmentDefenseAttorney);
        await CaptureSnapshotAsync(appointmentId, defenseAttorneyId);
        return result;
    }

    public virtual async Task<AppointmentDefenseAttorney> UpdateAsync(Guid id, Guid appointmentId, Guid defenseAttorneyId, Guid? identityUserId, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(defenseAttorneyId, nameof(defenseAttorneyId));
        var appointmentDefenseAttorney = await _appointmentDefenseAttorneyRepository.GetAsync(id);
        appointmentDefenseAttorney.AppointmentId = appointmentId;
        appointmentDefenseAttorney.DefenseAttorneyId = defenseAttorneyId;
        appointmentDefenseAttorney.IdentityUserId = identityUserId;
        appointmentDefenseAttorney.SetConcurrencyStampIfNotNull(concurrencyStamp);
        var result = await _appointmentDefenseAttorneyRepository.UpdateAsync(appointmentDefenseAttorney);
        await CaptureSnapshotAsync(appointmentId, defenseAttorneyId);
        return result;
    }

    /// <summary>
    /// #9 (2026-06-19): copy the (now-current) master name/firm/contact onto the
    /// appointment as a booking-time snapshot whenever the attorney is linked or
    /// re-set here. Single choke point for booking, staff appointment-edit, and the
    /// external-signup auto-link, so the snapshot stays in sync with appointment-side
    /// edits while a master self-edit never touches it. Best-effort: a missing
    /// appointment or master leaves the snapshot untouched (read falls back to join).
    /// </summary>
    private async Task CaptureSnapshotAsync(Guid appointmentId, Guid defenseAttorneyId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        var master = await _defenseAttorneyRepository.FindAsync(defenseAttorneyId);
        if (appointment == null || master == null)
        {
            return;
        }
        AttorneySnapshot.CaptureDefense(appointment, master);
        await _appointmentRepository.UpdateAsync(appointment);
    }
}
