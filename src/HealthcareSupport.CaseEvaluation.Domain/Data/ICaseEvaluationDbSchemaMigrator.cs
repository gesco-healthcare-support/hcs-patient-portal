using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Data;

public interface ICaseEvaluationDbSchemaMigrator
{
    Task MigrateAsync();
}
