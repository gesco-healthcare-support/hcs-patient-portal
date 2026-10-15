using HealthcareSupport.CaseEvaluation.CustomFields;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.CustomFields;

public class EfCoreCustomFieldRepository
    : EfCoreRepository<CaseEvaluationDbContext, CustomField, Guid>, ICustomFieldRepository
{
    public EfCoreCustomFieldRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
