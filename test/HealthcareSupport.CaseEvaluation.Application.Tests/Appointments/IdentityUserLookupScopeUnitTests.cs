using System;
using System.Linq;
using NSubstitute;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pure tests for <see cref="IdentityUserLookupScope"/>, over an in-memory list. The service-level
/// behaviour is pinned in <c>IdentityUserLookupScopeTests</c>; this covers the branches a signed-in
/// caller cannot reach, chiefly a principal with no user id.
/// </summary>
public class IdentityUserLookupScopeUnitTests
{
    private static readonly Guid SelfId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    private static IQueryable<IdentityUser> TwoUsers() => new[]
    {
        new IdentityUser(SelfId, "TEST-self", "TEST-self@test.local"),
        new IdentityUser(OtherId, "TEST-other", "TEST-other@test.local"),
    }.AsQueryable();

    private static ICurrentUser Caller(Guid? id, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Id.Returns(id);
        user.Roles.Returns(roles);
        return user;
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    [InlineData("IT Admin")]
    public void Internal_staff_see_every_account(string role)
    {
        IdentityUserLookupScope.ForCaller(TwoUsers(), Caller(SelfId, role))
            .Select(u => u.Id).ShouldBe(new[] { SelfId, OtherId }, ignoreOrder: true);
    }

    [Theory]
    [InlineData("Patient")]
    [InlineData("Claim Examiner")]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("TenantAdmin")] // not a production role; an unrecognised role must not count as staff
    public void Any_other_role_sees_only_its_own_account(string role)
    {
        IdentityUserLookupScope.ForCaller(TwoUsers(), Caller(SelfId, role))
            .Select(u => u.Id).ShouldBe(new[] { SelfId });
    }

    [Fact]
    public void An_account_with_no_role_sees_only_its_own_account()
    {
        IdentityUserLookupScope.ForCaller(TwoUsers(), Caller(SelfId))
            .Select(u => u.Id).ShouldBe(new[] { SelfId });
    }

    [Fact]
    public void A_caller_with_no_user_id_sees_nothing()
    {
        IdentityUserLookupScope.ForCaller(TwoUsers(), Caller(null, "Patient")).ShouldBeEmpty();
    }
}
