using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class AppointmentLanguageManager : DomainService
{
    protected IAppointmentLanguageRepository _appointmentLanguageRepository;
    protected IRepository<Patient, Guid> _patientRepository;

    public AppointmentLanguageManager(
        IAppointmentLanguageRepository appointmentLanguageRepository,
        IRepository<Patient, Guid> patientRepository)
    {
        _appointmentLanguageRepository = appointmentLanguageRepository;
        _patientRepository = patientRepository;
    }

    public virtual async Task<AppointmentLanguage> CreateAsync(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentLanguageConsts.NameMaxLength);
        // Admin-created rows are never system rows.
        var appointmentLanguage = new AppointmentLanguage(GuidGenerator.Create(), name, isSystem: false);
        return await _appointmentLanguageRepository.InsertAsync(appointmentLanguage);
    }

    public virtual async Task<AppointmentLanguage> UpdateAsync(Guid id, string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentLanguageConsts.NameMaxLength);
        var appointmentLanguage = await _appointmentLanguageRepository.GetAsync(id);
        EnsureNotSystem(appointmentLanguage);
        appointmentLanguage.Name = name;
        return await _appointmentLanguageRepository.UpdateAsync(appointmentLanguage);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _appointmentLanguageRepository.FindAsync(id);
        if (entity == null)
        {
            return;
        }
        EnsureNotSystem(entity);
        await EnsureNotInUseAsync(id);
        await _appointmentLanguageRepository.DeleteAsync(entity);
    }

    /// <summary>
    /// Block deleting a language still referenced by a Patient (via
    /// <see cref="Patient.AppointmentLanguageId"/>). Mirrors the
    /// AppointmentDocumentType in-use guard.
    /// </summary>
    private async Task EnsureNotInUseAsync(Guid id)
    {
        if (await _patientRepository.AnyAsync(p => p.AppointmentLanguageId == id))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentLanguageInUse);
        }
    }

    private static void EnsureNotSystem(AppointmentLanguage entity)
    {
        if (entity.IsSystem)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentLanguageSystemReadOnly);
        }
    }
}
