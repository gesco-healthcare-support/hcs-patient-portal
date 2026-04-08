using Microsoft.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

public class CaseEvaluationTenantDbContextFactory :
    CaseEvaluationDbContextFactoryBase<CaseEvaluationTenantDbContext>
{
    public CaseEvaluationTenantDbContextFactory()
        : base("TenantDevelopmentTime")
    {

    }

    protected override CaseEvaluationTenantDbContext CreateDbContext(
        DbContextOptions<CaseEvaluationTenantDbContext> dbContextOptions)
    {
        return new CaseEvaluationTenantDbContext(dbContextOptions);
    }
}
