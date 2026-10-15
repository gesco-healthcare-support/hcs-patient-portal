using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Identity;
using NSubstitute;
using Shouldly;
using Volo.Abp.Identity.Settings;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

public class DisableSelfEmailChangeSettingTests
{
    [Fact]
    public void Self_service_email_change_defaults_to_off()
    {
        var definition = new SettingDefinition(IdentitySettingNames.User.IsEmailUpdateEnabled, "true");
        var context = Substitute.For<ISettingDefinitionContext>();
        context.GetOrNull(IdentitySettingNames.User.IsEmailUpdateEnabled).Returns(definition);

        new DisableSelfEmailChangeSettingDefinitionProvider().Define(context);

        definition.DefaultValue.ShouldBe("False");
    }
}
