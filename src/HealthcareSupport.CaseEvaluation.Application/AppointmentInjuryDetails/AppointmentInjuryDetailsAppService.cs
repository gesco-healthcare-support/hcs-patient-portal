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
// Class-level authorization demoted from `Default` to plain `[Authorize]` so
// external roles (Patient + AA, the lookup-eligible bookers per Adrian D-2)
// can call `GetWcabOfficeLookupAsync`. Per-method `[Authorize(...Default)]`
// is preserved on the broader read endpoints so admin-side enumeration
// remains gated. (Step 1.4 / W-A-3, 2026-04-30.)
[Authorize]
public class AppointmentInjuryDetailsAppService : CaseEvaluationAppService, IAppointmentInjuryDetailsAppService
{
    protected IAppointmentInjuryDetailRepository _repository;
    protected AppointmentInjuryDetailManager _manager;
    protected IRepository<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice, Guid> _wcabOfficeRepository;

    public AppointmentInjuryDetailsAppService(
        IAppointmentInjuryDetailRepository repository,
        AppointmentInjuryDetailManager manager,
        IRepository<HealthcareSupport.CaseEvaluation.WcabOffices.WcabOffice, Guid> wcabOfficeRepository)
    {
        _repository = repository;
        _manager = manager;
        _wcabOfficeRepository = wcabOfficeRepository;
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
