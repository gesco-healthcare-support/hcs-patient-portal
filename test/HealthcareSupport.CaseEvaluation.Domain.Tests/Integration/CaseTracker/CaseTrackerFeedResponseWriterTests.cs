using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed's wire shape (#927): the Gesco envelope, and each row's body as a string that decodes to EXACTLY
/// the stored push body -- the property the consumer's unchanged upsert path depends on.
/// </summary>
public class CaseTrackerFeedResponseWriterTests
{
    private static readonly Guid RequestId = new("c3d4e5f6-a7b8-49ca-8bdc-ed2143658709");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    // Quotes, a backslash, markup characters and a non-ASCII letter (written as an escape to keep the source
    // ASCII): everything an escaper could alter.
    private const string Payload = "{\"data\":{\"note\":\"Sample <b>&</b> \\\\ caf\u00e9\"},\"meta\":{}}";

    [Fact]
    public void WritePage_CarriesEachBody_AsAStringThatDecodesToTheStoredBytes()
    {
        var result = CaseTrackerFeedResult.Served(
            new[] { new CaseTrackerFeedRow(2003, IntegrationMessageType.Intake, AppointmentId, Payload) },
            nextPosition: 2003,
            hasMore: false);

        using var json = JsonDocument.Parse(CaseTrackerFeedResponseWriter.WritePage(result, RequestId, Now));

        var row = json.RootElement.GetProperty("data").GetProperty("rows")[0];
        row.GetProperty("body").ValueKind.ShouldBe(JsonValueKind.String);
        row.GetProperty("body").GetString().ShouldBe(Payload);
    }

    [Fact]
    public void WritePage_WritesTheEnvelope_WithTheCursorTypeAndAppointmentPerRow()
    {
        var result = CaseTrackerFeedResult.Served(
            new[]
            {
                new CaseTrackerFeedRow(2003, IntegrationMessageType.Intake, AppointmentId, "{}"),
                new CaseTrackerFeedRow(2004, IntegrationMessageType.DocumentUpdate, AppointmentId, "[]"),
            },
            nextPosition: 2004,
            hasMore: true);

        using var json = JsonDocument.Parse(CaseTrackerFeedResponseWriter.WritePage(result, RequestId, Now));
        var root = json.RootElement;

        var data = root.GetProperty("data");
        data.GetProperty("nextCursor").GetString().ShouldBe("00000000000007D4");
        data.GetProperty("hasMore").GetBoolean().ShouldBeTrue();
        var rows = data.GetProperty("rows");
        rows[0].GetProperty("cursor").GetString().ShouldBe("00000000000007D3");
        rows[0].GetProperty("messageType").GetString().ShouldBe("intake");
        rows[0].GetProperty("appointmentId").GetString().ShouldBe(AppointmentId.ToString("D"));
        rows[1].GetProperty("messageType").GetString().ShouldBe("documentUpdate");
        root.GetProperty("meta").GetProperty("requestId").GetString().ShouldBe(RequestId.ToString("D"));
        root.GetProperty("meta").GetProperty("timestamp").GetString()!.ShouldEndWith("Z");
        root.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void WriteError_HasNoData_AndOneError()
    {
        using var json = JsonDocument.Parse(
            CaseTrackerFeedResponseWriter.WriteError("cursor_ahead", "Beyond anything issued.", "cursor", RequestId, Now));
        var root = json.RootElement;

        root.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
        var error = root.GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("cursor_ahead");
        error.GetProperty("message").GetString().ShouldBe("Beyond anything issued.");
        error.GetProperty("field").GetString().ShouldBe("cursor");
        root.GetProperty("errors").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public void WireName_RefusesAnUnknownMessageType()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CaseTrackerFeedResponseWriter.WireName((IntegrationMessageType)99));
    }
}
