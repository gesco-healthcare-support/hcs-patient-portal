using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Data;

public class CaseEvaluationDbMigrationService : ITransientDependency
{
    public ILogger<CaseEvaluationDbMigrationService> Logger { get; set; }

    private readonly IDataSeeder _dataSeeder;
    private readonly IEnumerable<ICaseEvaluationDbSchemaMigrator> _dbSchemaMigrators;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly AdminSeedPasswordResolver _adminSeedPasswordResolver;
    private readonly AdminPasswordRotator _adminPasswordRotator;

    public CaseEvaluationDbMigrationService(
        IDataSeeder dataSeeder,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        AdminSeedPasswordResolver adminSeedPasswordResolver,
        AdminPasswordRotator adminPasswordRotator,
        IEnumerable<ICaseEvaluationDbSchemaMigrator> dbSchemaMigrators)
    {
        _dataSeeder = dataSeeder;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _adminSeedPasswordResolver = adminSeedPasswordResolver;
        _adminPasswordRotator = adminPasswordRotator;
        _dbSchemaMigrators = dbSchemaMigrators;

        Logger = NullLogger<CaseEvaluationDbMigrationService>.Instance;
    }

    public async Task MigrateAsync()
    {
        var initialMigrationAdded = AddInitialMigrationIfNotExist();

        if (initialMigrationAdded)
        {
            return;
        }

        Logger.LogInformation("Started database migrations...");

        await MigrateDatabaseSchemaAsync();
        await SeedDataAsync();

        Logger.LogInformation($"Successfully completed host database migrations.");

        if (MultiTenancyConsts.IsEnabled)
        {

            var tenants = await _tenantRepository.GetListAsync(includeDetails: true);

            var migratedDatabaseSchemas = new HashSet<string>();
            foreach (var tenant in tenants)
            {
                using (_currentTenant.Change(tenant.Id))
                {
                    if (tenant.ConnectionStrings.Count > 0)
                    {
                        var tenantConnectionStrings = tenant.ConnectionStrings
                            .Select(x => x.Value)
                            .ToList();

                        if (!migratedDatabaseSchemas.IsSupersetOf(tenantConnectionStrings))
                        {
                            await MigrateDatabaseSchemaAsync(tenant);

                            migratedDatabaseSchemas.AddIfNotContains(tenantConnectionStrings);
                        }
                    }

                    await SeedDataAsync(tenant);
                }

                Logger.LogInformation("Successfully completed {TenantName} tenant database migrations.", tenant.Name);
            }

            Logger.LogInformation("Successfully completed all database migrations.");
        }
        Logger.LogInformation("You can safely end this process...");
    }

    private async Task MigrateDatabaseSchemaAsync(Tenant? tenant = null)
    {
        var scope = tenant == null ? "host" : tenant.Name + " tenant";
        Logger.LogInformation("Migrating schema for {Scope} database...", scope);

        foreach (var migrator in _dbSchemaMigrators)
        {
            await migrator.MigrateAsync();
        }
    }

    private async Task SeedDataAsync(Tenant? tenant = null)
    {
        var scope = tenant == null ? "host" : tenant.Name + " tenant";
        Logger.LogInformation("Executing {Scope} database seed...", scope);

        // Per-office admin email comes from the office seed config (Falkinstein + the
        // other offices); the host pass + any unconfigured tenant fall back to the default.
        var adminEmail = Saas.OfficeSeedData.FindByTenantName(tenant?.Name)?.AdminEmail
            ?? CaseEvaluationConsts.AdminEmailDefaultValue;

        // B12: the password comes from the configured store, which generates one per database on
        // first use and returns the stored value ever after -- but only when this pass is about to
        // CREATE the admin. Where the admin already exists the seeder ignores the password, and
        // writing a store entry anyway would leave a file that does not match the account
        // (AdminSeedPasswordResolver says which accounts that was). In Development the store is the
        // published-default one, so a local clone still seeds the documented credentials and
        // nothing about local work changes. There is deliberately no isDevelopment branch HERE --
        // the branch lives in the store selection, so adding a fifth seeding site cannot forget it.
        var adminPassword = await _adminSeedPasswordResolver.ResolveAsync(tenant?.Id);

        await _dataSeeder.SeedAsync(new DataSeedContext(tenant?.Id)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, adminEmail)
            .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, adminPassword)
        );

        // B12 decision D1. Seeding only helps a database created after this change: ABP's seeder
        // creates the admin ONLY when no admin exists, so every database that already exists keeps
        // whatever it was first seeded with -- which, until now, was a password published in the
        // framework source and in this public repository. This is the step that moves those onto a
        // generated one, and it is why no manual pass over the existing server is needed.
        await _adminPasswordRotator.RotateIfOnAKnownDefaultAsync(tenant?.Id, scope);
    }

    private bool AddInitialMigrationIfNotExist()
    {
        try
        {
            if (!DbMigrationsProjectExists())
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            if (!MigrationsFolderExists())
            {
                AddInitialMigration();
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Couldn't determine if any migrations exist.");
            return false;
        }
    }

    private static bool DbMigrationsProjectExists()
    {
        var dbMigrationsProjectFolder = GetEntityFrameworkCoreProjectFolderPath();

        return dbMigrationsProjectFolder != null;
    }

    private static bool MigrationsFolderExists()
    {
        var dbMigrationsProjectFolder = GetEntityFrameworkCoreProjectFolderPath();

        return dbMigrationsProjectFolder != null && Directory.Exists(Path.Combine(dbMigrationsProjectFolder, "Migrations"));
    }

    private void AddInitialMigration()
    {
        Logger.LogInformation("Creating initial migration...");

        string argumentPrefix;
        string fileName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            argumentPrefix = "-c";
            fileName = "/bin/bash";
        }
        else
        {
            argumentPrefix = "/C";
            fileName = "cmd.exe";
        }

        var procStartInfo = new ProcessStartInfo(fileName,
            $"{argumentPrefix} \"abp create-migration-and-run-migrator \"{GetEntityFrameworkCoreProjectFolderPath()}\"\""
        );

        try
        {
            Process.Start(procStartInfo);
        }
        catch (Exception)
        {
            throw new Exception("Couldn't run ABP CLI...");
        }
    }

    private static string? GetEntityFrameworkCoreProjectFolderPath()
    {
        var slnDirectoryPath = GetSolutionDirectoryPath();

        if (slnDirectoryPath == null)
        {
            throw new Exception("Solution folder not found!");
        }

        var srcDirectoryPath = Path.Combine(slnDirectoryPath, "src");

        return Directory.GetDirectories(srcDirectoryPath)
            .FirstOrDefault(d => d.EndsWith(".EntityFrameworkCore"));
    }

    private static string? GetSolutionDirectoryPath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory != null && Directory.GetParent(currentDirectory.FullName) != null)
        {
            currentDirectory = Directory.GetParent(currentDirectory.FullName);

            if (currentDirectory != null && Directory.GetFiles(currentDirectory.FullName).FirstOrDefault(f => f.EndsWith(".sln") || f.EndsWith(".slnx")) != null)
            {
                return currentDirectory.FullName;
            }
        }

        return null;
    }
}
