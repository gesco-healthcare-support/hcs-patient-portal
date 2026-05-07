using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class AppointmentDefenseAttorneyManager : DomainService
{
    protected IAppointmentDefenseAttorneyRepository _appointmentDefenseAttorneyRepository;

    public AppointmentDefenseAttorneyManager(IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository)
    {
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
    }

    public virtual async Task<AppointmentDefenseAttorney> CreateAsync(Guid appointmentId, Guid defenseAttorneyId, Guid? identityUserId)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(defenseAttorneyId, nameof(defenseAttorneyId));
        var appointmentDefenseAttorney = new AppointmentDefenseAttorney(GuidGenerator.Create(), appointmentId, defenseAttorneyId, identityUserId);
        return await _appointmentDefenseAttorneyRepository.InsertAsync(appointmentDefenseAttorney);
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
        return await _appointmentDefenseAttorneyRepository.UpdateAsync(appointmentDefenseAttorney);
    }
}
