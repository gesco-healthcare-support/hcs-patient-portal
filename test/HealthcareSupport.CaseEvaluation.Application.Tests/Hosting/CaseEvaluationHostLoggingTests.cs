using System.Collections.Generic;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// The hosts' log levels drop ASP.NET Core's per-request "Request starting" and "Request finished"
/// lines. The first of those carries <c>{QueryString}</c>, and query strings here carry patient
/// search fields, the booking lookup's email and the one-time tokens in emailed links.
/// <c>UseSerilogRequestLogging</c> replaces them with one line that logs the path only.
/// </summary>
public class CaseEvaluationHostLoggingTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static CapturingSink LogOneInformationEvent(string sourceContext)
    {
        var sink = new CapturingSink();
        using var logger = CaseEvaluationHost.ConfigureLevels(new LoggerConfiguration())
            .WriteTo.Sink(sink)
            .CreateLogger();
        logger.ForContext(Constants.SourceContextPropertyName, sourceContext)
            .Information("Request starting {Protocol} {Method} {Path}{QueryString}", "HTTP/1.1", "GET", "/api/app/patients", "?lastName=TEST");
        return sink;
    }

    [Fact]
    public void HostingDiagnostics_InformationIsDropped()
    {
        LogOneInformationEvent("Microsoft.AspNetCore.Hosting.Diagnostics").Events.ShouldBeEmpty();
    }

    [Fact]
    public void OtherMicrosoftCategories_InformationIsStillWritten()
    {
        // The control: without it, an empty sink above could mean nothing was ever logged.
        LogOneInformationEvent("Microsoft.AspNetCore.Routing").Events.Count.ShouldBe(1);
    }

    [Fact]
    public void HostingDiagnostics_WarningIsStillWritten()
    {
        var sink = new CapturingSink();
        using var logger = CaseEvaluationHost.ConfigureLevels(new LoggerConfiguration())
            .WriteTo.Sink(sink)
            .CreateLogger();
        logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Hosting.Diagnostics")
            .Warning("Hosting warning");

        sink.Events.Count.ShouldBe(1);
    }
}
