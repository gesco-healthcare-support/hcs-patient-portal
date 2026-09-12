using System;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

public class EfCorePackageDetailRepository
    : EfCoreRepository<CaseEvaluationDbContext, PackageDetail, Guid>, IPackageDetailRepository
{
    public EfCorePackageDetailRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
