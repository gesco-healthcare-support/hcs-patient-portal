using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 -- the admin-password store as the DbMigrator and the API build it outside Development:
/// ABP's conventional registration of this assembly, then <see cref="AdminPasswordStoreRegistrar"/>
/// with a folder configured, resolved from a real Autofac container.
///
/// <para><b>Why this exists.</b> The 2026-09-30 release stopped at start-up because the migrator could
/// not build <see cref="IAdminPasswordStore"/>: nothing registered <see cref="IAdminPasswordStoreLock"/>.
/// <see cref="SqlAppLock"/> was marked <c>ITransientDependency</c>, but ABP exposes a conventionally
/// registered class only as itself and its default interfaces -- those whose name, less the leading
/// I, ends the class name. <c>SqlAppLock</c> does not end in <c>AdminPasswordStoreLock</c>, so the
/// interface was never registered. Every other test reaches the store another way: the rigs register
/// the Development store directly (<c>CaseEvaluationTestBaseModule</c>) and
/// <see cref="LockedAdminPasswordStoreTests"/> builds the decorator by hand. None went through the
/// registrar, so the gap was invisible until a production host started.</para>
///
/// <para><b>The container is built but the modules are NOT initialized.</b> Resolution is the
/// guarantee, and initializing would reach for a database this test has no use for. Nothing here
/// opens a connection: <see cref="SqlAppLock"/> only reads its connection string until a lock is
/// taken, so the one configured below names a host that cannot exist.</para>
///
/// <para><b>What this does not prove.</b> It does not load either host module, so it does not show
/// that they call the registrar -- they do, and the image-level check on the fix PR started the real
/// migrator to cover that. The registrar is called here exactly as the hosts call it.</para>
/// </summary>
public sealed class AdminPasswordStoreRegistrationTests : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "admin-password-registration-" + Guid.NewGuid().ToString("N"));

    private IAbpApplicationWithInternalServiceProvider _app = null!;
    private IServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminPasswordStoreSelector.DirectoryKey] = _directory,
                ["ConnectionStrings:Default"] = "Server=admin-password-registration.invalid;Database=CaseEvaluation",
            })
            .Build();

        _app = await AbpApplicationFactory.CreateAsync<ProductionShapedStoreModule>(options =>
        {
            options.Services.ReplaceConfiguration(configuration);
            options.Environment = "Production";
            options.UseAutofac();
        });

        _services = _app.CreateServiceProvider();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        Directory.Delete(_directory, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The call that failed in production: the migration service takes the store in its constructor.
    /// </summary>
    [Fact]
    public void OutsideDevelopment_WithAFolder_TheStoreResolvesAsTheLockedStore()
    {
        var store = Should.NotThrow(
            () => _services.GetRequiredService<IAdminPasswordStore>(),
            "the DbMigrator and the API both resolve this at start-up outside Development. A "
            + "resolution error here is a host that cannot start.");

        store.ShouldBeOfType<LockedAdminPasswordStore>();
    }

    /// <summary>
    /// Pins the lock on its own, so a failure says which half is missing rather than only that the
    /// store could not be built.
    /// </summary>
    [Fact]
    public void OutsideDevelopment_WithAFolder_TheLockResolvesAsTheSqlApplicationLock()
    {
        var storeLock = Should.NotThrow(
            () => _services.GetRequiredService<IAdminPasswordStoreLock>(),
            "the locked store cannot be built without a lock, and conventional registration does "
            + "not expose SqlAppLock under this interface.");

        storeLock.ShouldBeOfType<SqlAppLock>();
    }

    /// <summary>
    /// The EF Core module and whatever it pulls in, plus the registrar called as the host modules
    /// call it. Deliberately not the test rigs' modules: those register the Development store.
    /// </summary>
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(CaseEvaluationEntityFrameworkCoreModule))]
    public sealed class ProductionShapedStoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            AdminPasswordStoreRegistrar.Register(
                context.Services, context.Services.GetConfiguration(), isDevelopment: false);
        }
    }
}
