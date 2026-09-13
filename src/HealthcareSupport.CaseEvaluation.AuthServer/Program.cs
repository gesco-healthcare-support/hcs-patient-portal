using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthcareSupport.CaseEvaluation.Logging;
using Serilog;
using Serilog.Events;

namespace HealthcareSupport.CaseEvaluation;

public static class Program
{
    // #775: the file sink ran on Serilog.Sinks.File DEFAULTS, which cap at
    // 1 GB and do NOT roll -- so logging STOPS at the ceiling, silently. The
    // bounds, the measurement behind them and what this does NOT fix all live
    // on LogFileConsts, so the two hosts cannot drift apart.
    public async static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.File(
                "Logs/logs.txt",
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: LogFileConsts.SizeLimitBytes,
                retainedFileCountLimit: LogFileConsts.RetainedFileCount))
            .WriteTo.Async(c => c.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting HealthcareSupport.CaseEvaluation.AuthServer.");
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
            builder.Host
                .AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
#if DEBUG
                        .MinimumLevel.Debug()
#else
                        .MinimumLevel.Information()
#endif
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Async(c => c.File(
                            "Logs/logs.txt",
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: LogFileConsts.SizeLimitBytes,
                            retainedFileCountLimit: LogFileConsts.RetainedFileCount))
                        .WriteTo.Async(c => c.Console())
                        .WriteTo.Async(c => c.AbpStudio(services));
                });
            await builder.AddApplicationAsync<CaseEvaluationAuthServerModule>();
            var app = builder.Build();
            await app.InitializeApplicationAsync();
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "HealthcareSupport.CaseEvaluation.AuthServer terminated unexpectedly!");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
