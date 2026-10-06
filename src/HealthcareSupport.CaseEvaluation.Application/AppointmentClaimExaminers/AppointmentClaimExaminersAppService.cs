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

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
public class AppointmentClaimExaminersAppService : CaseEvaluationAppService, IAppointmentClaimExaminersAppService
{
    protected IRepository<AppointmentClaimExaminer, Guid> _repository;
    protected AppointmentClaimExaminerManager _manager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentClaimExaminersAppService(
        IRepository<AppointmentClaimExaminer, Guid> repository,
        AppointmentClaimExaminerManager manager,
        IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository,
        AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _repository = repository;
        _manager = manager;
        _stateRepository = stateRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
    public virtual async Task<PagedResultDto<AppointmentClaimExaminerDto>> GetListAsync(GetAppointmentClaimExaminersInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var query = queryable.WhereIf(input.AppointmentId.HasValue, x => x.AppointmentId == input.AppointmentId!.Value);
        if (readableAppointmentIds != null) { query = query.Where(x => readableAppointmentIds.Contains(x.AppointmentId)); }
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
        var entity = await _repository.GetAsync(id);
        // Reading a child row is reading its parent appointment: the .Default permission ties the caller to no appointment.
        await _childOwnershipGuard.EnsureIsPartyAsync(entity.AppointmentId);
        return ObjectMapper.Map<AppointmentClaimExaminer, AppointmentClaimExaminerDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Default)]
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

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Party check against the row's STORED parent appointment (never request input), as on update.
        var existing = await _repository.GetAsync(id);
        await _childOwnershipGuard.EnsureIsPartyAsync(existing.AppointmentId);
        await _repository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentClaimExaminers.Create)]
    public virtual async Task<AppointmentClaimExaminerDto> CreateAsync(AppointmentClaimExaminerCreateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        // The caller must be a party to the appointment the new row hangs off. The external booking roles
        // hold this service's Create permission, so the permission alone ties the caller to no appointment.
        // Booking still passes: AppointmentsAppService.SubmitAsync flushes the new appointment, stamping its
        // CreatorId, before AppointmentChildGroupWriter writes any child group, so the booker passes as its
        // creator (AccessPathway.Creator; see AppointmentChildOwnershipGuard.EnsureIsPartyAsync for why that is permanent).
        // Checked FIRST, before any other lookup, so a non-party is refused the same way whatever they send.
        await _childOwnershipGuard.EnsureIsPartyAsync(input.AppointmentId);
        var entity = await _manager.CreateAsync(
            input.AppointmentId,
            input.IsActive,
            input.Name,
            input.Suite,
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
