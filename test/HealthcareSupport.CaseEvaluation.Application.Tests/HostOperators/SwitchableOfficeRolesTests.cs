using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// The in-office switcher lists every office for an operator in
/// <see cref="IntakeAssignmentsAppService.EveryOfficeHostRoles"/>, because the impersonation grant
/// admits holders of <c>Saas.Tenants.Impersonation</c> to every office. The switcher reads ROLES
/// (the operator is not the signed-in user, so it cannot ask the permission checker), and the grant
/// reads the PERMISSION. This keeps the two equal.
///
/// <para>WHY IT MATTERS. If a grant change gives that permission to another role, its holders could
/// still enter every office from the host but would see an EMPTY switcher menu inside one, with no
/// message. If a role loses it, its holders would be offered offices the grant then refuses.</para>
///
/// <para>The seeded holders come from <c>Authorization/role-permission-surface.approved.txt</c>,
/// which <c>RolePermissionSurfaceSnapshotTests</c> keeps equal to a real seeding run.</para>
/// </summary>
public class SwitchableOfficeRolesTests
{
    private const string ImpersonationPermission = "Saas.Tenants.Impersonation";

    [Fact]
    public void The_roles_offered_every_office_are_the_seeded_holders_of_the_impersonation_permission()
    {
        var line = File.ReadAllLines(SurfacePath())
            .SingleOrDefault(l => l.StartsWith(ImpersonationPermission + " ", StringComparison.Ordinal));
        line.ShouldNotBeNull($"{ImpersonationPermission} is missing from the role-permission surface.");

        var hostColumn = line!.Split(" host=")[1].Split(" office=")[0];
        var seededHolders = hostColumn == "-"
            ? Array.Empty<string>()
            : hostColumn.Split(", ");

        IntakeAssignmentsAppService.EveryOfficeHostRoles
            .OrderBy(r => r, StringComparer.Ordinal)
            .ShouldBe(seededHolders.OrderBy(r => r, StringComparer.Ordinal));
    }

    private static string SurfacePath([CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "..", "Authorization", "role-permission-surface.approved.txt");
}
