using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;

/// <summary>
/// R2-4 (2026-06-22): self-scoped claim-examiner profile. Lets a signed-in claim
/// examiner read and edit ONLY their own master (resolved from CurrentUser.Id). ABP
/// auto-exposes this as an HTTP API. Mirrors IMyAttorneyProfileAppService (#9).
/// </summary>
public interface IMyClaimExaminerProfileAppService : IApplicationService
{
    Task<MyClaimExaminerProfileDto> GetAsync();

    Task<MyClaimExaminerProfileDto> UpdateAsync(UpdateMyClaimExaminerProfileInput input);
}
