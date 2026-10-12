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
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Permissions;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Default)]
public class AppointmentBodyPartsAppService : CaseEvaluationAppService, IAppointmentBodyPartsAppService
{
    protected IRepository<AppointmentBodyPart, Guid> _repository;
    protected AppointmentBodyPartManager _manager;
    protected IAppointmentInjuryDetailRepository _injuryDetailRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentBodyPartsAppService(
        IRepository<AppointmentBodyPart, Guid> repository,
        AppointmentBodyPartManager manager,
        IAppointmentInjuryDetailRepository injuryDetailRepository,
        AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _repository = repository;
        _manager = manager;
        _injuryDetailRepository = injuryDetailRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Default)]
    public virtual async Task<PagedResultDto<AppointmentBodyPartDto>> GetListAsync(GetAppointmentBodyPartsInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable.WhereIf(input.AppointmentInjuryDetailId.HasValue, x => x.AppointmentInjuryDetailId == input.AppointmentInjuryDetailId!.Value);
        // Narrow in the query to body parts under injury details of appointments the caller may reach.
        // Applied even when an injury detail is named, so naming someone else's yields an empty page.
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        if (readableAppointmentIds != null)
        {
            var readableInjuryDetailIds = (await _injuryDetailRepository.GetQueryableAsync())
                .Where(i => readableAppointmentIds.Contains(i.AppointmentId))
                .Select(i => i.Id);
            query = query.Where(x => readableInjuryDetailIds.Contains(x.AppointmentInjuryDetailId));
        }
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
        var entity = await _repository.GetAsync(id);
        // A grandchild: the party check is against the appointment its injury detail belongs to.
        var parentInjuryDetail = await _injuryDetailRepository.GetAsync(entity.AppointmentInjuryDetailId);
        await _childOwnershipGuard.EnsureIsPartyAsync(parentInjuryDetail.AppointmentId);
        return ObjectMapper.Map<AppointmentBodyPart, AppointmentBodyPartDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Grandchild: party check against the appointment the row's injury detail belongs to.
        var entity = await _repository.GetAsync(id);
        var parentInjuryDetail = await _injuryDetailRepository.GetAsync(entity.AppointmentInjuryDetailId);
        await _childOwnershipGuard.EnsureIsPartyAsync(parentInjuryDetail.AppointmentId);
        await _repository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentBodyParts.Create)]
    public virtual async Task<AppointmentBodyPartDto> CreateAsync(AppointmentBodyPartCreateDto input)
    {
        if (input.AppointmentInjuryDetailId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentInjuryDetail"]]);
        }
        // A GRANDCHILD: its parent is an injury detail, so the party check is against the appointment that
        // injury detail belongs to, read from the stored row. Booking still passes, because
        // AppointmentChildGroupWriter writes each injury detail, under the flushed appointment, before its
        // body parts.
        var parentInjuryDetail = await _injuryDetailRepository.GetAsync(input.AppointmentInjuryDetailId);
        await _childOwnershipGuard.EnsureIsPartyAsync(parentInjuryDetail.AppointmentId);
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
        // A body part is a GRANDCHILD: its parent is an injury detail, which in turn belongs to
        // an appointment. So the two checks are against different ids and cannot be one call --
        // the row may not change injury detail, and the caller must be a party to the appointment
        // that injury detail belongs to. Both ids come from stored rows, never from the request.
        //
        // PARTY FIRST, then same-parent, for the reason given on the guard: a caller who is not a
        // party must be refused identically whatever injury-detail id they supply, or which refusal
        // arrives tells them whether their guess matched the row's real parent.
        var entity = await _repository.GetAsync(id);
        var parentInjuryDetail = await _injuryDetailRepository.GetAsync(entity.AppointmentInjuryDetailId);
        await _childOwnershipGuard.EnsureIsPartyAsync(parentInjuryDetail.AppointmentId);
        _childOwnershipGuard.EnsureSameParent(entity.AppointmentInjuryDetailId, input.AppointmentInjuryDetailId);

        entity.AppointmentInjuryDetailId = input.AppointmentInjuryDetailId;
        entity.BodyPartDescription = input.BodyPartDescription;
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<AppointmentBodyPart, AppointmentBodyPartDto>(entity);
    }
}
