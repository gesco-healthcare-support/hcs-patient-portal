using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;

/// <summary>
/// #9 (2026-06-19): self-scoped attorney profile. Lets a signed-in applicant/defense
/// attorney read and edit ONLY their own master (resolved from CurrentUser.Id). ABP
/// auto-exposes this as an HTTP API (no [RemoteService] override).
/// </summary>
public interface IMyAttorneyProfileAppService : IApplicationService
{
    Task<MyAttorneyProfileDto> GetAsync();

    Task<MyAttorneyProfileDto> UpdateAsync(UpdateMyAttorneyProfileInput input);
}
