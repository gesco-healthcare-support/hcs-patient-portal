using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
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

    public virtual async Task<NotificationTemplate?> FindByCodeAsync(
        string templateCode,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .Where(x => x.TemplateCode == templateCode && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<NotificationTemplateWithNavigationProperties?> GetWithNavigationPropertiesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildJoinedQueryAsync();
        return await query.FirstOrDefaultAsync(x => x.NotificationTemplate.Id == id, cancellationToken);
    }

    public virtual async Task<NotificationTemplateWithNavigationProperties?> FindWithNavigationPropertiesByCodeAsync(
        string templateCode,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildJoinedQueryAsync();
        return await query
            .Where(x => x.NotificationTemplate.TemplateCode == templateCode
                        && x.NotificationTemplate.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<NotificationTemplateWithNavigationProperties>> GetListWithNavigationPropertiesAsync(
        string? filterText = null,
        Guid? templateTypeId = null,
        bool? isActive = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await BuildJoinedQueryAsync(), filterText, templateTypeId, isActive);
        query = ApplySorting(query, sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(
        string? filterText = null,
        Guid? templateTypeId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(await BuildJoinedQueryAsync(), filterText, templateTypeId, isActive);
        return await query.LongCountAsync(cancellationToken);
    }

    private async Task<IQueryable<NotificationTemplateWithNavigationProperties>> BuildJoinedQueryAsync()
    {
        var dbContext = await GetDbContextAsync();
        return from template in (await GetQueryableAsync())
               join type in dbContext.Set<NotificationTemplateType>()
                   on template.TemplateTypeId equals type.Id into typeGroup
               from type in typeGroup.DefaultIfEmpty()
               select new NotificationTemplateWithNavigationProperties
               {
                   NotificationTemplate = template,
                   NotificationTemplateType = type,
               };
    }

    private static IQueryable<NotificationTemplateWithNavigationProperties> ApplyFilter(
        IQueryable<NotificationTemplateWithNavigationProperties> query,
        string? filterText,
        Guid? templateTypeId,
        bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            query = query.Where(x => x.NotificationTemplate.TemplateCode.Contains(filterText!));
        }
        if (templateTypeId.HasValue)
        {
            query = query.Where(x => x.NotificationTemplate.TemplateTypeId == templateTypeId.Value);
        }
        if (isActive.HasValue)
        {
            query = query.Where(x => x.NotificationTemplate.IsActive == isActive.Value);
        }
        return query;
    }

    private static IQueryable<NotificationTemplateWithNavigationProperties> ApplySorting(
        IQueryable<NotificationTemplateWithNavigationProperties> query,
        string? sorting)
    {
        // Default sort by TemplateCode ascending so the editor list is
        // stable across calls. ABP convention: System.Linq.Dynamic.Core
        // accepts a property-path string at the projection root.
        var effective = string.IsNullOrWhiteSpace(sorting)
            ? "NotificationTemplate.TemplateCode asc"
            : NormalizeSorting(sorting!);
        return query.OrderBy(effective);
    }

    /// <summary>
    /// ABP's PagedAndSortedResultRequestDto sends sortings like
    /// "templateCode asc". Translate to the projection-root form so the
    /// LINQ-Dynamic Core OrderBy can resolve it.
    /// </summary>
    private static string NormalizeSorting(string sorting)
    {
        Check.NotNull(sorting, nameof(sorting));
        // If the caller already includes the prefix, trust it.
        if (sorting.Contains('.'))
        {
            return sorting;
        }
        return $"NotificationTemplate.{sorting}";
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
