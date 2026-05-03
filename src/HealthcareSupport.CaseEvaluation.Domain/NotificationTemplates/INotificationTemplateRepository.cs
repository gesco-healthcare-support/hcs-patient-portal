using System;
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
}
