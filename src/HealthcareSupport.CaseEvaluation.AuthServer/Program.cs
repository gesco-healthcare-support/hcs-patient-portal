using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace HealthcareSupport.CaseEvaluation;

public static class Program
{
    // #775: the file sink ran on Serilog.Sinks.File DEFAULTS -- rollingInterval
    // Infinite, fileSizeLimitBytes 1 GB, rollOnFileSizeLimit false. That
    // combination does not fill the disk; it STOPS LOGGING at the ceiling, with
    // no error and nothing else affected. Either default alone is survivable. A
    // cap without rolling is the one that ends quietly.
    //
    // MEASURED ON THE BOX, 2026-09-11, rather than estimated. The API container
    // started 2026-08-31 18:51:28 and its log was 101,686,218 bytes 11.16 days
    // later -- about 9.1 MB/day at MinimumLevel.Information with ASP.NET Core
    // request logging on. That is roughly 106 days to the 1 GB ceiling.
    //
    // 50 MB per file is therefore ~5.5 days of normal traffic: an ordinary day
    // never rolls on size, and a burst rolls instead of ending. 31 files bounds
    // the directory at 1.55 GB worst case and about 280 MB in practice, against
    // 28 GB free on the box. Both numbers are one edit away if that balance
    // changes; the arithmetic is here so the next person does not have to redo
    // the measurement to move them.
    private const long LogFileSizeLimitBytes = 50L * 1024 * 1024;
    private const int LogRetainedFileCount = 31;
    public async static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.File(
                "Logs/logs.txt",
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: LogFileSizeLimitBytes,
                retainedFileCountLimit: LogRetainedFileCount))
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
                            fileSizeLimitBytes: LogFileSizeLimitBytes,
                            retainedFileCountLimit: LogRetainedFileCount))
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
