using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decisions 1 + 2) -- staff stop and restart emails to external people, for one case or
/// for all cases at every practice. Every method requires <c>CaseEvaluation.EmailStops</c>. Routes are on
/// the hand-written <c>EmailStopsController</c>.
/// </summary>
public interface IEmailStopsAppService : IApplicationService
{
    /// <summary>Every external address this case emails, with who they are and any stop.</summary>
    Task<AppointmentEmailRecipientsDto> GetAppointmentRecipientsAsync(Guid appointmentId);

    /// <summary>Stops emails to an address for this case or for all cases. Idempotent.</summary>
    Task<EmailStopStatusDto> StopForAppointmentAsync(Guid appointmentId, StopAppointmentEmailsInput input);

    /// <summary>Turns emails back on for this case (removes the CASE stop only).</summary>
    Task ResumeForAppointmentAsync(Guid appointmentId, string email);

    /// <summary>Whether stopping this address would leave a side with nobody able to answer Agree/Decline.</summary>
    Task<EmailStopConsentImpactDto> GetConsentImpactAsync(Guid appointmentId, string email, EmailStopScope scope);

    /// <summary>The all-cases list (active stops only).</summary>
    Task<PagedResultDto<GlobalEmailStopDto>> GetListAsync(GetGlobalEmailStopsInput input);

    /// <summary>Stops emails to an address for all cases, with no case needed. Idempotent.</summary>
    Task<GlobalEmailStopDto> CreateAsync(CreateGlobalEmailStopInput input);

    /// <summary>Turns emails back on for all cases (soft delete; the history stays).</summary>
    Task DeleteAsync(Guid id);

    /// <summary>The all-cases warning for the "Stopped emails" page (this practice only).</summary>
    Task<EmailStopConsentImpactDto> GetGlobalConsentImpactAsync(string email);
}
