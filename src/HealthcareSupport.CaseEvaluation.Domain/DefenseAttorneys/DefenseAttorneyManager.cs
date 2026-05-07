using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class DefenseAttorneyManager : DomainService
{
    protected IDefenseAttorneyRepository _defenseAttorneyRepository;

    public DefenseAttorneyManager(IDefenseAttorneyRepository defenseAttorneyRepository)
    {
        _defenseAttorneyRepository = defenseAttorneyRepository;
    }

    public virtual async Task<DefenseAttorney> CreateAsync(Guid? stateId, Guid? identityUserId, string? firmName = null, string? firmAddress = null, string? phoneNumber = null, string? webAddress = null, string? faxNumber = null, string? street = null, string? city = null, string? zipCode = null, string? email = null)
    {
        Check.Length(firmName, nameof(firmName), DefenseAttorneyConsts.FirmNameMaxLength);
        Check.Length(firmAddress, nameof(firmAddress), DefenseAttorneyConsts.FirmAddressMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), DefenseAttorneyConsts.PhoneNumberMaxLength);
        Check.Length(webAddress, nameof(webAddress), DefenseAttorneyConsts.WebAddressMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), DefenseAttorneyConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), DefenseAttorneyConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), DefenseAttorneyConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), DefenseAttorneyConsts.ZipCodeMaxLength, 0);
        Check.Length(email, nameof(email), DefenseAttorneyConsts.EmailMaxLength, 0);
        var defenseAttorney = new DefenseAttorney(GuidGenerator.Create(), stateId, identityUserId, firmName, firmAddress, phoneNumber, email);
        defenseAttorney.WebAddress = webAddress;
        defenseAttorney.FaxNumber = faxNumber;
        defenseAttorney.Street = street;
        defenseAttorney.City = city;
        defenseAttorney.ZipCode = zipCode;
        return await _defenseAttorneyRepository.InsertAsync(defenseAttorney);
    }

    public virtual async Task<DefenseAttorney> UpdateAsync(Guid id, Guid? stateId, Guid? identityUserId, string? firmName = null, string? firmAddress = null, string? phoneNumber = null, string? webAddress = null, string? faxNumber = null, string? street = null, string? city = null, string? zipCode = null, [CanBeNull] string? concurrencyStamp = null, string? email = null)
    {
        Check.Length(firmName, nameof(firmName), DefenseAttorneyConsts.FirmNameMaxLength);
        Check.Length(firmAddress, nameof(firmAddress), DefenseAttorneyConsts.FirmAddressMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), DefenseAttorneyConsts.PhoneNumberMaxLength);
        Check.Length(webAddress, nameof(webAddress), DefenseAttorneyConsts.WebAddressMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), DefenseAttorneyConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), DefenseAttorneyConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), DefenseAttorneyConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), DefenseAttorneyConsts.ZipCodeMaxLength, 0);
        Check.Length(email, nameof(email), DefenseAttorneyConsts.EmailMaxLength, 0);
        var defenseAttorney = await _defenseAttorneyRepository.GetAsync(id);
        defenseAttorney.StateId = stateId;
        defenseAttorney.IdentityUserId = identityUserId;
        defenseAttorney.FirmName = firmName;
        defenseAttorney.FirmAddress = firmAddress;
        defenseAttorney.PhoneNumber = phoneNumber;
        defenseAttorney.WebAddress = webAddress;
        defenseAttorney.FaxNumber = faxNumber;
        defenseAttorney.Street = street;
        defenseAttorney.City = city;
        defenseAttorney.ZipCode = zipCode;
        defenseAttorney.Email = email;
        defenseAttorney.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _defenseAttorneyRepository.UpdateAsync(defenseAttorney);
    }
}
