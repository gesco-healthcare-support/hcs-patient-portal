using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Snapshots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// Pins <see cref="RolePermissionSurface"/> against the committed approved snapshot, from a REAL
/// seeding run: the host pass runs at harness start-up (<c>CaseEvaluationTestBaseModule</c>), and
/// each fact here seeds one office with the production contributors that write roles and grants
/// (see <c>GrantingContributors</c>), before reading what the stores hold.
///
/// It lives in this project because the fact spans two: permissions are defined in
/// Application.Contracts and granted by Domain seed contributors. A database-backed test must be
/// run through a startup module, so the concrete class is in EntityFrameworkCore.Tests; the approved
/// file stays beside this source file, where the path helper resolves it at compile time.
/// </summary>
/// <typeparam name="TStartupModule">The test startup module that supplies the database.</typeparam>
public abstract class RolePermissionSurfaceSnapshotTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string ApprovedFileName = "role-permission-surface.approved.txt";

    private static readonly ApprovedSnapshotWording Wording = new(
        Headline: "The role-permission surface changed.",
        IssueReference: null,
        RemovedMeaning: "a role lost a permission, a permission changed side, or a permission was removed");

    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// The office observed, created once per test by <see cref="OfficeAsync"/>. A FRESH office, not
    /// one of the harness's fixed test offices: those already hold test-only roles and grants, which
    /// would appear in the snapshot as if production seeding wrote them.
    /// </summary>
    private Guid? _office;

    protected RolePermissionSurfaceSnapshotTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Role_permission_surface_matches_the_approved_snapshot()
    {
        ApprovedSnapshot.AssertMatches(
            Render(await ObserveAsync()),
            Path.Combine(ThisDirectory(), ApprovedFileName),
            Wording);
    }

    [Fact]
    public async Task Every_granted_permission_is_defined()
    {
        var observation = await ObserveAsync();

        var undefined = observation.Grants
            .Where(g => !observation.Definitions.ContainsKey(g.Permission))
            .Select(g => $"{g.Permission} -> {g.Role} ({(g.OnHost ? "host" : "office")})")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        undefined.ShouldBeEmpty(
            "a seeder grants a permission that no definition provider declares, so the grant can " +
            "never be checked or managed: " + string.Join("; ", undefined));
    }

    [Fact]
    public async Task Surface_covers_the_seeded_roles_and_a_plausible_number_of_permissions()
    {
        var observation = await ObserveAsync();

        observation.HostRoles.ShouldContain("IT Admin");
        observation.HostRoles.ShouldContain(RolePermissionSurface.AdminRole);
        observation.OfficeRoles.ShouldContain("Patient");
        observation.OfficeRoles.ShouldContain(RolePermissionSurface.AdminRole);

        RolePermissionSurface.Rows(observation.Grants, observation.Definitions).Count.ShouldBeGreaterThan(50,
            "far fewer rows than this repository defines permissions means the definitions or the " +
            "grants were not read.");
    }

    [Fact]
    public async Task Render_is_deterministic_across_a_repeated_seeding_run()
    {
        var first = Render(await ObserveAsync());
        var second = Render(await ObserveAsync());

        second.ShouldBe(first, "seeding the same office twice must not change what the stores hold.");
    }

    [Fact]
    public void Approved_snapshot_is_stored_with_lf_endings()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ThisDirectory(), ApprovedFileName));

        bytes.Count(b => b == (byte)'\r').ShouldBe(0,
            $"{ApprovedFileName} contains CR bytes. It must be LF-only; check that .gitattributes " +
            "is not normalising it on checkout.");
    }

    private sealed record Observation(
        IReadOnlyList<string> HostRoles,
        IReadOnlyList<string> OfficeRoles,
        IReadOnlyCollection<RolePermissionSurface.Grant> Grants,
        IReadOnlyDictionary<string, MultiTenancySides> Definitions);

    private static string Render(Observation o) =>
        RolePermissionSurface.Render(o.HostRoles, o.OfficeRoles, o.Grants, o.Definitions);

    /// <summary>
    /// The seed contributors that write roles and role grants, run for the office in the order the
    /// data seeder registers them: the identity contributor (the office <c>admin</c> role), the
    /// framework's permission contributor (every permission of the office's side to <c>admin</c>),
    /// and this repository's internal and external role contributors.
    ///
    /// Why not <see cref="IDataSeeder"/> itself, measured rather than assumed: in this harness the
    /// full run for an office fails before any grant is read. The harness's own test-data
    /// contributor re-creates its fixed test offices and refuses the existing names, and the
    /// notification-template seeder re-inserts host rows into the single shared test database
    /// (a real office has its own database). Neither writes roles or grants.
    /// </summary>
    private static readonly Type[] GrantingContributors =
    [
        typeof(IdentityDataSeedContributor),
        typeof(PermissionDataSeedContributor),
        typeof(InternalUserRoleDataSeedContributor),
        typeof(ExternalUserRoleDataSeedContributor),
    ];

    /// <summary>
    /// Seeds the office with <see cref="GrantingContributors"/>, then reads roles and role grants on
    /// the host and in the office, and every permission definition.
    /// </summary>
    private async Task<Observation> ObserveAsync()
    {
        var office = await OfficeAsync();
        var seedContext = new DataSeedContext(office)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, "test.admin@example.test")
            .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, "TEST-Passw0rd!");

        // Assignable rather than equal: this repository replaces the framework's identity
        // contributor with CaseEvaluationIdentityDataSeedContributor, which derives from it.
        var registered = GetRequiredService<IOptions<AbpDataSeedOptions>>().Value.Contributors
            .Where(t => GrantingContributors.Any(g => g.IsAssignableFrom(t)))
            .ToList();
        var missing = GrantingContributors.Where(g => !registered.Any(g.IsAssignableFrom)).Select(g => g.Name).ToList();
        missing.ShouldBeEmpty("a granting seed contributor is no longer registered with the data seeder");

        await InScopeAsync(office, async () =>
        {
            foreach (var type in registered)
            {
                await ((IDataSeedContributor)ServiceProvider.GetRequiredService(type)).SeedAsync(seedContext);
            }

            return true;
        });

        var hostRoles = await RoleNamesAsync(null);
        var officeRoles = await RoleNamesAsync(office);
        var grants = (await RoleGrantsAsync(null, onHost: true))
            .Concat(await RoleGrantsAsync(office, onHost: false))
            .ToList();

        var definitions = (await GetRequiredService<IPermissionDefinitionManager>().GetPermissionsAsync())
            .ToDictionary(p => p.Name, p => p.MultiTenancySide, StringComparer.Ordinal);

        return new Observation(hostRoles, officeRoles, grants, definitions);
    }

    /// <summary>
    /// Creates the observed office on first use, as a bare tenant row with nothing seeded in it, and
    /// returns its id; later calls in the same test return the same office.
    /// </summary>
    private async Task<Guid> OfficeAsync()
    {
        _office ??= await InScopeAsync(null, async () =>
        {
            var office = await GetRequiredService<ITenantManager>()
                .CreateAsync("TEST-office-" + Guid.NewGuid().ToString("N")[..10]);
            await GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(office, autoSave: true);
            return office.Id;
        });

        return _office.Value;
    }

    private Task<List<string>> RoleNamesAsync(Guid? tenantId) =>
        InScopeAsync(tenantId, async () =>
            (await GetRequiredService<IIdentityRoleRepository>().GetListAsync())
                .Select(r => r.Name)
                .ToList());

    private Task<List<RolePermissionSurface.Grant>> RoleGrantsAsync(Guid? tenantId, bool onHost) =>
        InScopeAsync(tenantId, async () =>
            (await GetRequiredService<IPermissionGrantRepository>().GetListAsync())
                .Where(g => g.ProviderName == RolePermissionValueProvider.ProviderName)
                .Select(g => new RolePermissionSurface.Grant(g.Name, g.ProviderKey, onHost))
                .ToList());

    private Task<T> InScopeAsync<T>(Guid? tenantId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return await action();
            }
        });

    /// <summary>
    /// The directory holding this source file, resolved at compile time, so a developer updating the
    /// snapshot gets the path of the file to COMMIT rather than of a build artefact.
    /// </summary>
    private static string ThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
