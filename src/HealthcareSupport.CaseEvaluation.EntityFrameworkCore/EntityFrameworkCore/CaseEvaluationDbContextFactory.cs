using Microsoft.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

public class CaseEvaluationDbContextFactory :
    CaseEvaluationDbContextFactoryBase<CaseEvaluationDbContext>
{
    protected override CaseEvaluationDbContext CreateDbContext(
        DbContextOptions<CaseEvaluationDbContext> dbContextOptions)
    {
        return new CaseEvaluationDbContext(dbContextOptions);
    }
}
