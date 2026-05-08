using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public class ApplicantAttorneyManager : DomainService
{
    protected IApplicantAttorneyRepository _applicantAttorneyRepository;

    public ApplicantAttorneyManager(IApplicantAttorneyRepository applicantAttorneyRepository)
    {
        _applicantAttorneyRepository = applicantAttorneyRepository;
    }

    public virtual async Task<ApplicantAttorney> CreateAsync(Guid? stateId, Guid? identityUserId, string? firmName = null, string? firmAddress = null, string? phoneNumber = null, string? webAddress = null, string? faxNumber = null, string? street = null, string? city = null, string? zipCode = null, string? email = null)
    {
        Check.Length(firmName, nameof(firmName), ApplicantAttorneyConsts.FirmNameMaxLength);
        Check.Length(firmAddress, nameof(firmAddress), ApplicantAttorneyConsts.FirmAddressMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), ApplicantAttorneyConsts.PhoneNumberMaxLength);
        Check.Length(webAddress, nameof(webAddress), ApplicantAttorneyConsts.WebAddressMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), ApplicantAttorneyConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), ApplicantAttorneyConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), ApplicantAttorneyConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), ApplicantAttorneyConsts.ZipCodeMaxLength, 0);
        Check.Length(email, nameof(email), ApplicantAttorneyConsts.EmailMaxLength, 0);
        var applicantAttorney = new ApplicantAttorney(GuidGenerator.Create(), stateId, identityUserId, firmName, firmAddress, phoneNumber, email);
        applicantAttorney.WebAddress = webAddress;
        applicantAttorney.FaxNumber = faxNumber;
        applicantAttorney.Street = street;
        applicantAttorney.City = city;
        applicantAttorney.ZipCode = zipCode;
        return await _applicantAttorneyRepository.InsertAsync(applicantAttorney);
    }

    public virtual async Task<ApplicantAttorney> UpdateAsync(Guid id, Guid? stateId, Guid? identityUserId, string? firmName = null, string? firmAddress = null, string? phoneNumber = null, string? webAddress = null, string? faxNumber = null, string? street = null, string? city = null, string? zipCode = null, [CanBeNull] string? concurrencyStamp = null, string? email = null)
    {
        Check.Length(firmName, nameof(firmName), ApplicantAttorneyConsts.FirmNameMaxLength);
        Check.Length(firmAddress, nameof(firmAddress), ApplicantAttorneyConsts.FirmAddressMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), ApplicantAttorneyConsts.PhoneNumberMaxLength);
        Check.Length(webAddress, nameof(webAddress), ApplicantAttorneyConsts.WebAddressMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), ApplicantAttorneyConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), ApplicantAttorneyConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), ApplicantAttorneyConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), ApplicantAttorneyConsts.ZipCodeMaxLength, 0);
        Check.Length(email, nameof(email), ApplicantAttorneyConsts.EmailMaxLength, 0);
        var applicantAttorney = await _applicantAttorneyRepository.GetAsync(id);
        applicantAttorney.StateId = stateId;
        applicantAttorney.IdentityUserId = identityUserId;
        applicantAttorney.FirmName = firmName;
        applicantAttorney.FirmAddress = firmAddress;
        applicantAttorney.PhoneNumber = phoneNumber;
        applicantAttorney.WebAddress = webAddress;
        applicantAttorney.FaxNumber = faxNumber;
        applicantAttorney.Street = street;
        applicantAttorney.City = city;
        applicantAttorney.ZipCode = zipCode;
        applicantAttorney.Email = email;
        applicantAttorney.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _applicantAttorneyRepository.UpdateAsync(applicantAttorney);
    }
}