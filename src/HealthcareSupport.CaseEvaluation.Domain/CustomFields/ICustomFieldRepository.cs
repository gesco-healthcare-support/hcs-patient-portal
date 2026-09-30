using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

public interface ICustomFieldRepository : IRepository<CustomField, Guid>
{
}
