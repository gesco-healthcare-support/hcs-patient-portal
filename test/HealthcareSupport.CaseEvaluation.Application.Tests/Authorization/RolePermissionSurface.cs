using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// Renders which seeded role holds which permission, on the host and in an office, beside each
/// permission's declared tenancy side, as a deterministic, sorted table.
///
/// WHY THIS EXISTS. <c>docs/security/AUTHORIZATION.md</c> restated these facts by hand and was
/// wrong about them more than once: it marked twelve permission groups with the wrong tenancy side,
/// listed two permissions that do not exist, said only two roles were seeded when seven named roles
/// are, and gave two internal roles an office-only scope although they are seeded on the host too.
/// A restated fact drifts; a regenerated one cannot drift silently.
///
/// WHAT IT ADDS TO <see cref="AuthorizationSurface"/>. That snapshot is the DEMAND side: the
/// permission each application-service method asks for. This one is the SUPPLY side: who is granted
/// each permission, and on which side. It names no endpoints, so no line repeats the other.
///
/// WHAT IT RECORDS, AND HOW. The caller observes a real seeding run (the grants and roles written to
/// the stores) and passes the observations in. Nothing here re-derives what a seeder is SUPPOSED to
/// grant: a derived table goes blind exactly where the derivation is wrong, while an observed one
/// shows every grant, including ones written by an inline literal or by the framework's own
/// <c>admin</c> seeding.
///
/// WHAT IT DOES NOT PROVE.
/// - That a grant reaches any user: which user holds which role is not recorded.
/// - That a permission check runs: both test harnesses call <c>AddAlwaysAllowAuthorization()</c>.
/// - Per-record access, such as the appointment accessor rules, which permissions do not express.
/// - What a long-lived database holds: seeders only ever add, so a database seeded before a grant
///   was removed from code can still carry it.
/// </summary>
public static class RolePermissionSurface
{
    /// <summary>Rendered where no seeded role holds the permission on that side.</summary>
    public const string None = "-";

    /// <summary>Rendered as the side of a granted permission that has no definition.</summary>
    public const string Undefined = "(undefined)";

    /// <summary>This repository's permission names all start with this prefix.</summary>
    public const string OwnPrefix = "CaseEvaluation.";

    /// <summary>
    /// The framework's static superuser role. Its grants of framework permissions are left out,
    /// because every framework upgrade would churn the snapshot while saying nothing about this
    /// repository's own seeding.
    /// </summary>
    public const string AdminRole = "admin";

    /// <summary>One observed role grant.</summary>
    /// <param name="Permission">The granted permission name.</param>
    /// <param name="Role">The role it is granted to.</param>
    /// <param name="OnHost">True for a host grant, false for an office grant.</param>
    public sealed record Grant(string Permission, string Role, bool OnHost);

    /// <summary>
    /// Renders the table. A row exists for every <see cref="OwnPrefix"/> permission that is defined
    /// or granted, and for every other permission granted to a role other than <see cref="AdminRole"/>.
    /// Sorting is ordinal, so the output is byte-identical on every machine.
    /// </summary>
    /// <param name="hostRoles">Names of the roles that exist on the host.</param>
    /// <param name="officeRoles">Names of the roles that exist in the observed office.</param>
    /// <param name="grants">Every observed role grant, host and office.</param>
    /// <param name="definitions">Every defined permission name and its declared side.</param>
    /// <returns>The rendered table, LF line endings, ending in a newline.</returns>
    public static string Render(
        IEnumerable<string> hostRoles,
        IEnumerable<string> officeRoles,
        IReadOnlyCollection<Grant> grants,
        IReadOnlyDictionary<string, MultiTenancySides> definitions)
    {
        ArgumentNullException.ThrowIfNull(hostRoles);
        ArgumentNullException.ThrowIfNull(officeRoles);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(definitions);

        var builder = new StringBuilder();
        builder.Append("role host: ").Append(JoinSorted(hostRoles)).Append('\n');
        builder.Append("role office: ").Append(JoinSorted(officeRoles)).Append('\n');

        foreach (var permission in Rows(grants, definitions))
        {
            builder.Append(permission)
                   .Append(" side=")
                   .Append(definitions.TryGetValue(permission, out var side) ? SideName(side) : Undefined)
                   .Append(" host=")
                   .Append(JoinSorted(grants.Where(g => g.OnHost && g.Permission == permission).Select(g => g.Role)))
                   .Append(" office=")
                   .Append(JoinSorted(grants.Where(g => !g.OnHost && g.Permission == permission).Select(g => g.Role)))
                   .Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>The permissions that get a row, sorted ordinal.</summary>
    /// <param name="grants">Every observed role grant.</param>
    /// <param name="definitions">Every defined permission name and its declared side.</param>
    /// <returns>The distinct permission names, sorted.</returns>
    public static IReadOnlyList<string> Rows(
        IReadOnlyCollection<Grant> grants,
        IReadOnlyDictionary<string, MultiTenancySides> definitions)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(definitions);

        return definitions.Keys.Where(IsOwn)
            .Concat(grants.Where(g => IsOwn(g.Permission) || g.Role != AdminRole).Select(g => g.Permission))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The side as the snapshot writes it.</summary>
    /// <param name="side">The declared side.</param>
    /// <returns>Host, Tenant or Both.</returns>
    public static string SideName(MultiTenancySides side) => side switch
    {
        MultiTenancySides.Host => "Host",
        MultiTenancySides.Tenant => "Tenant",
        MultiTenancySides.Both => "Both",
        _ => side.ToString(),
    };

    private static bool IsOwn(string permission) => permission.StartsWith(OwnPrefix, StringComparison.Ordinal);

    private static string JoinSorted(IEnumerable<string> names)
    {
        var sorted = names.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return sorted.Count == 0 ? None : string.Join(", ", sorted);
    }
}
