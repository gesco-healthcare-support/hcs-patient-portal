using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Settings;

public class CaseEvaluationSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(CaseEvaluationSettings.MySetting1));
    }
}
