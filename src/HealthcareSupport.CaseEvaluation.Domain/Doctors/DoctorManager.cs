using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorManager : DomainService
{
    protected IDoctorRepository _doctorRepository;
    protected IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    protected IRepository<Location, Guid> _locationRepository;

    public DoctorManager(IDoctorRepository doctorRepository, IRepository<AppointmentType, Guid> appointmentTypeRepository, IRepository<Location, Guid> locationRepository)
    {
        _doctorRepository = doctorRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
    }

    public virtual async Task<Doctor> CreateAsync(List<Guid> appointmentTypeIds, List<Guid> locationIds, Guid? identityUserId, string firstName, string lastName, string email, Gender gender)
    {
        Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), DoctorConsts.FirstNameMaxLength);
        Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), DoctorConsts.LastNameMaxLength);
        Check.NotNullOrWhiteSpace(email, nameof(email));
        Check.Length(email, nameof(email), DoctorConsts.EmailMaxLength);
        Check.NotNull(gender, nameof(gender));
        var doctor = new Doctor(GuidGenerator.Create(), identityUserId, firstName, lastName, email, gender);
        await SetAppointmentTypesAsync(doctor, appointmentTypeIds);
        await SetLocationsAsync(doctor, locationIds);
        return await _doctorRepository.InsertAsync(doctor);
    }

    public virtual async Task<Doctor> UpdateAsync(Guid id, List<Guid> appointmentTypeIds, List<Guid> locationIds, Guid? identityUserId, string firstName, string lastName, string email, Gender gender, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), DoctorConsts.FirstNameMaxLength);
        Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), DoctorConsts.LastNameMaxLength);
        Check.NotNullOrWhiteSpace(email, nameof(email));
        Check.Length(email, nameof(email), DoctorConsts.EmailMaxLength);
        Check.NotNull(gender, nameof(gender));
        var queryable = await _doctorRepository.WithDetailsAsync(x => x.AppointmentTypes, x => x.Locations);
        var query = queryable.Where(x => x.Id == id);
        var doctor = await AsyncExecuter.FirstOrDefaultAsync(query);
        doctor.IdentityUserId = identityUserId;
        doctor.FirstName = firstName;
        doctor.LastName = lastName;
        doctor.Email = email;
        doctor.Gender = gender;
        await SetAppointmentTypesAsync(doctor, appointmentTypeIds);
        await SetLocationsAsync(doctor, locationIds);
        doctor.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _doctorRepository.UpdateAsync(doctor);
    }

    private async Task SetAppointmentTypesAsync(Doctor doctor, List<Guid> appointmentTypeIds)
    {
        if (appointmentTypeIds == null || !appointmentTypeIds.Any())
        {
            doctor.RemoveAllAppointmentTypes();
            return;
        }

        var query = (await _appointmentTypeRepository.GetQueryableAsync()).Where(x => appointmentTypeIds.Contains(x.Id)).Select(x => x.Id);
        var appointmentTypeIdsInDb = await AsyncExecuter.ToListAsync(query);
        if (!appointmentTypeIdsInDb.Any())
        {
            return;
        }

        doctor.RemoveAllAppointmentTypesExceptGivenIds(appointmentTypeIdsInDb);
        foreach (var appointmentTypeId in appointmentTypeIdsInDb)
        {
            doctor.AddAppointmentType(appointmentTypeId);
        }
    }

    private async Task SetLocationsAsync(Doctor doctor, List<Guid> locationIds)
    {
        if (locationIds == null || !locationIds.Any())
        {
            doctor.RemoveAllLocations();
            return;
        }

        var query = (await _locationRepository.GetQueryableAsync()).Where(x => locationIds.Contains(x.Id)).Select(x => x.Id);
        var locationIdsInDb = await AsyncExecuter.ToListAsync(query);
        if (!locationIdsInDb.Any())
        {
            return;
        }

        doctor.RemoveAllLocationsExceptGivenIds(locationIdsInDb);
        foreach (var locationId in locationIdsInDb)
        {
            doctor.AddLocation(locationId);
        }
    }
}