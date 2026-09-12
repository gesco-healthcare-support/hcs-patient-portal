using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

public interface INotificationTemplateRepository : IRepository<NotificationTemplate, Guid>
{
    /// <summary>
    /// Resolves the active template for the given code in the current tenant
    /// scope. Returns null if none exists; callers should fall back to a
    /// hardcoded body and log the gap so seed coverage can be tightened.
    /// </summary>
    Task<NotificationTemplate?> FindByCodeAsync(string templateCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one template + its template type via a single LEFT JOIN.
    /// Null when the id does not exist in the current tenant scope.
    /// </summary>
    Task<NotificationTemplateWithNavigationProperties?> GetWithNavigationPropertiesAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="GetWithNavigationPropertiesAsync"/> but matches
    /// by <c>TemplateCode</c> within the current tenant scope. Used by the
    /// notification-handler resolution path.
    /// </summary>
    Task<NotificationTemplateWithNavigationProperties?> FindWithNavigationPropertiesByCodeAsync(
        string templateCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged list with text + type + active-flag filters. Mirrors OLD's
    /// <c>spm.spTemplates</c> stored proc semantics in the form expected
    /// by ABP's <c>PagedAndSortedResultRequestDto</c> caller.
    /// </summary>
    Task<List<NotificationTemplateWithNavigationProperties>> GetListWithNavigationPropertiesAsync(
        string? filterText = null,
        Guid? templateTypeId = null,
        bool? isActive = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    /// <summary>Count for the same filter set as <see cref="GetListWithNavigationPropertiesAsync"/>.</summary>
    Task<long> GetCountAsync(
        string? filterText = null,
        Guid? templateTypeId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
}
