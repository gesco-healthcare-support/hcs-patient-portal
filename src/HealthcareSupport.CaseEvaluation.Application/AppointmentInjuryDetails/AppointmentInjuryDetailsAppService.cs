using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.WcabOffices;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

[RemoteService(IsEnabled = false)]
// Class-level authorization demoted from `Default` to plain `[Authorize]` so any
// authenticated external role can call `GetWcabOfficeLookupAsync` (the WCAB-office
// lookup the booking form needs). Per-method `[Authorize(...Default)]` is preserved
// on the broader read endpoints so admin-side enumeration remains gated.
// (Step 1.4 / W-A-3, 2026-04-30.)
[Authorize]
public class AppointmentInjuryDetailsAppService : CaseEvaluationAppService, IAppointmentInjuryDetailsAppService
{
    protected IAppointmentInjuryDetailRepository _repository;
    protected AppointmentInjuryDetailManager _manager;
    protected IRepository<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice, Guid> _wcabOfficeRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentInjuryDetailsAppService(
        IAppointmentInjuryDetailRepository repository,
        AppointmentInjuryDetailManager manager,
        IRepository<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice, Guid> wcabOfficeRepository,
        AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _repository = repository;
        _manager = manager;
        _wcabOfficeRepository = wcabOfficeRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Default)]
    public virtual async Task<PagedResultDto<AppointmentInjuryDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentInjuryDetailsInput input)
    {
        var totalCount = await _repository.GetCountAsync(input.FilterText, input.AppointmentId, input.ClaimNumber);
        var items = await _repository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AppointmentId, input.ClaimNumber, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentInjuryDetailWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentInjuryDetailWithNavigationProperties>, List<AppointmentInjuryDetailWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Default)]
    public virtual async Task<AppointmentInjuryDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentInjuryDetailWithNavigationProperties, AppointmentInjuryDetailWithNavigationPropertiesDto>((await _repository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Default)]
    public virtual async Task<List<AppointmentInjuryDetailWithNavigationPropertiesDto>> GetByAppointmentIdAsync(Guid appointmentId)
    {
        var items = await _repository.GetListWithNavigationPropertiesAsync(appointmentId: appointmentId);
        return ObjectMapper.Map<List<AppointmentInjuryDetailWithNavigationProperties>, List<AppointmentInjuryDetailWithNavigationPropertiesDto>>(items);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Default)]
    public virtual async Task<AppointmentInjuryDetailDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentInjuryDetail, AppointmentInjuryDetailDto>(await _repository.GetAsync(id));
    }

    // Plain [Authorize]: any authenticated booker can read the WCAB office
    // lookup to populate the Claim Information modal (Step 1.4 / W-A-3).
    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWcabOfficeLookupAsync(LookupRequestDto input)
    {
        var query = (await _wcabOfficeRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Create)]
    public virtual async Task<AppointmentInjuryDetailDto> CreateAsync(AppointmentInjuryDetailCreateDto input)
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
            input.DateOfInjury,
            input.ClaimNumber,
            input.IsCumulativeInjury,
            input.BodyPartsSummary,
            input.ToDateOfInjury,
            input.WcabAdj,
            input.WcabOfficeId);
        return ObjectMapper.Map<AppointmentInjuryDetail, AppointmentInjuryDetailDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Edit)]
    public virtual async Task<AppointmentInjuryDetailDto> UpdateAsync(Guid id, AppointmentInjuryDetailUpdateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }

        // The caller must be a party to the row's OWN parent, and may not move the row to a
        // different one. The parent is read from the stored row, never from the request: checking
        // the supplied id would let a caller nominate an appointment they are a party to and still
        // write to somebody else's row. Every external role holds this service's Edit permission.
        var existing = await _repository.GetAsync(id);
        await _childOwnershipGuard.EnsureCanWriteChildAsync(existing.AppointmentId, input.AppointmentId);

        var entity = await _manager.UpdateAsync(
            id,
            input.AppointmentId,
            input.DateOfInjury,
            input.ClaimNumber,
            input.IsCumulativeInjury,
            input.BodyPartsSummary,
            input.ToDateOfInjury,
            input.WcabAdj,
            input.WcabOfficeId,
            input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentInjuryDetail, AppointmentInjuryDetailDto>(entity);
    }
}
