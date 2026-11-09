using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// 2026-11-09: the invite's <c>##TenantName##</c> is now the office's display name, doctor and
/// practice ("Yuri Falkinstein, M.D. (West Coast Spine Institute)"), which already carries any title.
/// The shipped text put "Dr. " in front of it, which printed "Dr. falkinstein" from the slug and would
/// now print "Dr. Yuri Falkinstein, M.D.". These pin the shipped wording the seeder writes.
/// </summary>
public class InviteTemplateWordingTests
{
    private static NotificationTemplateSeedDefault Invite =>
        NotificationTemplateSeedDefaults.GetSeedDefaults(NotificationTemplateConsts.Codes.InviteExternalUser);

    [Fact]
    public void The_subject_reads_register_with_the_office_and_has_no_title_of_its_own()
    {
        Invite.Subject.ShouldBe("You have been invited to register with ##TenantName##.");
    }

    [Fact]
    public void The_body_names_the_office_without_a_title_of_its_own()
    {
        var body = Invite.BodyEmail;

        body.ShouldContain("<strong>##TenantName##</strong> has invited you");
        body.ShouldContain(">Register with ##TenantName##</a>");
        body.ShouldNotContain("Dr. ##TenantName##");
        body.ShouldNotContain("Register at ##TenantName##");
    }
}
