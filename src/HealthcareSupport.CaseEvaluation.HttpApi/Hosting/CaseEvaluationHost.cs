using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// The shared entry point for both web hosts.
///
/// <para><b>Why this exists (#775 / #871).</b> <c>AuthServer/Program.cs</c> and
/// <c>HttpApi.Host/Program.cs</c> were 64 lines each and differed in four: a BOM, the
/// module type, and two log strings. SonarCloud reported the duplicated block as
/// essentially the whole <c>Main</c> body, so ANY change to either file landed inside it
/// and read as 100% duplicated new code -- #775 measured 34% against a 3% threshold with
/// zero issues and zero uncovered lines. The duplication was not a symptom of that change;
/// it was the pre-existing shape of the two files.</para>
///
/// <para><b>What was tried first and did not work.</b> <c>sonar.cpd.exclusions</c> for the
/// two paths. The argument reached the scanner intact and both files still reported
/// <c>new_duplicated_lines = 16</c> with per-line data still flagging them; the density
/// only moved 34.0 -> 27.6, which is a recalculation rather than an exclusion. That was
/// reverted rather than shipped, because a config change whose comment confidently
/// explains a thing it does not do is worse than the red mark it was meant to remove.</para>
///
/// <para><b>Why here and not Domain.Shared.</b> The design called for Domain.Shared, where
/// <see cref="LogFileConsts"/> already lives. Measured, that home is expensive: this method
/// needs ASP.NET Core, Serilog, Autofac and the ABP Studio client, and Domain.Shared is a
/// plain class library reached by Domain, Application.Contracts, Application,
/// EntityFrameworkCore, HttpApi, HttpApi.Client, DbMigrator, TestBase and Domain.Tests --
/// so all of them would inherit a web stack, DbMigrator being a console application. The
/// HttpApi project is referenced by EXACTLY the two hosts and nothing else, so putting it
/// here adds those packages to the two projects that already carry every one of them and
/// to nothing else. The constants stay in Domain.Shared; only the call shape moved.</para>
/// </summary>
public static class CaseEvaluationHost
{
    /// <summary>
    /// Relative to the content root, as before. Left a literal here rather than added to
    /// LogFileConsts: that type is documented as the rolling-file BOUNDS, the path was never
    /// part of its contract, and this extraction is meant to move the call shape and nothing
    /// else.
    /// </summary>
    private const string LogFilePath = "Logs/logs.txt";

    /// <summary>
    /// Boots a host: bootstrap logger, Serilog wiring, ABP application, run.
    /// </summary>
    /// <typeparam name="TModule">The ABP module for this process.</typeparam>
    /// <param name="appName">
    /// Used only in the two log lines that name the process. Passed explicitly rather than
    /// derived from <typeparamref name="TModule"/> so the text stays greppable -- the
    /// AuthServer's fatal line reads "...AuthServer terminated unexpectedly!" and that is
    /// what someone searches the logs for.
    /// </param>
    /// <param name="args">The process arguments, forwarded to the web host builder.</param>
    /// <returns>0 on clean shutdown, 1 if the host died.</returns>
    public static async Task<int> RunAsync<TModule>(string appName, string[] args)
        where TModule : IAbpModule
    {
        // #775: the file sink ran on Serilog.Sinks.File DEFAULTS, which cap at 1 GB and do
        // NOT roll -- so logging STOPS at the ceiling, silently. The bounds, the
        // measurement behind them and what this does NOT fix all live on LogFileConsts.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: LogFileConsts.SizeLimitBytes,
                retainedFileCountLimit: LogFileConsts.RetainedFileCount))
            .WriteTo.Async(c => c.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting {AppName}.", appName);
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
                            LogFilePath,
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: LogFileConsts.SizeLimitBytes,
                            retainedFileCountLimit: LogFileConsts.RetainedFileCount))
                        .WriteTo.Async(c => c.Console())
                        .WriteTo.Async(c => c.AbpStudio(services));
                });
            await builder.AddApplicationAsync<TModule>();
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

            Log.Fatal(ex, "{AppName} terminated unexpectedly!", appName);
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
