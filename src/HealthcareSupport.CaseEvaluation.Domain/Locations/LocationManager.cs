using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// Domain service for the per-office Location master entity. Beyond the
/// not-blank + length checks, IP4 (2026-06-05) adds the integrity guards a
/// intake-staff-facing master-data screen needs: a duplicate-name guard
/// (unique within an office), a non-negative ParkingFee guard, a ZipCode
/// format guard, and <see cref="EnsureCanDeleteAsync"/> which blocks a (soft)
/// delete while an Appointment or DoctorAvailability still references the
/// Location. Database-per-office: Location and its referencing rows are
/// IMultiTenant in the office's own database, so the guards run in the office's
/// context where the filter already scopes them to that office.
/// </summary>
public class LocationManager : DomainService
{
    // US ZIP: 5 digits, optionally + 4 (ZIP+4). Blank is allowed (optional field).
    // A match timeout is supplied as defense-in-depth (ReDoS hardening) even though
    // this anchored pattern has no catastrophic-backtracking risk.
    private static readonly Regex ZipCodeFormat =
        new(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    protected ILocationRepository _locationRepository;
    protected IRepository<Appointment, Guid> _appointmentRepository;
    protected IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;

    public LocationManager(
        ILocationRepository locationRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> doctorAvailabilityRepository)
    {
        _locationRepository = locationRepository;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
    }

    public virtual async Task<Location> CreateAsync(Guid? stateId, List<Guid> appointmentTypeIds, string name, decimal parkingFee, bool isActive, string? address = null, string? city = null, string? zipCode = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), LocationConsts.NameMaxLength);
        Check.Length(address, nameof(address), LocationConsts.AddressMaxLength);
        Check.Length(city, nameof(city), LocationConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), LocationConsts.ZipCodeMaxLength);
        EnsureParkingFeeNonNegative(parkingFee);
        EnsureZipCodeFormat(zipCode);
        await EnsureNameIsUniqueAsync(name, Guid.Empty);
        var location = new Location(GuidGenerator.Create(), stateId, name, parkingFee, isActive, address, city, zipCode);
        location.SetAppointmentTypes(appointmentTypeIds ?? new List<Guid>());
        return await _locationRepository.InsertAsync(location);
    }

    public virtual async Task<Location> UpdateAsync(Guid id, Guid? stateId, List<Guid> appointmentTypeIds, string name, decimal parkingFee, bool isActive, string? address = null, string? city = null, string? zipCode = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), LocationConsts.NameMaxLength);
        Check.Length(address, nameof(address), LocationConsts.AddressMaxLength);
        Check.Length(city, nameof(city), LocationConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), LocationConsts.ZipCodeMaxLength);
        EnsureParkingFeeNonNegative(parkingFee);
        EnsureZipCodeFormat(zipCode);
        await EnsureNameIsUniqueAsync(name, id);
        // I3: load the AppointmentTypes M2M so SetAppointmentTypes diffs correctly
        // (mirrors DoctorManager.UpdateAsync).
        var queryable = await _locationRepository.WithDetailsAsync(x => x.AppointmentTypes);
        var location = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id))
            ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Location), id);
        location.StateId = stateId;
        location.Name = name;
        location.ParkingFee = parkingFee;
        location.IsActive = isActive;
        location.Address = address;
        location.City = city;
        location.ZipCode = zipCode;
        location.SetAppointmentTypes(appointmentTypeIds ?? new List<Guid>());
        location.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _locationRepository.UpdateAsync(location);
    }

    /// <summary>
    /// Guards a (soft) delete: throws <see cref="CaseEvaluationDomainErrorCodes.LocationInUse"/>
    /// when the Location is still referenced by an Appointment or DoctorAvailability. Appointments
    /// are probed first (the more actionable blocker). Database-per-office: the Location and its
    /// referencing rows are IMultiTenant in the office's own database, so the probe runs in the
    /// office's context where the filter already scopes each count to that office.
    /// </summary>
    public virtual async Task EnsureCanDeleteAsync(Guid id)
    {
        var appointmentCount = await _appointmentRepository.CountAsync(x => x.LocationId == id);
        if (appointmentCount > 0)
        {
            ThrowInUse("Appointment", appointmentCount);
        }

        var availabilityCount = await _doctorAvailabilityRepository.CountAsync(x => x.LocationId == id);
        if (availabilityCount > 0)
        {
            ThrowInUse("DoctorAvailability", availabilityCount);
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid excludeId)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var duplicateCount = await _locationRepository.CountAsync(
            x => x.Name.ToLower() == normalized && x.Id != excludeId);
        if (duplicateCount > 0)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.LocationDuplicateName)
                .WithData("name", name);
        }
    }

    private static void EnsureParkingFeeNonNegative(decimal parkingFee)
    {
        if (parkingFee < 0)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.LocationParkingFeeNegative);
        }
    }

    private static void EnsureZipCodeFormat(string? zipCode)
    {
        if (!string.IsNullOrWhiteSpace(zipCode) && !ZipCodeFormat.IsMatch(zipCode))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.LocationZipCodeInvalid);
        }
    }

    private static void ThrowInUse(string entity, long count)
    {
        throw new BusinessException(CaseEvaluationDomainErrorCodes.LocationInUse)
            .WithData("entity", entity)
            .WithData("count", count);
    }
}
