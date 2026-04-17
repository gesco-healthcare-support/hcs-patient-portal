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
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Doctors;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Doctors.Default)]
public class DoctorsAppService : CaseEvaluationAppService, IDoctorsAppService
{
    protected IDoctorRepository _doctorRepository;
    protected DoctorManager _doctorManager;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<Volo.Saas.Tenants.Tenant, Guid> _tenantRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> _appointmentTypeRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> _locationRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IDataFilter<IMultiTenant> _dataFilter;

    public DoctorsAppService(IDoctorRepository doctorRepository,
       DoctorManager doctorManager,
       IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository,
       IRepository<Volo.Saas.Tenants.Tenant, Guid> tenantRepository,
       IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository,
       IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository,
       IdentityUserManager userManager,
       IDataFilter<IMultiTenant> dataFilter
       )
    {
        _doctorRepository = doctorRepository;
        _doctorManager = doctorManager;
        _identityUserRepository = identityUserRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
        _tenantRepository = tenantRepository;
        _userManager = userManager;
        _dataFilter = dataFilter;
    }

    public virtual async Task<PagedResultDto<DoctorWithNavigationPropertiesDto>> GetListAsync(GetDoctorsInput input)
    {
        var isHost = CurrentTenant.Id == null;

        using (isHost ? _dataFilter.Disable() : null)
        {
            var totalCount = await _doctorRepository.GetCountAsync(input.FilterText, input.FirstName, input.LastName, input.Email, input.IdentityUserId, input.AppointmentTypeId, input.LocationId);
            var items = await _doctorRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirstName, input.LastName, input.Email, input.IdentityUserId, input.AppointmentTypeId, input.LocationId, input.Sorting, input.MaxResultCount, input.SkipCount);
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

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input)
    {
        var query = (await _tenantRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var query = (await _appointmentTypeRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var query = (await _locationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var doctor = await _doctorManager.CreateAsync(input.AppointmentTypeIds, input.LocationIds, input.IdentityUserId, input.FirstName, input.LastName, input.Email, input.Gender);
        return ObjectMapper.Map<Doctor, DoctorDto>(doctor);
    }

    [Authorize(CaseEvaluationPermissions.Doctors.Edit)]
    public virtual async Task<DoctorDto> UpdateAsync(Guid id, DoctorUpdateDto input)
    {
        var doctor = await _doctorManager.UpdateAsync(id, input.AppointmentTypeIds, input.LocationIds, input.IdentityUserId, input.FirstName, input.LastName, input.Email, input.Gender, input.ConcurrencyStamp);

        if (input.IdentityUserId.HasValue)
        {
            var user = await _userManager.FindByIdAsync(input.IdentityUserId.Value.ToString());
            if (user == null)
            {
                throw new UserFriendlyException("Linked identity user was not found.");
            }

            user.Name = input.FirstName;
            user.Surname = input.LastName;
            if (!string.IsNullOrWhiteSpace(input.Email))
            {
                var emailResult = await _userManager.SetEmailAsync(user, input.Email);
                if (!emailResult.Succeeded)
                {
                    throw new UserFriendlyException("Failed to update identity user email: " +
                        string.Join(", ", emailResult.Errors.Select(e => e.Description)));
                }
            }

            var userResult = await _userManager.UpdateAsync(user);
            if (!userResult.Succeeded)
            {
                throw new UserFriendlyException("Failed to update identity user: " +
                    string.Join(", ", userResult.Errors.Select(e => e.Description)));
            }
        }

        return ObjectMapper.Map<Doctor, DoctorDto>(doctor);
    }
}