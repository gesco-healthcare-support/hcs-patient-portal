using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.OpenIddict;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;
using Volo.Abp.Users;
using Volo.Saas.Host;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests;

/// <summary>
/// The deny-by-default gates of the office switch-in grant: who may switch into an office at all,
/// and, for a Host Intake operator, the per-office assignment check that is the security boundary.
/// </summary>
/// <remarks>
/// <para>
/// OFFICE DECOY: the Intake operator IS assigned to office A. Switching into office B must still be
/// refused, so the grant has to ask about the office it was given, not "is this operator assigned
/// anywhere". With the gate removed, the B switch would go on into the sign-in path instead of
/// returning the refusal asserted here.
/// </para>
/// <para>
/// NOT COVERED: the successful sign-in as the per-office shadow user. It runs through ABP's concrete
/// <c>IdentityUserManager</c>, the claims-principal factory and the base grant's session plumbing,
/// which this unit harness does not stand up.
/// </para>
/// </remarks>
public class HostIntakeImpersonationExtensionGrantTests
{
    private static readonly Guid OperatorId = Guid.Parse("7e570000-0000-4000-9000-000000000001");
    private static readonly Guid AssignedOfficeId = Guid.Parse("7e570000-0000-4000-9000-0000000000a1");
    private static readonly Guid OtherOfficeId = Guid.Parse("7e570000-0000-4000-9000-0000000000b2");
    private const string OperatorEmail = "TEST-intake.operator@test.local";
    private const string GrantType = "Impersonation";

    private readonly List<(Guid Operator, Guid Office)> _assignmentQuestions = new();
    private readonly List<Guid> _provisionedOffices = new();

    /// <summary>Exposes the protected switch-in entry point and sets the collaborators it reads.</summary>
    private sealed class TestableGrant : HostIntakeImpersonationExtensionGrant
    {
        public TestableGrant(IPermissionChecker permissions, ICurrentUser user)
        {
            permissionChecker = permissions;
            currentUser = user;
        }

        public Task<IActionResult> SwitchIntoAsync(ExtensionGrantContext context, Guid officeId) =>
            ImpersonateTenantAsync(context, new ClaimsPrincipal(new ClaimsIdentity()), officeId, "TEST-requested-admin");
    }

    private TestableGrant Grant(bool saasImpersonation, bool intakeImpersonation, Guid? operatorId, string? email)
    {
        var permissions = Substitute.For<IPermissionChecker>();
        permissions.IsGrantedAsync(SaasHostPermissions.Tenants.Impersonation).Returns(saasImpersonation);
        permissions.IsGrantedAsync(CaseEvaluationPermissions.IntakeImpersonation.Default).Returns(intakeImpersonation);

        var user = Substitute.For<ICurrentUser>();
        user.Id.Returns(operatorId);
        user.Email.Returns(email);
        user.UserName.Returns(email);

        return new TestableGrant(permissions, user);
    }

    private ExtensionGrantContext Context()
    {
        var assignments = Substitute.For<IIntakeAssignmentChecker>();
        assignments.IsAssignedAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(call =>
        {
            var operatorId = call.ArgAt<Guid>(0);
            var officeId = call.ArgAt<Guid>(1);
            _assignmentQuestions.Add((operatorId, officeId));
            // LOAD-BEARING DECOY: the operator is assigned to office A only.
            return Task.FromResult(operatorId == OperatorId && officeId == AssignedOfficeId);
        });

        var provisioner = Substitute.For<IIntakeShadowUserProvisioner>();
        provisioner.EnsureShadowUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>()).Returns(call =>
        {
            _provisionedOffices.Add(call.ArgAt<Guid>(0));
            return Task.FromResult(Guid.NewGuid());
        });

        var services = new ServiceCollection()
            .AddSingleton(assignments)
            .AddSingleton(provisioner)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        return new ExtensionGrantContext(httpContext, new OpenIddictRequest { GrantType = GrantType });
    }

    private static void ShouldBeRefused(IActionResult result, string expectedDescription)
    {
        var forbid = result.ShouldBeOfType<ForbidResult>();
        forbid.AuthenticationSchemes.ShouldBe(new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme });
        forbid.Properties!.Items[".error"].ShouldBe(OpenIddictConstants.Errors.InvalidRequest);
        forbid.Properties.Items[".error_description"].ShouldBe(expectedDescription);
        forbid.Properties.Parameters["grant_type"].ShouldBe(GrantType);
    }

    [Fact]
    public async Task A_user_with_neither_switch_permission_is_refused()
    {
        var grant = Grant(saasImpersonation: false, intakeImpersonation: false, OperatorId, OperatorEmail);

        var result = await grant.SwitchIntoAsync(Context(), AssignedOfficeId);

        ShouldBeRefused(result, "You are not permitted to switch into offices.");
        _assignmentQuestions.ShouldBeEmpty();
        _provisionedOffices.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_intake_operator_is_refused_an_office_they_are_not_assigned_to_even_when_assigned_to_another()
    {
        var grant = Grant(saasImpersonation: false, intakeImpersonation: true, OperatorId, OperatorEmail);

        var result = await grant.SwitchIntoAsync(Context(), OtherOfficeId);

        ShouldBeRefused(result, "You are not assigned to this office.");
        _assignmentQuestions.ShouldBe(new[] { (OperatorId, OtherOfficeId) });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task An_operator_whose_identity_cannot_be_resolved_is_refused_before_any_office_work(
        bool saasImpersonation, bool intakeImpersonation)
    {
        var noId = Grant(saasImpersonation, intakeImpersonation, operatorId: null, OperatorEmail);
        var noEmail = Grant(saasImpersonation, intakeImpersonation, OperatorId, email: "  ");

        ShouldBeRefused(await noId.SwitchIntoAsync(Context(), AssignedOfficeId), "Operator identity could not be resolved.");
        ShouldBeRefused(await noEmail.SwitchIntoAsync(Context(), AssignedOfficeId), "Operator identity could not be resolved.");

        _assignmentQuestions.ShouldBeEmpty();
        _provisionedOffices.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_supervisor_permission_takes_the_supervisor_path_and_skips_the_intake_assignment_gate()
    {
        // Holding both permissions: the broad SaaS one wins, so the Intake assignment gate is never asked.
        var grant = Grant(saasImpersonation: true, intakeImpersonation: true, operatorId: null, OperatorEmail);

        var result = await grant.SwitchIntoAsync(Context(), OtherOfficeId);

        ShouldBeRefused(result, "Operator identity could not be resolved.");
        _assignmentQuestions.ShouldBeEmpty();
    }
}
