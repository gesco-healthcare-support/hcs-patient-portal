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

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Doctors.Default)]
public class DoctorsAppService : CaseEvaluationAppService, IDoctorsAppService
{
    protected IDoctorRepository _doctorRepository;
    protected DoctorManager _doctorManager;
    protected IRepository<Volo.Saas.Tenants.Tenant, Guid> _tenantRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> _appointmentTypeRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> _locationRepository;
    private readonly IDataFilter<IMultiTenant> _dataFilter;

    public DoctorsAppService(IDoctorRepository doctorRepository,
       DoctorManager doctorManager,
       IRepository<Volo.Saas.Tenants.Tenant, Guid> tenantRepository,
       IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository,
       IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository,
       IDataFilter<IMultiTenant> dataFilter
       )
    {
        _doctorRepository = doctorRepository;
        _doctorManager = doctorManager;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
        _tenantRepository = tenantRepository;
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
        await _doctorRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.Doctors.Create)]
    public virtual async Task<DoctorDto> CreateAsync(DoctorCreateDto input)
    {
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
