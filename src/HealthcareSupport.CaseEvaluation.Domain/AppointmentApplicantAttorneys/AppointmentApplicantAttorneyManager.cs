using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class AppointmentApplicantAttorneyManager : DomainService
{
    protected IAppointmentApplicantAttorneyRepository _appointmentApplicantAttorneyRepository;

    public AppointmentApplicantAttorneyManager(IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository)
    {
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
    }

    public virtual async Task<AppointmentApplicantAttorney> CreateAsync(Guid appointmentId, Guid applicantAttorneyId, Guid? identityUserId)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(applicantAttorneyId, nameof(applicantAttorneyId));
        var appointmentApplicantAttorney = new AppointmentApplicantAttorney(GuidGenerator.Create(), appointmentId, applicantAttorneyId, identityUserId);
        return await _appointmentApplicantAttorneyRepository.InsertAsync(appointmentApplicantAttorney);
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
        return await _appointmentApplicantAttorneyRepository.UpdateAsync(appointmentApplicantAttorney);
    }
}