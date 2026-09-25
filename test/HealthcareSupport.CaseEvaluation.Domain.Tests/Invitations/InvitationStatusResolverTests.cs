using HealthcareSupport.CaseEvaluation.Invitations;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// Pure unit tests for <see cref="InvitationStatusResolver"/> (no DB / DI).
/// Pins the status-derivation precedence used by the internal "Pending Invites"
/// list: Revoked > Accepted > Expired > Pending.
/// </summary>
public class InvitationStatusResolverTests
{
    private static readonly DateTime Now = new(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Future = Now.AddDays(1);
    private static readonly DateTime Past = Now.AddDays(-1);

    [Fact]
    public void Unaccepted_unexpired_active_is_Pending()
        => InvitationStatusResolver.Resolve(isDeleted: false, acceptedAt: null, expiresAt: Future, nowUtc: Now)
            .ShouldBe(InvitationStatus.Pending);

    [Fact]
    public void Past_expiry_is_Expired()
        => InvitationStatusResolver.Resolve(false, null, Past, Now)
            .ShouldBe(InvitationStatus.Expired);

    [Fact]
    public void Expiry_exactly_now_is_Expired()
        => InvitationStatusResolver.Resolve(false, null, Now, Now)
            .ShouldBe(InvitationStatus.Expired);

    [Fact]
    public void Accepted_is_Accepted()
        => InvitationStatusResolver.Resolve(false, Now, Future, Now)
            .ShouldBe(InvitationStatus.Accepted);

    [Fact]
    public void Accepted_outranks_Expired()
        => InvitationStatusResolver.Resolve(false, Past, Past, Now)
            .ShouldBe(InvitationStatus.Accepted);

    [Fact]
    public void Revoked_outranks_Pending()
        => InvitationStatusResolver.Resolve(true, null, Future, Now)
            .ShouldBe(InvitationStatus.Revoked);

    [Fact]
    public void Revoked_outranks_Accepted()
        => InvitationStatusResolver.Resolve(true, Now, Future, Now)
            .ShouldBe(InvitationStatus.Revoked);

    [Fact]
    public void Revoked_outranks_Expired()
        => InvitationStatusResolver.Resolve(true, null, Past, Now)
            .ShouldBe(InvitationStatus.Revoked);
}
