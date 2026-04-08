using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeManager : DomainService
{
    protected IWcabOfficeRepository _wcabOfficeRepository;

    public WcabOfficeManager(IWcabOfficeRepository wcabOfficeRepository)
    {
        _wcabOfficeRepository = wcabOfficeRepository;
    }

    public virtual async Task<WcabOffice> CreateAsync(Guid? stateId, string name, string abbreviation, bool isActive, string? address = null, string? city = null, string? zipCode = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), WcabOfficeConsts.NameMaxLength);
        Check.NotNullOrWhiteSpace(abbreviation, nameof(abbreviation));
        Check.Length(abbreviation, nameof(abbreviation), WcabOfficeConsts.AbbreviationMaxLength);
        Check.Length(address, nameof(address), WcabOfficeConsts.AddressMaxLength);
        Check.Length(city, nameof(city), WcabOfficeConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), WcabOfficeConsts.ZipCodeMaxLength);
        var wcabOffice = new WcabOffice(GuidGenerator.Create(), stateId, name, abbreviation, isActive, address, city, zipCode);
        return await _wcabOfficeRepository.InsertAsync(wcabOffice);
    }

    public virtual async Task<WcabOffice> UpdateAsync(Guid id, Guid? stateId, string name, string abbreviation, bool isActive, string? address = null, string? city = null, string? zipCode = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), WcabOfficeConsts.NameMaxLength);
        Check.NotNullOrWhiteSpace(abbreviation, nameof(abbreviation));
        Check.Length(abbreviation, nameof(abbreviation), WcabOfficeConsts.AbbreviationMaxLength);
        Check.Length(address, nameof(address), WcabOfficeConsts.AddressMaxLength);
        Check.Length(city, nameof(city), WcabOfficeConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), WcabOfficeConsts.ZipCodeMaxLength);
        var wcabOffice = await _wcabOfficeRepository.GetAsync(id);
        wcabOffice.StateId = stateId;
        wcabOffice.Name = name;
        wcabOffice.Abbreviation = abbreviation;
        wcabOffice.IsActive = isActive;
        wcabOffice.Address = address;
        wcabOffice.City = city;
        wcabOffice.ZipCode = zipCode;
        wcabOffice.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _wcabOfficeRepository.UpdateAsync(wcabOffice);
    }
}