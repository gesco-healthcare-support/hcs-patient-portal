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

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
public class AppointmentClaimExaminersAppService : CaseEvaluationAppService, IAppointmentClaimExaminersAppService
{
    protected IRepository<AppointmentClaimExaminer, Guid> _repository;
    protected AppointmentClaimExaminerManager _manager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;

    public AppointmentClaimExaminersAppService(
        IRepository<AppointmentClaimExaminer, Guid> repository,
        AppointmentClaimExaminerManager manager,
        IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository)
    {
        _repository = repository;
        _manager = manager;
        _stateRepository = stateRepository;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
    public virtual async Task<PagedResultDto<AppointmentClaimExaminerDto>> GetListAsync(GetAppointmentClaimExaminersInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.WhereIf(input.AppointmentInjuryDetailId.HasValue, x => x.AppointmentInjuryDetailId == input.AppointmentInjuryDetailId!.Value);
        var totalCount = query.Count();
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? AppointmentClaimExaminerConsts.GetDefaultSorting(false) : input.Sorting;
        var items = await query.OrderBy(sorting).PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<AppointmentClaimExaminer>();
        return new PagedResultDto<AppointmentClaimExaminerDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentClaimExaminer>, List<AppointmentClaimExaminerDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
    public virtual async Task<AppointmentClaimExaminerDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentClaimExaminer, AppointmentClaimExaminerDto>(await _repository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Create)]
    public virtual async Task<AppointmentClaimExaminerDto> CreateAsync(AppointmentClaimExaminerCreateDto input)
    {
        if (input.AppointmentInjuryDetailId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentInjuryDetail"]]);
        }
        var entity = await _manager.CreateAsync(
            input.AppointmentInjuryDetailId,
            input.IsActive,
            input.Name,
            input.ClaimExaminerNumber,
            input.Email,
            input.PhoneNumber,
            input.Fax,
            input.Street,
            input.City,
            input.Zip,
            input.StateId);
        return ObjectMapper.Map<AppointmentClaimExaminer, AppointmentClaimExaminerDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Edit)]
    public virtual async Task<AppointmentClaimExaminerDto> UpdateAsync(Guid id, AppointmentClaimExaminerUpdateDto input)
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
            input.ClaimExaminerNumber,
            input.Email,
            input.PhoneNumber,
            input.Fax,
            input.Street,
            input.City,
            input.Zip,
            input.StateId,
            input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentClaimExaminer, AppointmentClaimExaminerDto>(entity);
    }
}
