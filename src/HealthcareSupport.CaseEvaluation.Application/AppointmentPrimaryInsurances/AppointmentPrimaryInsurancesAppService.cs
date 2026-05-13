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

    public AppointmentPrimaryInsurancesAppService(
        IRepository<AppointmentPrimaryInsurance, Guid> repository,
        AppointmentPrimaryInsuranceManager manager,
        IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository)
    {
        _repository = repository;
        _manager = manager;
        _stateRepository = stateRepository;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPrimaryInsurances.Default)]
    public virtual async Task<PagedResultDto<AppointmentPrimaryInsuranceDto>> GetListAsync(GetAppointmentPrimaryInsurancesInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.WhereIf(input.AppointmentInjuryDetailId.HasValue, x => x.AppointmentInjuryDetailId == input.AppointmentInjuryDetailId!.Value);
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
        if (input.AppointmentInjuryDetailId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentInjuryDetail"]]);
        }
        var entity = await _manager.CreateAsync(
            input.AppointmentInjuryDetailId,
            input.IsActive,
            input.Name,
            input.Suite,
            input.Attention,
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
        if (input.AppointmentInjuryDetailId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentInjuryDetail"]]);
        }
        var entity = await _manager.UpdateAsync(
            id,
            input.AppointmentInjuryDetailId,
            input.IsActive,
            input.Name,
            input.Suite,
            input.Attention,
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
