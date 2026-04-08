using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class AppointmentLanguageManager : DomainService
{
    protected IAppointmentLanguageRepository _appointmentLanguageRepository;

    public AppointmentLanguageManager(IAppointmentLanguageRepository appointmentLanguageRepository)
    {
        _appointmentLanguageRepository = appointmentLanguageRepository;
    }

    public virtual async Task<AppointmentLanguage> CreateAsync(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentLanguageConsts.NameMaxLength);
        var appointmentLanguage = new AppointmentLanguage(GuidGenerator.Create(), name);
        return await _appointmentLanguageRepository.InsertAsync(appointmentLanguage);
    }

    public virtual async Task<AppointmentLanguage> UpdateAsync(Guid id, string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentLanguageConsts.NameMaxLength);
        var appointmentLanguage = await _appointmentLanguageRepository.GetAsync(id);
        appointmentLanguage.Name = name;
        return await _appointmentLanguageRepository.UpdateAsync(appointmentLanguage);
    }
}