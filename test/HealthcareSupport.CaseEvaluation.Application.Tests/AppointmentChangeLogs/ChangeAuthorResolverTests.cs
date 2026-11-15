using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// The "who" rules, over fake audit facts and a fake user directory. All names and ids are synthetic.
/// </summary>
public class ChangeAuthorResolverTests
{
    private readonly Guid _log = Guid.NewGuid();
    private readonly Guid _shadowId = Guid.NewGuid();
    private readonly Guid _operatorId = Guid.NewGuid();

    private async Task<ChangeAuthor> ResolveAsync(
        ChangeAuthorFacts facts,
        ChangeAuthorUser? officeUser = null,
        string? hostName = null,
        Guid? audit = null)
    {
        var id = audit ?? _log;
        var source = Substitute.For<IChangeAuthorSource>();
        source.LoadAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, ChangeAuthorFacts> { [_log] = facts });

        var directory = Substitute.For<IChangeAuthorDirectory>();
        directory.FindOfficeUserAsync(Arg.Any<Guid>()).Returns(officeUser);
        directory.FindHostOperatorNameAsync(Arg.Any<Guid>()).Returns(hostName);

        var result = await new ChangeAuthorResolver(source, directory).ResolveAsync(new[] { id });
        return result[id];
    }

    [Fact]
    public async Task The_impersonator_wins_over_the_shadow_user()
    {
        var facts = new ChangeAuthorFacts(_shadowId, "shadow.test", _operatorId, "operator.test", null, null);
        var shadow = new ChangeAuthorUser("Shadow Login", IsStaff: true, HostOperatorId: _operatorId);

        var author = await ResolveAsync(facts, shadow, hostName: "Pat Operator");

        author.Name.ShouldBe("Pat Operator");
        author.Kind.ShouldBe(ChangeAuthorKinds.HostStaff);
    }

    [Fact]
    public async Task A_gone_operator_falls_back_to_the_name_the_audit_row_kept()
    {
        var facts = new ChangeAuthorFacts(_shadowId, "shadow.test", _operatorId, "operator.test", null, null);

        var author = await ResolveAsync(facts, hostName: null);

        author.Name.ShouldBe("operator.test");
        author.Kind.ShouldBe(ChangeAuthorKinds.HostStaff);
    }

    [Fact]
    public async Task A_stamped_shadow_with_no_impersonator_resolves_to_the_operator()
    {
        var facts = new ChangeAuthorFacts(_shadowId, "shadow.test", null, null, null, null);
        var shadow = new ChangeAuthorUser("Shadow Login", IsStaff: true, HostOperatorId: _operatorId);

        var author = await ResolveAsync(facts, shadow, hostName: "Pat Operator");

        author.Name.ShouldBe("Pat Operator");
        author.Kind.ShouldBe(ChangeAuthorKinds.HostStaff);
    }

    [Fact]
    public async Task An_office_staff_login_is_Staff_and_shows_its_display_name()
    {
        var facts = new ChangeAuthorFacts(_shadowId, "sam.test", null, null, null, null);

        var author = await ResolveAsync(facts, new ChangeAuthorUser("Sam Supervisor", true, null));

        author.Name.ShouldBe("Sam Supervisor");
        author.Kind.ShouldBe(ChangeAuthorKinds.Staff);
    }

    [Fact]
    public async Task An_outside_login_is_Outside()
    {
        var facts = new ChangeAuthorFacts(_shadowId, "lee.test", null, null, null, null);

        var author = await ResolveAsync(facts, new ChangeAuthorUser("Lee Lawyer", false, null));

        author.Name.ShouldBe("Lee Lawyer");
        author.Kind.ShouldBe(ChangeAuthorKinds.Outside);
    }

    [Fact]
    public async Task A_deleted_user_keeps_the_sign_in_name_from_the_audit_row()
    {
        var facts = new ChangeAuthorFacts(_shadowId, "gone.test", null, null, null, null);

        var author = await ResolveAsync(facts, officeUser: null);

        author.Name.ShouldBe("gone.test");
    }

    [Fact]
    public async Task A_caller_with_no_user_on_the_integration_url_is_the_Case_Tracker()
    {
        var facts = new ChangeAuthorFacts(null, null, null, null, "/api/integration/offices/x/attendance", null);

        var author = await ResolveAsync(facts);

        author.Name.ShouldBe("Case Tracker");
        author.Kind.ShouldBe(ChangeAuthorKinds.CaseTracker);
    }

    [Theory]
    [InlineData("/api/public/change-request-consent/abc/respond")]
    [InlineData("/api/public/appointment-documents/upload")]
    public async Task A_caller_with_no_user_on_an_email_link_page_is_a_Party(string url)
    {
        var author = await ResolveAsync(new ChangeAuthorFacts(null, null, null, null, url, null));

        author.Name.ShouldBe("Party (email link)");
        author.Kind.ShouldBe(ChangeAuthorKinds.Party);
    }

    [Fact]
    public async Task Anything_else_without_a_user_is_System()
    {
        var author = await ResolveAsync(new ChangeAuthorFacts(null, null, null, null, "/hangfire/job", null));

        author.Name.ShouldBe("System");
        author.Kind.ShouldBe(ChangeAuthorKinds.System);
    }

    [Fact]
    public async Task An_audit_log_that_cannot_be_found_is_System()
    {
        var author = await ResolveAsync(
            new ChangeAuthorFacts(_shadowId, "x.test", null, null, null, null), audit: Guid.NewGuid());

        author.Kind.ShouldBe(ChangeAuthorKinds.System);
    }

    [Fact]
    public async Task One_person_across_many_audit_logs_costs_one_lookup()
    {
        var second = Guid.NewGuid();
        var source = Substitute.For<IChangeAuthorSource>();
        source.LoadAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(new Dictionary<Guid, ChangeAuthorFacts>
        {
            [_log] = new ChangeAuthorFacts(_shadowId, "s.test", _operatorId, "o.test", null, null),
            [second] = new ChangeAuthorFacts(_shadowId, "s.test", _operatorId, "o.test", null, null),
        });
        var directory = Substitute.For<IChangeAuthorDirectory>();
        directory.FindHostOperatorNameAsync(Arg.Any<Guid>()).Returns("Pat Operator");

        var result = await new ChangeAuthorResolver(source, directory).ResolveAsync(new[] { _log, second });

        result.Count.ShouldBe(2);
        await directory.Received(1).FindHostOperatorNameAsync(_operatorId);
    }

    [Fact]
    public void Staff_roles_are_recognised_case_insensitively()
    {
        ChangeAuthorRoles.IsStaff(new[] { "staff supervisor" }).ShouldBeTrue();
        ChangeAuthorRoles.IsStaff(new[] { "Applicant Attorney" }).ShouldBeFalse();
        ChangeAuthorRoles.IsStaff(Array.Empty<string>()).ShouldBeFalse();
    }
}
