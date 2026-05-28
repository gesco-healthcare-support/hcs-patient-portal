using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// Doctor CRUD AppService. Honors the one-doctor-per-tenant invariant
/// (PARITY-FLAG-NEW-006) via two guards: <see cref="CreateAsync"/> rejects
/// a second live Doctor row inside the tenant; <see cref="DeleteAsync"/>
/// rejects removal while the tenant still has an Appointment,
/// DoctorAvailability, or active DoctorPreferredLocation. The tenant scope
/// IS the doctor identity, so these dependents are counted tenant-wide
/// (no DoctorId predicate on the operational entities).
/// <see cref="DoctorTenantAppService"/> remains the canonical net-new path
/// (tenant provisioning); this service is for profile edits and
/// soft-deletes only.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Doctors.Default)]
public class DoctorsAppService : CaseEvaluationAppService, IDoctorsAppService
{
    protected IDoctorRepository _doctorRepository;
    protected DoctorManager _doctorManager;
    protected IRepository<Volo.Saas.Tenants.Tenant, Guid> _tenantRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> _appointmentTypeRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> _locationRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> _appointmentRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> _doctorAvailabilityRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.DoctorPreferredLocations.DoctorPreferredLocation> _doctorPreferredLocationRepository;
    private readonly IDataFilter<IMultiTenant> _dataFilter;

    public DoctorsAppService(IDoctorRepository doctorRepository,
       DoctorManager doctorManager,
       IRepository<Volo.Saas.Tenants.Tenant, Guid> tenantRepository,
       IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository,
       IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository,
       IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository,
       IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> doctorAvailabilityRepository,
       IRepository<HealthcareSupport.CaseEvaluation.DoctorPreferredLocations.DoctorPreferredLocation> doctorPreferredLocationRepository,
       IDataFilter<IMultiTenant> dataFilter
       )
    {
        _doctorRepository = doctorRepository;
        _doctorManager = doctorManager;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
        _tenantRepository = tenantRepository;
        _appointmentRepository = appointmentRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _doctorPreferredLocationRepository = doctorPreferredLocationRepository;
        _dataFilter = dataFilter;
    }

    public virtual async Task<PagedResultDto<DoctorWithNavigationPropertiesDto>> GetListAsync(GetDoctorsInput input)
    {
        var isHost = CurrentTenant.Id == null;

        using (isHost ? _dataFilter.Disable() : null)
        {
            var totalCount = await _doctorRepository.GetCountAsync(input.FilterText, input.FirstName, input.LastName, input.Email, input.AppointmentTypeId, input.LocationId);
            var items = await _doctorRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirstName, input.LastName, input.Email, input.AppointmentTypeId, input.LocationId, input.Sorting, input.MaxResultCount, input.SkipCount);
            return new PagedResultDto<DoctorWithNavigationPropertiesDto>
            {
                TotalCount = totalCount,
                Items = ObjectMapper.Map<List<DoctorWithNavigationProperties>, List<DoctorWithNavigationPropertiesDto>>(items)
            };
        }
    }

    public virtual async Task<DoctorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DoctorWithNavigationProperties, DoctorWithNavigationPropertiesDto>((await _doctorRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    public virtual async Task<DoctorDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Doctor, DoctorDto>(await _doctorRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input)
    {
        var query = (await _tenantRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Saas.Tenants.Tenant>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Saas.Tenants.Tenant>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        var query = (await _appointmentTypeRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input)
    {
        var query = (await _locationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.Locations.Location>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.Locations.Location>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.Doctors.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // 2026-05-15 -- guard the soft-delete against orphaning the schedule.
        // Tenant scope IS the doctor identity; deleting the Doctor row while
        // the tenant still has appointments or availabilities would leave a
        // "ghost calendar" (active slots / appointments with no parent
        // profile). Each probe scopes to the current tenant via ABP's
        // IMultiTenant filter automatically.
        //
        // 2026-05-27 -- appointments are probed FIRST. Every Appointment
        // requires a DoctorAvailability (required FK, NoAction), so a slot
        // always co-exists with an appointment. Probing availabilities first
        // would mean the operator is only ever told "N slots remain" and
        // never the more actionable "N appointment(s) remain". Appointment ->
        // DoctorAvailability -> active DoctorPreferredLocation surfaces the
        // most specific blocker first.
        //
        // 2026-05-20 (Q1 Option C, Q2 Option A): host-scope M2M tables
        // (DoctorLocation, DoctorAppointmentType) are intentionally NOT
        // probed -- pure profile metadata, already hidden from every app
        // query by HasQueryFilter(x => !x.Doctor.IsDeleted).
        // DoctorPreferredLocation IS probed but only for IsActive == true
        // rows; inactive rows are audit-preserved history (the entity is
        // never hard-deleted in the normal flow, so counting them would
        // create a "soft-delete forever blocked" trap).
        var appointmentCount = await _appointmentRepository.CountAsync();
        if (appointmentCount > 0)
        {
            ThrowDependentsExist("Appointment", appointmentCount);
        }

        var availabilityCount = await _doctorAvailabilityRepository.CountAsync();
        if (availabilityCount > 0)
        {
            ThrowDependentsExist("DoctorAvailability", availabilityCount);
        }

        var activePreferredLocationCount = await _doctorPreferredLocationRepository
            .CountAsync(x => x.DoctorId == id && x.IsActive);
        if (activePreferredLocationCount > 0)
        {
            ThrowDependentsExist("DoctorPreferredLocation", activePreferredLocationCount);
        }

        await _doctorRepository.DeleteAsync(id);
    }

    private static void ThrowDependentsExist(string entity, long count)
    {
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents)
            .WithData("entity", entity)
            .WithData("count", count);
    }

    [Authorize(CaseEvaluationPermissions.Doctors.Create)]
    public virtual async Task<DoctorDto> CreateAsync(DoctorCreateDto input)
    {
        // 2026-05-15 -- one-doctor-per-tenant invariant (PARITY-FLAG-NEW-006).
        // Tenant scope IS the doctor identity; a second Doctor row inside the
        // same tenant breaks every downstream "lookup by tenant scope" path
        // (DoctorAvailability, Appointment, slot generation).
        // DoctorTenantAppService is the canonical net-new path; this
        // AppService is for profile edits. The explicit TenantId predicate is
        // null-safe and deterministic in both host and tenant scope (the
        // implicit soft-delete filter already excludes deleted rows, so a
        // soft-deleted doctor does not block a fresh create).
        var existing = await _doctorRepository.FindAsync(x => x.TenantId == CurrentTenant.Id);
        if (existing != null)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.DoctorOnePerTenantViolated)
                .WithData("tenantId", CurrentTenant.Id ?? Guid.Empty);
        }

        var doctor = await _doctorManager.CreateAsync(input.AppointmentTypeIds, input.LocationIds, input.FirstName, input.LastName, input.Email, input.Gender);
        return ObjectMapper.Map<Doctor, DoctorDto>(doctor);
    }

    [Authorize(CaseEvaluationPermissions.Doctors.Edit)]
    public virtual async Task<DoctorDto> UpdateAsync(Guid id, DoctorUpdateDto input)
    {
        var doctor = await _doctorManager.UpdateAsync(id, input.AppointmentTypeIds, input.LocationIds, input.FirstName, input.LastName, input.Email, input.Gender, input.ConcurrencyStamp);
        return ObjectMapper.Map<Doctor, DoctorDto>(doctor);
    }
}
