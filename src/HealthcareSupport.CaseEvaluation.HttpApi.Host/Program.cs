using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Hosting;

namespace HealthcareSupport.CaseEvaluation;

public static class Program
{
    // The whole bootstrap lives in CaseEvaluationHost.RunAsync, shared with the other
    // host (#775 / #871). These two files were 64 lines each differing in four, so every
    // edit to either landed inside the duplicated block Sonar reports.
    public static Task<int> Main(string[] args) =>
        CaseEvaluationHost.RunAsync<CaseEvaluationHttpApiHostModule>(
            "HealthcareSupport.CaseEvaluation.HttpApi.Host", args);
}
