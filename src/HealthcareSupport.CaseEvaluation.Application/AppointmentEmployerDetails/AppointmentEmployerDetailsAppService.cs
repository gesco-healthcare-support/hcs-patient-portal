
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.Appointments;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentEmployerDetailsAppService : CaseEvaluationAppService, IAppointmentEmployerDetailsAppService
{
    protected IAppointmentEmployerDetailRepository _appointmentEmployerDetailRepository;
    protected AppointmentEmployerDetailManager _appointmentEmployerDetailManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> _appointmentRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected AppointmentChildOwnershipGuard _childOwnershipGuard;

    public AppointmentEmployerDetailsAppService(IAppointmentEmployerDetailRepository appointmentEmployerDetailRepository, AppointmentEmployerDetailManager appointmentEmployerDetailManager, IRepository<HealthcareSupport.CaseEvaluation.Appointments.Appointment, Guid> appointmentRepository, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, AppointmentChildOwnershipGuard childOwnershipGuard)
    {
        _appointmentEmployerDetailRepository = appointmentEmployerDetailRepository;
        _appointmentEmployerDetailManager = appointmentEmployerDetailManager;
        _appointmentRepository = appointmentRepository;
        _stateRepository = stateRepository;
        _childOwnershipGuard = childOwnershipGuard;
    }
    [Authorize]
    public virtual async Task<PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>> GetListAsync(GetAppointmentEmployerDetailsInput input)
    {
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var totalCount = await _appointmentEmployerDetailRepository.GetCountAsync(input.FilterText, input.EmployerName, input.PhoneNumber, input.Street, input.City, input.AppointmentId, input.StateId, restrictToAppointmentIds: readableAppointmentIds);
        var items = await _appointmentEmployerDetailRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.EmployerName, input.PhoneNumber, input.Street, input.City, input.AppointmentId, input.StateId, input.Sorting, input.MaxResultCount, input.SkipCount, restrictToAppointmentIds: readableAppointmentIds);
        return new PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentEmployerDetailWithNavigationProperties>, List<AppointmentEmployerDetailWithNavigationPropertiesDto>>(items)
        };
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
    public virtual async Task<AppointmentEmployerDetailWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _appointmentEmployerDetailRepository.GetWithNavigationPropertiesAsync(id) ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AppointmentEmployerDetail), id);
        await _childOwnershipGuard.EnsureIsPartyAsync(item.AppointmentEmployerDetail.AppointmentId);
        return ObjectMapper.Map<AppointmentEmployerDetailWithNavigationProperties, AppointmentEmployerDetailWithNavigationPropertiesDto>(item);
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
    public virtual async Task<AppointmentEmployerDetailDto> GetAsync(Guid id)
    {
        var entity = await _appointmentEmployerDetailRepository.GetAsync(id);
        // Reading a child row is reading its parent appointment: the .Default permission ties the caller to no appointment.
        await _childOwnershipGuard.EnsureIsPartyAsync(entity.AppointmentId);
        return ObjectMapper.Map<AppointmentEmployerDetail, AppointmentEmployerDetailDto>(entity);
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLookupAsync(LookupRequestDto input)
    {
        // Narrowed to appointments the caller may reach, in the query itself so the count and paging cannot leak the rest. Internal callers get null (no narrowing).
        var readableAppointmentIds = await _childOwnershipGuard.GetReadableAppointmentIdsAsync();
        var query = (await _appointmentRepository.GetQueryableAsync()).WhereIf(readableAppointmentIds != null, x => readableAppointmentIds!.Contains(x.Id)).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.RequestConfirmationNumber != null && x.RequestConfirmationNumber.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.Appointments.Appointment>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.Appointments.Appointment>, List<LookupDto<Guid>>>(lookupData)
        };
    }
    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Default)]
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

    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _appointmentEmployerDetailRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Create)]
    public virtual async Task<AppointmentEmployerDetailDto> CreateAsync(AppointmentEmployerDetailCreateDto input)
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

        var appointmentEmployerDetail = await _appointmentEmployerDetailManager.CreateAsync(
            input.AppointmentId,
            input.StateId,
            input.EmployerName,
            input.Occupation,
            input.PhoneNumber,
            input.Street,
            input.City,
            input.ZipCode);
        return ObjectMapper.Map<AppointmentEmployerDetail, AppointmentEmployerDetailDto>(appointmentEmployerDetail);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentEmployerDetails.Edit)]
    public virtual async Task<AppointmentEmployerDetailDto> UpdateAsync(Guid id, AppointmentEmployerDetailUpdateDto input)
    {
        if (input.AppointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        // The caller must be a party to the row's OWN parent, and may not move the row to a
        // different one. The parent is read from the stored row, never from the request: checking
        // the supplied id would let a caller nominate an appointment they are a party to and still
        // write to somebody else's row. Every external role holds this service's Edit permission.
        var existingChild = await _appointmentEmployerDetailRepository.GetAsync(id);
        await _childOwnershipGuard.EnsureCanWriteChildAsync(existingChild.AppointmentId, input.AppointmentId);


        var appointmentEmployerDetail = await _appointmentEmployerDetailManager.UpdateAsync(
            id,
            input.AppointmentId,
            input.StateId,
            input.EmployerName,
            input.Occupation,
            input.PhoneNumber,
            input.Street,
            input.City,
            input.ZipCode,
            input.ConcurrencyStamp);
        return ObjectMapper.Map<AppointmentEmployerDetail, AppointmentEmployerDetailDto>(appointmentEmployerDetail);
    }
}