using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public class AppointmentClaimExaminerManager : DomainService
{
    protected IRepository<AppointmentClaimExaminer, Guid> _repository;

    public AppointmentClaimExaminerManager(IRepository<AppointmentClaimExaminer, Guid> repository)
    {
        _repository = repository;
    }

    public virtual async Task<AppointmentClaimExaminer> CreateAsync(
        Guid appointmentInjuryDetailId,
        bool isActive,
        string? name = null,
        string? claimExaminerNumber = null,
        string? email = null,
        string? phoneNumber = null,
        string? fax = null,
        string? street = null,
        string? city = null,
        string? zip = null,
        Guid? stateId = null)
    {
        Check.NotNull(appointmentInjuryDetailId, nameof(appointmentInjuryDetailId));
        Check.Length(name, nameof(name), AppointmentClaimExaminerConsts.NameMaxLength, 0);
        Check.Length(claimExaminerNumber, nameof(claimExaminerNumber), AppointmentClaimExaminerConsts.SuiteMaxLength, 0);
        Check.Length(email, nameof(email), AppointmentClaimExaminerConsts.EmailMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), AppointmentClaimExaminerConsts.PhoneNumberMaxLength, 0);
        Check.Length(fax, nameof(fax), AppointmentClaimExaminerConsts.FaxMaxLength, 0);
        Check.Length(street, nameof(street), AppointmentClaimExaminerConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), AppointmentClaimExaminerConsts.CityMaxLength, 0);
        Check.Length(zip, nameof(zip), AppointmentClaimExaminerConsts.ZipMaxLength, 0);

        var entity = new AppointmentClaimExaminer(GuidGenerator.Create(), appointmentInjuryDetailId, isActive)
        {
            Name = name,
            Suite = claimExaminerNumber,
            Email = email,
            PhoneNumber = phoneNumber,
            Fax = fax,
            Street = street,
            City = city,
            Zip = zip,
            StateId = stateId,
        };
        return await _repository.InsertAsync(entity);
    }

    public virtual async Task<AppointmentClaimExaminer> UpdateAsync(
        Guid id,
        Guid appointmentInjuryDetailId,
        bool isActive,
        string? name = null,
        string? claimExaminerNumber = null,
        string? email = null,
        string? phoneNumber = null,
        string? fax = null,
        string? street = null,
        string? city = null,
        string? zip = null,
        Guid? stateId = null,
        [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(appointmentInjuryDetailId, nameof(appointmentInjuryDetailId));
        Check.Length(name, nameof(name), AppointmentClaimExaminerConsts.NameMaxLength, 0);
        Check.Length(claimExaminerNumber, nameof(claimExaminerNumber), AppointmentClaimExaminerConsts.SuiteMaxLength, 0);
        Check.Length(email, nameof(email), AppointmentClaimExaminerConsts.EmailMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), AppointmentClaimExaminerConsts.PhoneNumberMaxLength, 0);
        Check.Length(fax, nameof(fax), AppointmentClaimExaminerConsts.FaxMaxLength, 0);
        Check.Length(street, nameof(street), AppointmentClaimExaminerConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), AppointmentClaimExaminerConsts.CityMaxLength, 0);
        Check.Length(zip, nameof(zip), AppointmentClaimExaminerConsts.ZipMaxLength, 0);

        var entity = await _repository.GetAsync(id);
        entity.AppointmentInjuryDetailId = appointmentInjuryDetailId;
        entity.IsActive = isActive;
        entity.Name = name;
        entity.Suite = claimExaminerNumber;
        entity.Email = email;
        entity.PhoneNumber = phoneNumber;
        entity.Fax = fax;
        entity.Street = street;
        entity.City = city;
        entity.Zip = zip;
        entity.StateId = stateId;
        entity.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _repository.UpdateAsync(entity);
    }
}
