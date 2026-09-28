using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Default)]
public class AppointmentPrimaryInsurancesAppService : CaseEvaluationAppService, IAppointmentPrimaryInsurancesAppService
{
    protected IRepository<AppointmentPrimaryInsurance, Guid> _repository;
    protected AppointmentPrimaryInsuranceManager _manager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentPrimaryInsurancesAppService(
        IRepository<AppointmentPrimaryInsurance, Guid> repository,
        AppointmentPrimaryInsuranceManager manager,
        IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository,
        AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _repository = repository;
        _manager = manager;
        _stateRepository = stateRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Default)]
    public virtual async Task<PagedResultDto<AppointmentPrimaryInsuranceDto>> GetListAsync(GetAppointmentPrimaryInsurancesInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.WhereIf(input.AppointmentId.HasValue, x => x.AppointmentId == input.AppointmentId!.Value);
        var totalCount = query.Count();
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? AppointmentPrimaryInsuranceConsts.GetDefaultSorting(false) : input.Sorting;
        var items = await query.OrderBy(sorting).PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<AppointmentPrimaryInsurance>();
        return new PagedResultDto<AppointmentPrimaryInsuranceDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentPrimaryInsurance>, List<AppointmentPrimaryInsuranceDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Default)]
    public virtual async Task<AppointmentPrimaryInsuranceDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentPrimaryInsurance, AppointmentPrimaryInsuranceDto>(await _repository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Create)]
    public virtual async Task<AppointmentPrimaryInsuranceDto> CreateAsync(AppointmentPrimaryInsuranceCreateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        var entity = await _manager.CreateAsync(
            input.AppointmentId,
            input.IsActive,
            input.Name,
            input.Suite,
            input.PhoneNumber,
            input.FaxNumber,
            input.Street,
            input.City,
            input.Zip,
            input.StateId);
        return ObjectMapper.Map<AppointmentPrimaryInsurance, AppointmentPrimaryInsuranceDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Edit)]
    public virtual async Task<AppointmentPrimaryInsuranceDto> UpdateAsync(Guid id, AppointmentPrimaryInsuranceUpdateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        // The caller must be a party to the row's OWN parent, and may not move the row to a
        // different one. The parent is read from the stored row, never from the request: checking
        // the supplied id would let a caller nominate an appointment they are a party to and still
        // write to somebody else's row. Every external role holds this service's Edit permission.
        var existingChild = await _repository.GetAsync(id);
        await _childOwnershipGuard.EnsureCanWriteChildAsync(existingChild.AppointmentId, input.AppointmentId);

        var entity = await _manager.UpdateAsync(
            id,
            input.AppointmentId,
            input.IsActive,
            input.Name,
            input.Suite,
            input.PhoneNumber,
            input.FaxNumber,
            input.Street,
            input.City,
            input.Zip,
            input.StateId,
            input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentPrimaryInsurance, AppointmentPrimaryInsuranceDto>(entity);
    }
}
