using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyManager : DomainService
{
    protected IAppointmentApplicantAttorneyRepository _appointmentApplicantAttorneyRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;

    public AppointmentApplicantAttorneyManager(
        IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository)
    {
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _appointmentRepository = appointmentRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
    }

    public virtual async Task<AppointmentApplicantAttorney> CreateAsync(Guid appointmentId, Guid applicantAttorneyId, Guid? identityUserId)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(applicantAttorneyId, nameof(applicantAttorneyId));
        var appointmentApplicantAttorney = new AppointmentApplicantAttorney(GuidGenerator.Create(), appointmentId, applicantAttorneyId, identityUserId);
        var result = await _appointmentApplicantAttorneyRepository.InsertAsync(appointmentApplicantAttorney);
        await CaptureSnapshotAsync(appointmentId, applicantAttorneyId);
        return result;
    }

    public virtual async Task<AppointmentApplicantAttorney> UpdateAsync(Guid id, Guid appointmentId, Guid applicantAttorneyId, Guid? identityUserId, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(applicantAttorneyId, nameof(applicantAttorneyId));
        var appointmentApplicantAttorney = await _appointmentApplicantAttorneyRepository.GetAsync(id);
        appointmentApplicantAttorney.AppointmentId = appointmentId;
        appointmentApplicantAttorney.ApplicantAttorneyId = applicantAttorneyId;
        appointmentApplicantAttorney.IdentityUserId = identityUserId;
        appointmentApplicantAttorney.SetConcurrencyStampIfNotNull(concurrencyStamp);
        var result = await _appointmentApplicantAttorneyRepository.UpdateAsync(appointmentApplicantAttorney);
        await CaptureSnapshotAsync(appointmentId, applicantAttorneyId);
        return result;
    }

    /// <summary>
    /// #9 (2026-06-19): copy the (now-current) master name/firm/contact onto the
    /// appointment as a booking-time snapshot whenever the attorney is linked or
    /// re-set here. This is the single choke point booking, staff appointment-edit,
    /// and the external-signup auto-link all funnel through, so the snapshot stays in
    /// sync with appointment-side edits while a master self-edit never touches it.
    /// Best-effort: a missing appointment or master leaves the snapshot untouched (the
    /// read path falls back to the master join).
    /// </summary>
    private async Task CaptureSnapshotAsync(Guid appointmentId, Guid applicantAttorneyId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        var master = await _applicantAttorneyRepository.FindAsync(applicantAttorneyId);
        if (appointment == null || master == null)
        {
            return;
        }
        AttorneySnapshot.CaptureApplicant(appointment, master);
        await _appointmentRepository.UpdateAsync(appointment);
    }
}
