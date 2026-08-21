using System;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Documents;

public interface IDocumentRepository : IRepository<Document, Guid>
{
}
