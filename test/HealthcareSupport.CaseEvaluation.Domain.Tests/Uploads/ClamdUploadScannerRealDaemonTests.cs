using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// OPT-IN: the real <see cref="ClamdUploadScanner"/> against a REAL clamd (B11). Not FakeClamd and
/// not a python client, so it proves OUR client reads a genuine daemon's verdicts correctly.
/// </summary>
/// <remarks>
/// <para>
/// HOW TO RUN. Needs Docker. Unset, the tests show as SKIPPED (never as passed), so CI is unaffected.
/// <code>
/// RUN_CLAMD_INTEGRATION=1 dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests --filter ClamdUploadScannerRealDaemonTests
/// </code>
/// (PowerShell: <c>$env:RUN_CLAMD_INTEGRATION='1'</c> first, and clear it afterwards.) This is a
/// <c>dotnet test</c>, so do NOT set DOTNET_ENVIRONMENT.
/// </para>
/// <para>
/// IMAGE. The exact pin in docker-compose.prod.yml, tag and digest, kept in <see cref="Image"/>. If
/// the compose pin is bumped, bump it here. A cold start downloads signatures from the internet
/// (outbound HTTPS to database.clamav.net) and takes about one to two minutes; the test waits up to
/// five. The first run also pulls the image.
/// </para>
/// <para>
/// The EICAR string is assembled at run time from fragments, never written whole in source: Windows
/// Defender quarantines any file holding it.
/// </para>
/// </remarks>
public class ClamdUploadScannerRealDaemonTests : IAsyncLifetime
{
    private const string Image =
        "clamav/clamav:1.5@sha256:ebec5bc138401b36ae987caa1a3fa3c3b2a21ed3d51f0bfa5852825e663e67b0";

    private string? _container;
    private int _port;

    public async Task InitializeAsync()
    {
        if (!ClamdRealDaemonFactAttribute.Enabled)
        {
            return;
        }

        var name = "clamd-real-it-" + Guid.NewGuid().ToString("N")[..8];
        await Docker($"run -d --name {name} -p 127.0.0.1::3310 {Image}");
        _container = name;

        var mapping = (await Docker($"port {name} 3310/tcp")).Split('\n')[0].Trim();
        _port = int.Parse(mapping[(mapping.LastIndexOf(':') + 1)..]);

        // Ready means a clean scan returns a verdict through OUR client: signatures are loaded.
        var scanner = ScannerFor(_port);
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (true)
        {
            try
            {
                var probe = await scanner.ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes("ready probe")));
                if (probe.IsClean)
                {
                    return;
                }
            }
            catch (UploadScanUnavailableException) when (DateTime.UtcNow < deadline)
            {
                // clamd not listening yet; signatures are still loading.
            }

            DateTime.UtcNow.ShouldBeLessThan(deadline, "clamd never became ready within five minutes.");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await Docker($"rm -f {_container}");
        }
    }

    [ClamdRealDaemonFact]
    public async Task A_real_clamd_reports_the_EICAR_test_string_as_infected()
    {
        var eicar = string.Concat(
            "X5O!P%@AP[4" + (char)92 + "PZX54(P^)7CC)7}$", "EICAR-STANDARD-ANTIVIRUS-TEST-FILE", "!$H+H*");

        var result = await ScannerFor(_port).ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes(eicar)));

        result.IsClean.ShouldBeFalse();
        result.Signature.ShouldNotBeNull();
        result.Signature.ShouldContain("Eicar");
    }

    [ClamdRealDaemonFact]
    public async Task A_real_clamd_reports_an_ordinary_payload_as_clean()
    {
        var result = await ScannerFor(_port).ScanAsync(
            new MemoryStream(Encoding.ASCII.GetBytes("An ordinary medical report, nothing to see.")));

        result.IsClean.ShouldBeTrue();
        result.Signature.ShouldBeNull();
    }

    private static ClamdUploadScanner ScannerFor(int port) =>
        new(Options.Create(new ClamdOptions { Host = "127.0.0.1", Port = port, TimeoutSeconds = 30 }));

    private static async Task<string> Docker(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("docker", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, $"docker {arguments} failed: {await stderr}");
        return await stdout;
    }
}

/// <summary>A [Fact] that is reported SKIPPED, not passed, unless RUN_CLAMD_INTEGRATION is set.</summary>
public sealed class ClamdRealDaemonFactAttribute : FactAttribute
{
    public const string EnvironmentFlag = "RUN_CLAMD_INTEGRATION";

    public static bool Enabled =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentFlag));

    public ClamdRealDaemonFactAttribute()
    {
        if (!Enabled)
        {
            Skip = $"Opt-in: set {EnvironmentFlag}=1 (needs Docker) to scan with a real clamd.";
        }
    }
}
