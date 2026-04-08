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

        var builder = new DbContextOptionsBuilder<TDbContext>()
            .UseSqlServer(configuration.GetConnectionString(ConnectionStringName));

        return CreateDbContext(builder.Options);
    }

    protected abstract TDbContext CreateDbContext(DbContextOptions<TDbContext> dbContextOptions);

    protected IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../HealthcareSupport.CaseEvaluation.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
