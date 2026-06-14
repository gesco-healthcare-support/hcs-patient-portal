using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Send Back / Request-more-information (2026-06-14). Staff flag fields + add a
/// note (Pending -&gt; InfoRequested); the external party resubmits their
/// corrections (InfoRequested -&gt; Pending). The note + flagged-field list are
/// returned un-masked so the external fix-it page can render them. The flagged
/// fields are stored as a JSON array, so DTO mapping is manual (Mapperly cannot
/// deserialize JSON).
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentInfoRequestsAppService
    : CaseEvaluationAppService, IAppointmentInfoRequestsAppService
{
    private readonly IRepository<AppointmentInfoRequest, Guid> _infoRequestRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly AppointmentManager _appointmentManager;
    private readonly AppointmentReadAccessGuard _readAccessGuard;

    public AppointmentInfoRequestsAppService(
        IRepository<AppointmentInfoRequest, Guid> infoRequestRepository,
        IAppointmentRepository appointmentRepository,
        AppointmentManager appointmentManager,
        AppointmentReadAccessGuard readAccessGuard)
    {
        _infoRequestRepository = infoRequestRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _readAccessGuard = readAccessGuard;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Approve)]
    public virtual async Task<AppointmentInfoRequestDto> SendBackAsync(
        Guid appointmentId,
        SendBackAppointmentInput input)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Appointment"]]);
        }
        if (input == null || string.IsNullOrWhiteSpace(input.Note))
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:NoteRequired"]);
        }

        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        if (appointment.AppointmentStatus != AppointmentStatusType.Pending)
        {
            throw new UserFriendlyException(L["AppointmentInfoRequest:OnlyPendingCanBeSentBack"]);
        }

        var fieldsJson = JsonSerializer.Serialize(input.FlaggedFields ?? new List<FlaggedFieldDto>());
        var entity = new AppointmentInfoRequest(
            GuidGenerator.Create(),
            appointment.TenantId,
            appointmentId,
            input.Note.Trim(),
            fieldsJson,
            CurrentUser.Id);
        await _infoRequestRepository.InsertAsync(entity, autoSave: true);

        // Fire the Pending -> InfoRequested transition (re-validates the source
        // status + publishes the status-changed event for notifications).
        await _appointmentManager.SendBackAsync(appointmentId, CurrentUser.Id);

        return MapToDto(entity);
    }

    [Authorize]
    public virtual async Task ResubmitAsync(Guid appointmentId)
    {
        // Party-scoped: the creator, internal staff, or an Edit-accessor. The
        // external user's corrected field values are saved through the existing
        // patient / document endpoints before this transition-only call.
        await _readAccessGuard.EnsureCanEditAsync(appointmentId);

        var open = await GetOpenEntityAsync(appointmentId);
        if (open != null)
        {
            open.MarkResolved(Clock.Now);
            await _infoRequestRepository.UpdateAsync(open, autoSave: true);
        }

        await _appointmentManager.ResubmitInfoAsync(appointmentId, CurrentUser.Id);
    }

    [Authorize]
    public virtual async Task<AppointmentInfoRequestDto?> GetOpenAsync(Guid appointmentId)
    {
        await _readAccessGuard.EnsureCanReadAsync(appointmentId);
        var open = await GetOpenEntityAsync(appointmentId);
        return open == null ? null : MapToDto(open);
    }

    private async Task<AppointmentInfoRequest?> GetOpenEntityAsync(Guid appointmentId)
    {
        return await _infoRequestRepository.FirstOrDefaultAsync(
            r => r.AppointmentId == appointmentId && r.Status == InfoRequestStatus.Open);
    }

    private static AppointmentInfoRequestDto MapToDto(AppointmentInfoRequest e)
    {
        List<FlaggedFieldDto> fields;
        try
        {
            fields = JsonSerializer.Deserialize<List<FlaggedFieldDto>>(e.RequestedFields)
                     ?? new List<FlaggedFieldDto>();
        }
        catch (JsonException)
        {
            fields = new List<FlaggedFieldDto>();
        }

        return new AppointmentInfoRequestDto
        {
            Id = e.Id,
            AppointmentId = e.AppointmentId,
            Note = e.Note,
            FlaggedFields = fields,
            Status = e.Status,
            RequestedByUserId = e.RequestedByUserId,
            CreationTime = e.CreationTime,
            ResolvedAt = e.ResolvedAt,
        };
    }
}
