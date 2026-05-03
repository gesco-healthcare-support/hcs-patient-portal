using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

public class EfCoreNotificationTemplateRepository
    : EfCoreRepository<CaseEvaluationDbContext, NotificationTemplate, Guid>, INotificationTemplateRepository
{
    public EfCoreNotificationTemplateRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<NotificationTemplate?> FindByCodeAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .Where(x => x.TemplateCode == templateCode && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class EfCoreNotificationTemplateTypeRepository
    : EfCoreRepository<CaseEvaluationDbContext, NotificationTemplateType, Guid>, INotificationTemplateTypeRepository
{
    public EfCoreNotificationTemplateTypeRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
