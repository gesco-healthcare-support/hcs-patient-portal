using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthcareSupport.CaseEvaluation.Data;
using Serilog;
using Volo.Abp;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.DbMigrator;

/// <summary>
/// Runs the database migrations and the data seed once, inside a standalone ABP application,
/// then stops the host.
/// </summary>
public class DbMigratorHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public DbMigratorHostedService(
        IHostApplicationLifetime hostApplicationLifetime,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (var application = await AbpApplicationFactory.CreateAsync<CaseEvaluationDbMigratorModule>(options =>
        {
            options.Services.ReplaceConfiguration(_configuration);
            // ABP builds IAbpHostEnvironment from this option alone and reads a blank name as
            // Production, so without it DOTNET_ENVIRONMENT never reaches a seeder that asks ABP.
            options.Environment = _hostEnvironment.EnvironmentName;
            // The application above is a SEPARATE container from this host's, so nothing the host
            // registered is visible inside it -- including IHostEnvironment, which domain services
            // take (AdminPasswordRotator reads IsDevelopment(); the tenant migration handler reads
            // ApplicationName to know it is in the migrator). Without this they cannot be built and
            // the migrator stops at start-up. The host's own instance, so both containers agree.
            options.Services.AddSingleton<IHostEnvironment>(_hostEnvironment);
            options.UseAutofac();
            options.Services.AddLogging(c => c.AddSerilog());
            options.AddDataMigrationEnvironment();
        }))
        {
            await application.InitializeAsync();

            await application
                .ServiceProvider
                .GetRequiredService<CaseEvaluationDbMigrationService>()
                .MigrateAsync();

            await application.ShutdownAsync();

            _hostApplicationLifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
