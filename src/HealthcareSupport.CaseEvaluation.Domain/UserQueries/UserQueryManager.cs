using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Domain service for creating <see cref="UserQuery"/> rows. The create
/// flow goes through the manager (not the AppService directly) per the
/// project convention that aggregate creation lives in a domain service.
/// The entity is the sole invariant holder (non-empty, max-length Message);
/// the manager just stamps the id and persists.
/// </summary>
public class UserQueryManager : DomainService
{
    private readonly IRepository<UserQuery, Guid> _userQueryRepository;

    public UserQueryManager(IRepository<UserQuery, Guid> userQueryRepository)
    {
        _userQueryRepository = userQueryRepository;
    }

    /// <summary>
    /// Persists a new query carrying <paramref name="message"/> and returns
    /// the saved aggregate. The submitter is captured by ABP audit
    /// (<c>CreatorId</c>) and the tenant by the multi-tenant filter.
    /// </summary>
    public virtual async Task<UserQuery> CreateAsync(string message)
    {
        var userQuery = new UserQuery(GuidGenerator.Create(), message);
        await _userQueryRepository.InsertAsync(userQuery, autoSave: true);
        return userQuery;
    }
}
