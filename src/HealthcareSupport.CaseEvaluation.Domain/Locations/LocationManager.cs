using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Locations;

public class LocationManager : DomainService
{
    protected ILocationRepository _locationRepository;

    public LocationManager(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    public virtual async Task<Location> CreateAsync(Guid? stateId, Guid? appointmentTypeId, string name, decimal parkingFee, bool isActive, string? address = null, string? city = null, string? zipCode = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), LocationConsts.NameMaxLength);
        Check.Length(address, nameof(address), LocationConsts.AddressMaxLength);
        Check.Length(city, nameof(city), LocationConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), LocationConsts.ZipCodeMaxLength);
        var location = new Location(GuidGenerator.Create(), stateId, appointmentTypeId, name, parkingFee, isActive, address, city, zipCode);
        return await _locationRepository.InsertAsync(location);
    }

    public virtual async Task<Location> UpdateAsync(Guid id, Guid? stateId, Guid? appointmentTypeId, string name, decimal parkingFee, bool isActive, string? address = null, string? city = null, string? zipCode = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), LocationConsts.NameMaxLength);
        Check.Length(address, nameof(address), LocationConsts.AddressMaxLength);
        Check.Length(city, nameof(city), LocationConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), LocationConsts.ZipCodeMaxLength);
        var location = await _locationRepository.GetAsync(id);
        location.StateId = stateId;
        location.AppointmentTypeId = appointmentTypeId;
        location.Name = name;
        location.ParkingFee = parkingFee;
        location.IsActive = isActive;
        location.Address = address;
        location.City = city;
        location.ZipCode = zipCode;
        location.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _locationRepository.UpdateAsync(location);
    }
}