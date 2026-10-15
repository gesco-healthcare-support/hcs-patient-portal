using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- anonymous, token-addressed consent surface for the
/// opposing side. <see cref="GetConsentInfoAsync"/> backs the GET landing page
/// (no state change; safe for email-scanner prefetch); <see cref="SubmitDecisionAsync"/>
/// records the Yes/No via the single-use token. Exposed via the manual
/// <c>PublicChangeRequestConsentController</c> at <c>api/public/change-request-consent</c>.
/// </summary>
public interface IPublicChangeRequestConsentAppService : IApplicationService
{
    Task<ChangeRequestConsentInfoDto> GetConsentInfoAsync(string token);

    Task<ChangeRequestConsentInfoDto> SubmitDecisionAsync(string token, SubmitChangeRequestConsentDto input);
}
