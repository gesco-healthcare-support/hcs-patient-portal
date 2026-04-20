using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

public static class CaseEvaluationEfCoreEntityExtensionMappings
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        CaseEvaluationGlobalFeatureConfigurator.Configure();
        CaseEvaluationModuleExtensionConfigurator.Configure();

        OneTimeRunner.Run(() => { });
    }
}
