using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// No permission may reach a side its parent does not: a child's <see cref="MultiTenancySides"/> must
/// be contained in its parent's, for every permission this app defines.
///
/// <para><b>Why the framework does not do this for us.</b> In ABP 10.0.2 <c>AddChild</c> defaults the
/// child's side to <see cref="MultiTenancySides.Both"/> whatever the parent says, and the permission
/// checker reads the child's OWN side, never its parent's. So declaring a parent Host-only protects
/// nothing below it unless every child repeats the side. That is how
/// <c>IntakeAssignments.Manage</c> came to be granted to every office's admin role until #1147
/// passed <c>MultiTenancySides.Host</c> to it. The code assumed an invariant the framework does not
/// enforce; this test enforces it, so the next child added without a side fails here by name rather
/// than shipping.</para>
///
/// <para>"Contained in" rather than "not wider": a Tenant child under a Host parent is as wrong as a
/// Both one, and only a subset check catches both.</para>
///
/// <para>Existing grants are not this test's concern. Office grant rows of a permission that became
/// Host-only are inert (ABP refuses a Host-only permission inside an office before reading any
/// grant) and are removed on every deploy by <c>HostOnlyPermissionGrantCleanupContributor</c>.</para>
/// </summary>
public abstract class PermissionSideInvariantTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task EveryChildPermission_StaysWithinItsParentsMultiTenancySide()
    {
        var pairs = await CollectParentChildPairsAsync();

        var violations = pairs
            .Where(pair => (pair.Child.MultiTenancySide & pair.Parent.MultiTenancySide) != pair.Child.MultiTenancySide)
            .Select(pair => $"{pair.Child.Name} is {pair.Child.MultiTenancySide} under {pair.Parent.Name}, which is {pair.Parent.MultiTenancySide}")
            .ToList();

        violations.ShouldBeEmpty(
            "A child permission without an explicit side defaults to Both, and ABP checks the child's own "
            + "side -- so each of these is grantable where its parent is not. Pass the parent's side to AddChild.");
    }

    [Fact]
    public async Task TheSweep_ReachesTheChildrenWhoseSideIsLoadBearing()
    {
        // Positive control. A reflection-style sweep that walks nothing passes the test above for the
        // wrong reason; this fails instead. Each pair is one the invariant actually depends on: the
        // two children that must narrow their parent's side, and one ordinary child of a parent that
        // declares its side explicitly.
        var pairs = (await CollectParentChildPairsAsync())
            .Select(pair => (Parent: pair.Parent.Name, Child: pair.Child.Name))
            .ToList();

        pairs.ShouldContain((CaseEvaluationPermissions.IntakeAssignments.Default, CaseEvaluationPermissions.IntakeAssignments.Manage));
        pairs.ShouldContain((CaseEvaluationPermissions.Appointments.Default, CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters));
        pairs.ShouldContain((CaseEvaluationPermissions.InternalUsers.Default, CaseEvaluationPermissions.InternalUsers.Create));

        // And the walk covers the definition provider broadly, not just those three: the provider
        // declares 97 children today (one AddChild call each). The floor sits a little below that so
        // adding a permission never breaks it, while a walk that stopped at the top level -- which
        // finds none -- or at one group cannot get near it.
        pairs.Count.ShouldBeGreaterThanOrEqualTo(90);
    }

    private async Task<List<(PermissionDefinition Parent, PermissionDefinition Child)>> CollectParentChildPairsAsync()
    {
        var groups = await GetRequiredService<IPermissionDefinitionManager>().GetGroupsAsync();
        var pairs = new List<(PermissionDefinition Parent, PermissionDefinition Child)>();
        foreach (var group in groups.Where(g => g.Name.StartsWith(CaseEvaluationPermissions.GroupName)))
        {
            foreach (var permission in group.Permissions)
            {
                AddPairs(permission, pairs);
            }
        }
        return pairs;
    }

    private static void AddPairs(PermissionDefinition parent, List<(PermissionDefinition Parent, PermissionDefinition Child)> pairs)
    {
        foreach (var child in parent.Children)
        {
            pairs.Add((parent, child));
            AddPairs(child, pairs);
        }
    }
}
