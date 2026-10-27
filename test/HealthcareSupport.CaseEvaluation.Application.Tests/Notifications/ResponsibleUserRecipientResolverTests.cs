using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// <see cref="ResponsibleUserRecipientResolver"/> (2026-10-26): where an appointment's Responsible User mail
/// goes. A switched-on account gets it; a switched-off one never does -- the office's notification inbox
/// stands in when the office has one, otherwise nothing is addressed.
///
/// <para>The resolver is real; the user repository and the setting provider are substituted. The result
/// asserted is the returned address. Synthetic data only (HIPAA).</para>
/// </summary>
public class ResponsibleUserRecipientResolverTests
{
    private const string Inbox = "TEST-office-inbox@test.local";

    private static (ResponsibleUserRecipientResolver Resolver, IRepository<IdentityUser, Guid> Users, ISettingProvider Settings)
        Build(IdentityUser? stored, string? officeInbox = null)
    {
        var users = Substitute.For<IRepository<IdentityUser, Guid>>();
        users.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(stored);
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail).Returns(officeInbox);
        return (new ResponsibleUserRecipientResolver(users, settings, NullLogger<ResponsibleUserRecipientResolver>.Instance), users, settings);
    }

    private static IdentityUser User(string email, bool active = true)
    {
        var user = new IdentityUser(Guid.NewGuid(), "TEST-user", email);
        user.SetIsActive(active);
        return user;
    }

    [Fact]
    public async Task ASwitchedOnUser_GetsTheMail_AndTheInboxIsNotConsulted()
    {
        var staff = User("TEST-staff@test.local");
        var (resolver, _, settings) = Build(staff, Inbox);

        var recipient = (await resolver.ResolveAsync(staff.Id)).ShouldNotBeNull();

        recipient.Email.ShouldBe("TEST-staff@test.local");
        recipient.User.ShouldBe(staff);
        await settings.DidNotReceiveWithAnyArgs().GetOrNullAsync(default!);
    }

    [Fact]
    public async Task ASwitchedOffUser_IsReplacedByTheOfficeInbox()
    {
        var gone = User("TEST-left@test.local", active: false);
        var (resolver, _, _) = Build(gone, "  " + Inbox + " ");

        var recipient = (await resolver.ResolveAsync(gone.Id)).ShouldNotBeNull();

        recipient.Email.ShouldBe(Inbox);
        recipient.User.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ASwitchedOffUser_InAnOfficeWithoutAnInbox_GetsNothing(string? officeInbox)
    {
        var gone = User("TEST-left@test.local", active: false);
        var (resolver, _, _) = Build(gone, officeInbox);

        (await resolver.ResolveAsync(gone.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task AUserThatIsGone_OrHasNoAddress_ResolvesToNothing()
    {
        (await Build(stored: null, Inbox).Resolver.ResolveAsync(Guid.NewGuid())).ShouldBeNull();
        (await Build(User("   "), Inbox).Resolver.ResolveAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoOrEmptyUserId_ResolvesToNothing_WithoutReadingUsers(bool emptyGuid)
    {
        var (resolver, users, _) = Build(User("TEST-staff@test.local"), Inbox);

        (await resolver.ResolveAsync(emptyGuid ? Guid.Empty : null)).ShouldBeNull();
        await users.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
