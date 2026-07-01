using System;
using System.Globalization;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- anonymous consent AppService (see
/// <see cref="IPublicChangeRequestConsentAppService"/>). The single-use token is the
/// only credential; the tenant is resolved from the request subdomain (same as the
/// public document-upload flow), so the IMultiTenant filter scopes the lookup. A
/// replay (already-responded) or expired token returns the current state for the
/// landing page rather than erroring -- idempotent by design.
/// </summary>
[AllowAnonymous]
[RemoteService(IsEnabled = false)]
public class PublicChangeRequestConsentAppService :
    CaseEvaluationAppService,
    IPublicChangeRequestConsentAppService
{
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;

    public PublicChangeRequestConsentAppService(
        ChangeRequestConsentManager consentManager,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> slotRepository)
    {
        _consentManager = consentManager;
        _appointmentRepository = appointmentRepository;
        _slotRepository = slotRepository;
    }

    [AllowAnonymous]
    public virtual async Task<ChangeRequestConsentInfoDto> GetConsentInfoAsync(string token)
    {
        var match = await _consentManager.ResolveByRawTokenAsync(token);
        return await BuildInfoAsync(match.Request, match.Side);
    }

    [AllowAnonymous]
    public virtual async Task<ChangeRequestConsentInfoDto> SubmitDecisionAsync(
        string token,
        SubmitChangeRequestConsentDto input)
    {
        Check.NotNull(input, nameof(input));
        try
        {
            var match = await _consentManager.RecordDecisionAsync(token, input.Approved, respondedByEmail: null);
            return await BuildInfoAsync(match.Request, match.Side);
        }
        catch (BusinessException ex) when (
            ex.Code == CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded ||
            ex.Code == CaseEvaluationDomainErrorCodes.ChangeRequestConsentExpired)
        {
            // Idempotent replay / expiry: surface the current decision state for the
            // landing page instead of a hard error. (Expiry already recorded the
            // default-No inside RecordDecisionAsync.)
            var match = await _consentManager.ResolveByRawTokenAsync(token);
            return await BuildInfoAsync(match.Request, match.Side);
        }
    }

    private async Task<ChangeRequestConsentInfoDto> BuildInfoAsync(AppointmentChangeRequest request, ChangeRequestSide side)
    {
        var appointment = await _appointmentRepository.FindAsync(request.AppointmentId);

        string? newDateTime = null;
        if (request.ChangeRequestType == ChangeRequestType.Reschedule
            && request.NewDoctorAvailabilityId.HasValue)
        {
            var slot = await _slotRepository.FindAsync(request.NewDoctorAvailabilityId.Value);
            if (slot != null)
            {
                var date = slot.AvailableDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
                var time = new DateTime(2000, 1, 1, slot.FromTime.Hour, slot.FromTime.Minute, slot.FromTime.Second)
                    .ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
                newDateTime = $"{date} at {time}";
            }
        }

        return new ChangeRequestConsentInfoDto
        {
            ConfirmationNumber = appointment?.RequestConfirmationNumber ?? string.Empty,
            ChangeRequestType = request.ChangeRequestType,
            Reason = request.ChangeRequestType == ChangeRequestType.Cancel
                ? request.CancellationReason
                : request.ReScheduleReason,
            RequestedNewDateTime = newDateTime,
            ConsentStatus = request.SideConsentStatus(side),
        };
    }
}
