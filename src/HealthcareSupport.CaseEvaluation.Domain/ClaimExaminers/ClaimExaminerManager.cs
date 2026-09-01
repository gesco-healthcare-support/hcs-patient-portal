using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class ClaimExaminerManager : DomainService
{
    protected IClaimExaminerRepository _claimExaminerRepository;

    public ClaimExaminerManager(IClaimExaminerRepository claimExaminerRepository)
    {
        _claimExaminerRepository = claimExaminerRepository;
    }

    public virtual async Task<ClaimExaminer> CreateAsync(Guid? stateId, Guid? identityUserId, string? phoneNumber = null, string? faxNumber = null, string? street = null, string? city = null, string? zipCode = null, string? email = null, string? firstName = null, string? lastName = null)
    {
        Check.Length(firstName, nameof(firstName), ClaimExaminerConsts.FirstNameMaxLength, 0);
        Check.Length(lastName, nameof(lastName), ClaimExaminerConsts.LastNameMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), ClaimExaminerConsts.PhoneNumberMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), ClaimExaminerConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), ClaimExaminerConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), ClaimExaminerConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), ClaimExaminerConsts.ZipCodeMaxLength, 0);
        Check.Length(email, nameof(email), ClaimExaminerConsts.EmailMaxLength, 0);
        var claimExaminer = new ClaimExaminer(GuidGenerator.Create(), stateId, identityUserId, phoneNumber, email);
        claimExaminer.FirstName = firstName;
        claimExaminer.LastName = lastName;
        claimExaminer.FaxNumber = faxNumber;
        claimExaminer.Street = street;
        claimExaminer.City = city;
        claimExaminer.ZipCode = zipCode;
        return await _claimExaminerRepository.InsertAsync(claimExaminer);
    }

    public virtual async Task<ClaimExaminer> UpdateAsync(Guid id, Guid? stateId, Guid? identityUserId, string? phoneNumber = null, string? faxNumber = null, string? street = null, string? city = null, string? zipCode = null, [CanBeNull] string? concurrencyStamp = null, string? email = null, string? firstName = null, string? lastName = null)
    {
        Check.Length(firstName, nameof(firstName), ClaimExaminerConsts.FirstNameMaxLength, 0);
        Check.Length(lastName, nameof(lastName), ClaimExaminerConsts.LastNameMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), ClaimExaminerConsts.PhoneNumberMaxLength, 0);
        Check.Length(faxNumber, nameof(faxNumber), ClaimExaminerConsts.FaxNumberMaxLength, 0);
        Check.Length(street, nameof(street), ClaimExaminerConsts.StreetMaxLength, 0);
        Check.Length(city, nameof(city), ClaimExaminerConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), ClaimExaminerConsts.ZipCodeMaxLength, 0);
        Check.Length(email, nameof(email), ClaimExaminerConsts.EmailMaxLength, 0);
        var claimExaminer = await _claimExaminerRepository.GetAsync(id);
        claimExaminer.StateId = stateId;
        claimExaminer.IdentityUserId = identityUserId;
        claimExaminer.FirstName = firstName;
        claimExaminer.LastName = lastName;
        claimExaminer.PhoneNumber = phoneNumber;
        claimExaminer.FaxNumber = faxNumber;
        claimExaminer.Street = street;
        claimExaminer.City = city;
        claimExaminer.ZipCode = zipCode;
        claimExaminer.Email = email;
        claimExaminer.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _claimExaminerRepository.UpdateAsync(claimExaminer);
    }
}
