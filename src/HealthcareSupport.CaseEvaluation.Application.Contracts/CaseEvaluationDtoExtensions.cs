using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation;

public static class CaseEvaluationDtoExtensions
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        OneTimeRunner.Run(() => { });
    }
}
