using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Writes feed responses in the Gesco envelope (#927, decided 2026-09-24):
/// <c>{ data: { rows, nextCursor, hasMore }, meta: { requestId, timestamp }, errors: [] }</c>, and for a refusal
/// <c>data: null</c> with one <c>{ code, message, field }</c> error.
///
/// <para>Written field by field with <see cref="Utf8JsonWriter"/> rather than serialized from an object, for the
/// reason <see cref="IntakePayloadSerializer"/> gives: the contract must not change because someone altered the
/// global JSON options.</para>
///
/// <para>Each row's <c>body</c> is a JSON STRING holding the stored payload -- byte for byte what the push posts
/// -- so the consumer hands it to the deserializer it already runs on a push body (decided 2026-09-24).</para>
/// </summary>
public static class CaseTrackerFeedResponseWriter
{
    /// <summary>A served page.</summary>
    public static string WritePage(CaseTrackerFeedResult result, Guid requestId, DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Write(requestId, timestampUtc, writer =>
        {
            writer.WriteStartObject("data");
            writer.WriteStartArray("rows");
            foreach (var row in result.Rows)
            {
                WriteRow(writer, row);
            }

            writer.WriteEndArray();
            writer.WriteString("nextCursor", result.NextCursor);
            writer.WriteBoolean("hasMore", result.HasMore);
            writer.WriteEndObject();
        }, error: null);
    }

    /// <summary>A refusal: no data, one error. <paramref name="field"/> names the offending parameter, if any.</summary>
    public static string WriteError(string code, string message, string? field, Guid requestId, DateTime timestampUtc) =>
        Write(requestId, timestampUtc, writer => writer.WriteNull("data"), (code, message, field));

    /// <summary>The wire name of a message type: <c>intake</c> or <c>documentUpdate</c>.</summary>
    public static string WireName(IntegrationMessageType messageType) => messageType switch
    {
        IntegrationMessageType.Intake => "intake",
        IntegrationMessageType.DocumentUpdate => "documentUpdate",
        _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unknown Case Tracker message type."),
    };

    private static void WriteRow(Utf8JsonWriter writer, CaseTrackerFeedRow row)
    {
        writer.WriteStartObject();
        writer.WriteString("cursor", CaseTrackerFeedCursor.Encode(row.Position));
        writer.WriteString("messageType", WireName(row.MessageType));
        writer.WriteString("appointmentId", row.AppointmentId.ToString("D"));
        writer.WriteString("body", row.Payload);
        writer.WriteEndObject();
    }

    private static string Write(
        Guid requestId,
        DateTime timestampUtc,
        Action<Utf8JsonWriter> writeData,
        (string Code, string Message, string? Field)? error)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writeData(writer);

            writer.WriteStartObject("meta");
            writer.WriteString("requestId", requestId.ToString("D"));
            writer.WriteString("timestamp", IntegrationTimestamp.ToIsoUtc(timestampUtc));
            writer.WriteEndObject();

            writer.WriteStartArray("errors");
            if (error.HasValue)
            {
                writer.WriteStartObject();
                writer.WriteString("code", error.Value.Code);
                writer.WriteString("message", error.Value.Message);
                writer.WriteString("field", error.Value.Field);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
