using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public abstract class CaseEvaluationDbContextFactoryBase<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : DbContext
{

    protected string ConnectionStringName { get; }

    public CaseEvaluationDbContextFactoryBase(string connectionStringName = "Default")
    {
        ConnectionStringName = connectionStringName;
    }

    public TDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        CaseEvaluationEfCoreEntityExtensionMappings.Configure();

        // Resolve the configured connection string. If the named connection
        // is missing (e.g. dev environment never configured "TenantDevelopmentTime"),
        // fall back to "Default" so design-time EF tools can still scaffold
        // tenant-side migrations against the dev DB. The fallback is gated by
        // a name check so the host context never silently shadow-falls onto
        // itself; only explicitly-named overrides may downgrade.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrEmpty(connectionString) && ConnectionStringName != "Default")
        {
            connectionString = configuration.GetConnectionString("Default");
        }

        var builder = new DbContextOptionsBuilder<TDbContext>()
            .UseSqlServer(connectionString);

        return CreateDbContext(builder.Options);
    }

    protected abstract TDbContext CreateDbContext(DbContextOptions<TDbContext> dbContextOptions);

    protected static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../HealthcareSupport.CaseEvaluation.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
