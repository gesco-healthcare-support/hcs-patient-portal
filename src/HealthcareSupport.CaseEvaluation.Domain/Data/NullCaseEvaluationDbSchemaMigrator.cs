using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Data;

/* This is used if database provider does't define
 * ICaseEvaluationDbSchemaMigrator implementation.
 */
public class NullCaseEvaluationDbSchemaMigrator : ICaseEvaluationDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
