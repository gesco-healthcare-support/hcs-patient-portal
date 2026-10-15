using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.AdminPasswords;

/// <summary>
/// B12 -- the admin-password rotation run the way the DbMigrator runs it: in a container with NO
/// ASP.NET Core identity module, so no Identity token provider is registered.
///
/// <para><b>Why this exists.</b> The 2026-09-30 release deploy stopped in the migrator with "No
/// IUserTwoFactorTokenProvider named 'Default' is registered": the rotator moved the admin onto
/// the stored password through a reset token, and only web hosts register the provider that
/// issues one. <c>AdminPasswordRotatorTests</c> did not see it because the Application test
/// harness registers a no-op "Default" provider (<c>CaseEvaluationApplicationTestModule</c>) for
/// the registration tests. This class boots its own container without that harness.</para>
///
/// <para><b>What is real and what is not.</b> Real: the EF Core module and everything it brings,
/// the registrar with a folder configured (so the store is the real folder store), the
/// IdentityUserManager and the rotator, a copy of the seeded test database. Substituted, each for a
/// stated reason: the database is SQLite rather than SQL Server, so the SQL application lock is
/// replaced by an in-process one (the real lock is proven against SQL Server in
/// <see cref="SqlAppLockTests"/>); and IHostEnvironment is a Production stub, standing in for the
/// host instance DbMigratorHostedService passes in.</para>
/// </summary>
public sealed class AdminPasswordRotationInMigratorContainerTests : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "admin-password-rotation-" + Guid.NewGuid().ToString("N"));

    private SqliteConnection _connection = null!;
    private IAbpApplicationWithInternalServiceProvider _app = null!;
    private IServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _connection = TestDatabaseTemplate.CreateCopy();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                [AdminPasswordStoreSelector.DirectoryKey] = _directory,
                ["ConnectionStrings:Default"] = "Server=admin-password-rotation.invalid;Database=CaseEvaluation",
            })
            .Build();

        _app = await AbpApplicationFactory.CreateAsync<MigratorShapedModule>(options =>
        {
            options.Services.ReplaceConfiguration(configuration);
            options.Services.AddSingleton(_connection);
            options.Environment = Environments.Production;
            options.UseAutofac();
        });

        _services = _app.CreateServiceProvider();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        _connection.Dispose();
        Directory.Delete(_directory, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The deploy's failure, and the whole point of the rotator: an admin still on the published
    /// password ends the pass on the stored one, must change it at next sign-in, and the published
    /// password no longer works.
    /// </summary>
    [Fact]
    public async Task InTheMigratorContainer_AnAdminOnThePublishedDefault_IsMovedOntoTheStoredPassword()
    {
        await GivenTheHostAdminIsOnThePublishedDefaultAsync();

        var rotated = false;
        await Should.NotThrowAsync(
            async () => rotated = await _services.GetRequiredService<AdminPasswordRotator>()
                .RotateIfOnAKnownDefaultAsync(null, "host"),
            "the DbMigrator registers no Identity token provider, so rotation must not need one. "
            + "A failure here is a migrator that stops mid-deploy.");

        rotated.ShouldBeTrue();

        var stored = await new FileAdminPasswordStore(_directory).GetOrCreateAsync(null);
        await InAUnitOfWorkAsync(async userManager =>
        {
            var admin = (await userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName))!;
            (await userManager.CheckPasswordAsync(admin, stored)).ShouldBeTrue("the admin must be on the stored password");
            (await userManager.CheckPasswordAsync(admin, CaseEvaluationConsts.AdminPasswordDefaultValue))
                .ShouldBeFalse("the published password must no longer work");
            admin.ShouldChangePasswordOnNextLogin.ShouldBeTrue();
        });
    }

    private Task GivenTheHostAdminIsOnThePublishedDefaultAsync()
    {
        return InAUnitOfWorkAsync(async userManager =>
        {
            var admin = await userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
            if (admin == null)
            {
                admin = new IdentityUser(
                    Guid.NewGuid(), CaseEvaluationConsts.AdminUserName, "a1b2c3d4@e5f6a7b8.com");
                (await userManager.CreateAsync(admin, CaseEvaluationConsts.AdminPasswordDefaultValue))
                    .Succeeded.ShouldBeTrue();
                return;
            }

            // The seeded template's admin is created on the published default. Asserted rather than
            // assumed: if the seed ever changes, this says so here instead of the rotator silently
            // doing nothing and the test failing on a misleading line.
            (await userManager.CheckPasswordAsync(admin, CaseEvaluationConsts.AdminPasswordDefaultValue))
                .ShouldBeTrue("precondition: the seeded host admin is on the published default");
        });
    }

    private async Task InAUnitOfWorkAsync(Func<IdentityUserManager, Task> action)
    {
        using var scope = _services.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        await action(scope.ServiceProvider.GetRequiredService<IdentityUserManager>());
        await uow.CompleteAsync();
    }

    /// <summary>
    /// The EF Core module plus what the DbMigrator adds for the admin-password path -- and
    /// deliberately NOT the test harness modules, which register a no-op token provider.
    /// </summary>
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(CaseEvaluationEntityFrameworkCoreModule),
        typeof(AbpEntityFrameworkCoreSqliteModule))]
    public sealed class MigratorShapedModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            AdminPasswordStoreRegistrar.Register(
                context.Services, context.Services.GetConfiguration(), isDevelopment: false);
            context.Services.AddTransient<IAdminPasswordStoreLock, InProcessLock>();
            context.Services.AddSingleton<IHostEnvironment>(new ProductionHostEnvironment());

            var connection = context.Services.GetSingletonInstance<SqliteConnection>();
            Configure<AbpDbContextOptions>(options =>
            {
                options.Configure(dbContext => dbContext.DbContextOptions.UseSqlite(connection));
            });
        }
    }

    private sealed class InProcessLock : IAdminPasswordStoreLock
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public async Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken cancellationToken = default)
        {
            await Gate.WaitAsync(cancellationToken);
            return new Release();
        }

        private sealed class Release : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                Gate.Release();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class ProductionHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "HealthcareSupport.CaseEvaluation.DbMigrator";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
