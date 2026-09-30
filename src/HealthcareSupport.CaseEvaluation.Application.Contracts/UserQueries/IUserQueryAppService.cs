using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Submit-only Contact-Us surface. Any authenticated user can submit a
/// query; the email is routed to the appointment's responsible internal
/// user (when a confirmation number resolves to an Approved appointment) or
/// broadcast to all IT-Admins. There is no read/list method -- OLD shipped
/// no staff inbox UI, so this slice is write-only.
/// </summary>
public interface IUserQueryAppService : IApplicationService
{
    Task CreateAsync(UserQueryCreateDto input);
}
