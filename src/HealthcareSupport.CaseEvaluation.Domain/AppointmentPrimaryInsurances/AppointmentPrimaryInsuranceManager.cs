using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public class AppointmentPrimaryInsuranceManager : DomainService
{
    protected IRepository<AppointmentPrimaryInsurance, Guid> _repository;

    public AppointmentPrimaryInsuranceManager(IRepository<AppointmentPrimaryInsurance, Guid> repository)
    {
        _repository = repository;
    }

    public virtual async Task<AppointmentPrimaryInsurance> CreateAsync(
        Guid appointmentId,
        bool isActive,
        string? name = null,
        string? insuranceNumber = null,
        string? phoneNumber = null,
        string? faxNumber = null,
        string? street = null,
        string? city = null,
        string? zip = null,
        Guid? stateId = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.Length(name, nameof(name), AppointmentPrimaryInsuranceConsts.NameMaxLength, 0);
        Check.Length(insuranceNumber, nameof(insuranceNumber), AppointmentPrimaryInsuranceConsts.SuiteMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), AppointmentPrimaryInsuranceConsts.PhoneNumberMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), AppointmentPrimaryInsuranceConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), AppointmentPrimaryInsuranceConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), AppointmentPrimaryInsuranceConsts.CityMaxLength, 0);
        Check.Length(zip, nameof(zip), AppointmentPrimaryInsuranceConsts.ZipMaxLength, 0);

        var entity = new AppointmentPrimaryInsurance(GuidGenerator.Create(), appointmentId, isActive)
        {
            Name = name,
            Suite = insuranceNumber,
            PhoneNumber = phoneNumber,
            FaxNumber = faxNumber,
            Street = street,
            City = city,
            Zip = zip,
            StateId = stateId,
        };
        return await _repository.InsertAsync(entity);
    }

    public virtual async Task<AppointmentPrimaryInsurance> UpdateAsync(
        Guid id,
        Guid appointmentId,
        bool isActive,
        string? name = null,
        string? insuranceNumber = null,
        string? phoneNumber = null,
        string? faxNumber = null,
        string? street = null,
        string? city = null,
        string? zip = null,
        Guid? stateId = null,
        [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.Length(name, nameof(name), AppointmentPrimaryInsuranceConsts.NameMaxLength, 0);
        Check.Length(insuranceNumber, nameof(insuranceNumber), AppointmentPrimaryInsuranceConsts.SuiteMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), AppointmentPrimaryInsuranceConsts.PhoneNumberMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), AppointmentPrimaryInsuranceConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), AppointmentPrimaryInsuranceConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), AppointmentPrimaryInsuranceConsts.CityMaxLength, 0);
        Check.Length(zip, nameof(zip), AppointmentPrimaryInsuranceConsts.ZipMaxLength, 0);

        var entity = await _repository.GetAsync(id);
        entity.AppointmentId = appointmentId;
        entity.IsActive = isActive;
        entity.Name = name;
        entity.Suite = insuranceNumber;
        entity.PhoneNumber = phoneNumber;
        entity.FaxNumber = faxNumber;
        entity.Street = street;
        entity.City = city;
        entity.Zip = zip;
        entity.StateId = stateId;
        entity.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _repository.UpdateAsync(entity);
    }
}
