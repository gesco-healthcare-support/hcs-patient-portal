using Volo.Abp.AuditLogging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Reads the audit log rows (user, impersonator, URL) behind a set of changes with ONE batched
/// query. ABP's <see cref="IAuditLogRepository"/> has no "by ids" call, but its EF implementation is
/// also a plain repository, so the queryable is used when available and a per-id read otherwise.
/// </summary>
public class AuditLogChangeAuthorSource : IChangeAuthorSource, ITransientDependency
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly IServiceProvider _services;

    public AuditLogChangeAuthorSource(IAuditLogRepository auditLogs, IServiceProvider services)
    {
        _auditLogs = auditLogs;
        _services = services;
    }

    public virtual async Task<IReadOnlyDictionary<Guid, ChangeAuthorFacts>> LoadAsync(
        IReadOnlyCollection<Guid> auditLogIds)
    {
        var result = new Dictionary<Guid, ChangeAuthorFacts>();
        if (auditLogIds.Count == 0)
        {
            return result;
        }

        var ids = auditLogIds.ToList();
        var repository = _services.GetService(typeof(IRepository<AuditLog, Guid>)) as IRepository<AuditLog, Guid>
            ?? _auditLogs as IRepository<AuditLog, Guid>;
        var executer = _services.GetService(typeof(IAsyncQueryableExecuter)) as IAsyncQueryableExecuter;
        if (repository != null && executer != null)
        {
            var query = await repository.GetQueryableAsync();
            var rows = await executer.ToListAsync(
                query.Where(a => ids.Contains(a.Id))
                    .Select(a => new
                    {
                        a.Id,
                        a.UserId,
                        a.UserName,
                        a.ImpersonatorUserId,
                        a.ImpersonatorUserName,
                        a.Url,
                        a.ClientId,
                    }));
            foreach (var a in rows)
            {
                result[a.Id] = new ChangeAuthorFacts(
                    a.UserId, a.UserName, a.ImpersonatorUserId, a.ImpersonatorUserName, a.Url, a.ClientId);
            }
            return result;
        }

        foreach (var id in ids)
        {
            var a = await _auditLogs.FindAsync(id, includeDetails: false);
            if (a != null)
            {
                result[id] = new ChangeAuthorFacts(
                    a.UserId, a.UserName, a.ImpersonatorUserId, a.ImpersonatorUserName, a.Url, a.ClientId);
            }
        }
        return result;
    }
}
