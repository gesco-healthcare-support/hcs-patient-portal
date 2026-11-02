using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Submit-only Contact-Us surface. Any authenticated user can submit a
/// query; the email goes to the office's notification inbox, or to all
/// IT-Admins when the office has none (it went to the appointment's
/// responsible user until 2026-11-02). There is no read/list method -- OLD shipped
/// no staff inbox UI, so this slice is write-only.
/// </summary>
public interface IUserQueryAppService : IApplicationService
{
    Task CreateAsync(UserQueryCreateDto input);
}
