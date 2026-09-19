using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;
using HealthcareSupport.CaseEvaluation.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Everything a layer-3 test needs to name a caller: the seeded office, including its
/// patient, appointment and the booker identity user who OWNS the patient record.
/// </summary>
public record AuthorizationFixture(SeededOffice Office);

/// <summary>
/// Base class for #707 layer 3. Boots the one harness in this repository whose
/// authorization pipeline is real, points it at its own databases, and seeds the office,
/// the four production external roles with their real grants, and two purpose-built roles
/// that isolate the surfaces where all four production roles agree.
/// </summary>
public abstract class CaseEvaluationRealAuthorizationTestBase
    : AbpIntegratedTest<CaseEvaluationRealAuthorizationTestModule>
{
    public const string OfficeName = "F2-authz-office";

    /// <summary>
    /// A role holding ONE innocuous permission and none of the PHI ones.
    ///
    /// <para>IT MUST HOLD SOMETHING, AND THAT IS THE WHOLE POINT. #707 is explicit that a
    /// negative guarantee cannot be proven against an empty fixture: a role holding nothing
    /// is refused everywhere even by a harness that is simply broken, so the refusal
    /// carries no information. Because this role demonstrably reaches
    /// <see cref="MinimalRoleGrant"/>, its refusal on a PHI surface is attributable to the
    /// one permission it lacks rather than to the pipeline, the seed or the principal.</para>
    /// </summary>
    public const string MinimalRoleName = "F2-authz-minimal";

    /// <summary>The innocuous permission <see cref="MinimalRoleName"/> does hold.</summary>
    public const string MinimalRoleGrant = "CaseEvaluation.DoctorAvailabilities";

    /// <summary>
    /// A role holding ONLY the Case Tracker push permission, which no production external
    /// role holds. It is the positive control for that surface.
    /// </summary>
    public const string PushRoleName = "F2-authz-push";

    /// <summary>The permission <see cref="PushRoleName"/> holds.</summary>
    public const string PushRoleGrant = "CaseEvaluation.Appointments.PushToCaseTracker";

    /// <summary>Matches RolePermissionValueProvider.ProviderName.</summary>
    private const string RoleProviderName = "R";

    // The office, its databases and its tenant record live in process-wide state, so the
    // seed runs once for the whole run and the semaphore serializes the first call.
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static AuthorizationFixture? _fixture;

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json", optional: false);
        builder.AddJsonFile("appsettings.secrets.json", optional: true);
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = RealAuthorizationTestDatabase.HostConnectionString,
        });
        services.ReplaceConfiguration(builder.Build());
    }

    protected async Task<AuthorizationFixture> GetFixtureAsync()
    {
        if (_fixture != null)
        {
            return _fixture;
        }

        await SeedLock.WaitAsync();
        try
        {
            if (_fixture != null)
            {
                return _fixture;
            }

            RealAuthorizationTestDatabase.EnsureInitialized();

            var currentTenant = GetRequiredService<ICurrentTenant>();
            var tenantManager = GetRequiredService<ITenantManager>();
            var tenantRepository = GetRequiredService<IRepository<Tenant, Guid>>();
            var seeder = GetRequiredService<MultiOfficeSeeder>();
            var externalRoleSeeder = GetRequiredService<ExternalUserRoleDataSeedContributor>();
            var roleManager = GetRequiredService<IdentityRoleManager>();
            var permissionManager = GetRequiredService<IPermissionManager>();

            var fixture = await WithUnitOfWorkAsync(async () =>
            {
                Guid officeId;
                using (currentTenant.Change(null))
                {
                    var office = await tenantManager.CreateAsync(OfficeName);
                    office.SetDefaultConnectionString(
                        RealAuthorizationTestDatabase.OfficeConnectionString);
                    await tenantRepository.InsertAsync(office, autoSave: true);
                    officeId = office.Id;
                }

                var seededOffice = await seeder.SeedAsync(officeId, "authz");

                // The four production external roles WITH their real grants. Using the
                // production seeder rather than hand-written grants is what makes the
                // Patient-vs-Defense-Attorney contrast on the SSN surface meaningful: the
                // difference under test is the one production actually ships.
                await externalRoleSeeder.SeedAsync(new DataSeedContext(officeId));

                using (currentTenant.Change(officeId))
                {
                    await EnsureRoleAsync(roleManager, MinimalRoleName, officeId);
                    await permissionManager.SetAsync(
                        MinimalRoleGrant, RoleProviderName, MinimalRoleName, isGranted: true);

                    await EnsureRoleAsync(roleManager, PushRoleName, officeId);
                    await permissionManager.SetAsync(
                        PushRoleGrant, RoleProviderName, PushRoleName, isGranted: true);
                }

                return new AuthorizationFixture(seededOffice);
            }, requiresNew: true);

            _fixture = fixture;
            return fixture;
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private static async Task EnsureRoleAsync(
        IdentityRoleManager roleManager, string roleName, Guid officeId)
    {
        if (await roleManager.FindByNameAsync(roleName) != null)
        {
            return;
        }

        await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, officeId));
    }

    // requiresNew starts a fresh, independent unit of work -- needed when crossing into a
    // different tenant's context so the office connection re-resolves.
    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action, bool requiresNew = false)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew);
        await action();
        await uow.CompleteAsync();
    }

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(
        Func<Task<TResult>> func, bool requiresNew = false)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew);
        var result = await func();
        await uow.CompleteAsync();
        return result;
    }
}
