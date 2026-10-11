using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// 2026-10-10 -- lets a signed-in party agree to or decline a reschedule or cancellation from
/// inside the portal, as an alternative to the single-use emailed link (which keeps working).
/// The caller is matched to a side by their VERIFIED email being that side's representative
/// on the appointment -- the same person the link was emailed to -- and must also be able to
/// read the appointment. Internal staff are never treated as a party here.
/// </summary>
public interface IPartyChangeRequestConsentAppService : IApplicationService
{
    /// <summary>
    /// The change requests waiting for the caller's answer, on one appointment or (when
    /// <paramref name="appointmentId"/> is null) on any appointment the caller can see.
    /// </summary>
    Task<ListResultDto<PartyConsentRequestDto>> GetMyPendingAsync(Guid? appointmentId);

    /// <summary>Records the caller's answer for every side of the request they represent.</summary>
    Task<PartyConsentRequestDto> SubmitMyDecisionAsync(Guid changeRequestId, SubmitChangeRequestConsentDto input);
}
