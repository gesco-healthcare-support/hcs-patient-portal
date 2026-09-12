using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.EventBus.Local;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Submit-only Contact-Us AppService. Persists the query via
/// <see cref="UserQueryManager"/> then publishes
/// <see cref="UserQuerySubmittedEto"/> so the email side-effect runs in a
/// handler (commit-then-mail, matching OLD's order and every other NEW
/// notification flow).
///
/// <para>Class-level <c>[Authorize]</c> only -- any authenticated user may
/// submit, mirroring OLD's gate on <c>UserTypeEnum.ExternalUser</c>. There
/// is no per-action permission because external roles are not granted
/// feature permissions; the navbar surfaces the Help button only to them.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class UserQueryAppService : CaseEvaluationAppService, IUserQueryAppService
{
    private readonly UserQueryManager _userQueryManager;
    private readonly ILocalEventBus _localEventBus;

    public UserQueryAppService(
        UserQueryManager userQueryManager,
        ILocalEventBus localEventBus)
    {
        _userQueryManager = userQueryManager;
        _localEventBus = localEventBus;
    }

    public virtual async Task CreateAsync(UserQueryCreateDto input)
    {
        var userQuery = await _userQueryManager.CreateAsync(input.Message);

        await _localEventBus.PublishAsync(new UserQuerySubmittedEto
        {
            UserQueryId = userQuery.Id,
            Message = input.Message,
            RequestConfirmationNumber = input.RequestConfirmationNumber,
            SubmitterUserId = CurrentUser.Id,
            TenantId = CurrentTenant.Id,
            OccurredAt = Clock.Now,
        });
    }
}
