using System;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

public interface INotificationTemplateTypeRepository : IRepository<NotificationTemplateType, Guid>
{
}
