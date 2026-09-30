using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for the two email lookups on <see cref="DocumentEmailContextResolver"/> that decide
/// WHERE a document email is addressed: the responsible staff user's email, and the uploader's email
/// with its fallback.
///
/// <para><b>WHAT IS PINNED.</b> No user id (or an empty one) resolves to no responsible email, and to
/// the fallback for an uploader without the user table being read. A found user's email wins; a user
/// that is gone, or has a blank email, falls back.</para>
///
/// <para><b>The result asserted is the returned address.</b> The resolver is real; only the identity
/// user repository is substituted. The other nine constructor arguments are null because these two
/// methods never touch them (the constructor only stores them) -- in particular
/// <c>IdentityUserManager</c> is not involved. Synthetic data only (HIPAA).</para>
/// </summary>
public class DocumentEmailContextResolverEmailLookupTests
{
    private static (DocumentEmailContextResolver Resolver, IRepository<IdentityUser, Guid> Users) Build(IdentityUser? stored)
    {
        var users = Substitute.For<IRepository<IdentityUser, Guid>>();
        users.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(stored);
        var resolver = new DocumentEmailContextResolver(
            null!, null!, null!, null!, users, null!, null!, null!, null!, null!);
        return (resolver, users);
    }

    private static IdentityUser User(string email) => new(Guid.NewGuid(), "TEST-user", email);

    [Fact]
    public async Task ResolveResponsibleUserEmailAsync_AStoredUser_ReturnsTheirEmail()
    {
        var (resolver, _) = Build(User("TEST-staff@test.local"));

        (await resolver.ResolveResponsibleUserEmailAsync(Guid.NewGuid())).ShouldBe("TEST-staff@test.local");
    }

    [Fact]
    public async Task ResolveResponsibleUserEmailAsync_AUserThatIsGone_ReturnsNull()
    {
        var (resolver, _) = Build(stored: null);

        (await resolver.ResolveResponsibleUserEmailAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveResponsibleUserEmailAsync_NoOrEmptyUserId_ReturnsNullWithoutReadingUsers(bool emptyGuid)
    {
        var (resolver, users) = Build(User("TEST-staff@test.local"));

        (await resolver.ResolveResponsibleUserEmailAsync(emptyGuid ? Guid.Empty : null)).ShouldBeNull();
        await users.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveUploaderEmailAsync_AStoredUserWithAnEmail_WinsOverTheFallback()
    {
        var (resolver, _) = Build(User("TEST-uploader@test.local"));

        (await resolver.ResolveUploaderEmailAsync(Guid.NewGuid(), "TEST-fallback@test.local"))
            .ShouldBe("TEST-uploader@test.local");
    }

    /// <summary>
    /// A user that is gone, or has no email on record, falls back to the supplied address (the patient's
    /// or the booker's) so the uploader is still told.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveUploaderEmailAsync_UserGoneOrWithoutAnEmail_UsesTheFallback(bool userExists)
    {
        var (resolver, _) = Build(userExists ? User("   ") : null);

        (await resolver.ResolveUploaderEmailAsync(Guid.NewGuid(), "TEST-fallback@test.local"))
            .ShouldBe("TEST-fallback@test.local");
    }

    /// <summary>
    /// An anonymous upload (no user id, or an empty one) goes straight to the fallback without reading
    /// the user table.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveUploaderEmailAsync_AnonymousUpload_UsesTheFallbackWithoutReadingUsers(bool emptyGuid)
    {
        var (resolver, users) = Build(User("TEST-uploader@test.local"));

        (await resolver.ResolveUploaderEmailAsync(emptyGuid ? Guid.Empty : null, "TEST-fallback@test.local"))
            .ShouldBe("TEST-fallback@test.local");
        await users.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
