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

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Default)]
public class AppointmentBodyPartsAppService : CaseEvaluationAppService, IAppointmentBodyPartsAppService
{
    protected IRepository<AppointmentBodyPart, Guid> _repository;
    protected AppointmentBodyPartManager _manager;

    public AppointmentBodyPartsAppService(
        IRepository<AppointmentBodyPart, Guid> repository,
        AppointmentBodyPartManager manager)
    {
        _repository = repository;
        _manager = manager;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Default)]
    public virtual async Task<PagedResultDto<AppointmentBodyPartDto>> GetListAsync(GetAppointmentBodyPartsInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.WhereIf(input.AppointmentInjuryDetailId.HasValue, x => x.AppointmentInjuryDetailId == input.AppointmentInjuryDetailId!.Value);
        var totalCount = query.Count();
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? AppointmentBodyPartConsts.GetDefaultSorting(false) : input.Sorting;
        var items = await query.OrderBy(sorting).PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<AppointmentBodyPart>();
        return new PagedResultDto<AppointmentBodyPartDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentBodyPart>, List<AppointmentBodyPartDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Default)]
    public virtual async Task<AppointmentBodyPartDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentBodyPart, AppointmentBodyPartDto>(await _repository.GetAsync(id));
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Create)]
    public virtual async Task<AppointmentBodyPartDto> CreateAsync(AppointmentBodyPartCreateDto input)
    {
        if (input.AppointmentInjuryDetailId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentInjuryDetail"]]);
        }
        var entity = await _manager.CreateAsync(input.AppointmentInjuryDetailId, input.BodyPartDescription);
        return ObjectMapper.Map<AppointmentBodyPart, AppointmentBodyPartDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Edit)]
    public virtual async Task<AppointmentBodyPartDto> UpdateAsync(Guid id, AppointmentBodyPartUpdateDto input)
    {
        if (input.AppointmentInjuryDetailId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentInjuryDetail"]]);
        }
        var entity = await _repository.GetAsync(id);
        entity.AppointmentInjuryDetailId = input.AppointmentInjuryDetailId;
        entity.BodyPartDescription = input.BodyPartDescription;
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<AppointmentBodyPart, AppointmentBodyPartDto>(entity);
    }
}
