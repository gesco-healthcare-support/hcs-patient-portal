using System;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Documents;

public class EfCoreDocumentRepository
    : EfCoreRepository<CaseEvaluationDbContext, Document, Guid>, IDocumentRepository
{
    public EfCoreDocumentRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
