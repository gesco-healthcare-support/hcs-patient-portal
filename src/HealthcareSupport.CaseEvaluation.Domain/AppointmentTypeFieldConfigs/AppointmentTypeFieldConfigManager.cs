using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

/// <summary>
/// W2-5: thin Manager for AppointmentTypeFieldConfig.
/// Validates inputs and delegates persistence to the repository so AppService
/// callers never construct entities directly.
/// </summary>
public class AppointmentTypeFieldConfigManager : DomainService
{
    private readonly IRepository<AppointmentTypeFieldConfig, Guid> _repository;

    public AppointmentTypeFieldConfigManager(IRepository<AppointmentTypeFieldConfig, Guid> repository)
    {
        _repository = repository;
    }

    public virtual async Task<AppointmentTypeFieldConfig> CreateAsync(
        Guid? tenantId,
        Guid appointmentTypeId,
        string fieldName,
        bool hidden,
        bool readOnly,
        string? defaultValue,
        bool required = false)
    {
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        Check.NotNullOrWhiteSpace(fieldName, nameof(fieldName));

        var entity = new AppointmentTypeFieldConfig(
            GuidGenerator.Create(),
            tenantId,
            appointmentTypeId,
            fieldName,
            hidden,
            readOnly,
            defaultValue,
            required);
        return await _repository.InsertAsync(entity);
    }

    public virtual async Task<AppointmentTypeFieldConfig> UpdateAsync(
        Guid id,
        bool hidden,
        bool readOnly,
        string? defaultValue,
        string? concurrencyStamp = null,
        bool required = false)
    {
        var entity = await _repository.GetAsync(id);
        entity.Hidden = hidden;
        entity.ReadOnly = readOnly;
        entity.Required = required;
        entity.DefaultValue = defaultValue;
        entity.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _repository.UpdateAsync(entity);
    }
}
