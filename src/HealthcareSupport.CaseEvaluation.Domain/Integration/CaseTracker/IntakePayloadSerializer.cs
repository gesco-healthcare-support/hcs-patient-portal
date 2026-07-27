using System.Text.Json;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Single place the integration JSON is produced, so the push and the Part 4 reconcile endpoint
/// cannot drift into two different wire formats.
///
/// <para>Nulls are deliberately NOT omitted: the contract documents nullable fields
/// (<c>panelNumber</c>, <c>previousAppointmentId</c>, a packet's <c>fileSize</c>) and sending them
/// explicitly is clearer for the receiver than making them absent. Their DTOs ignore unknown
/// properties, so additive fields are safe.</para>
/// </summary>
public static class IntakePayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Serialize(IntakeEnvelope envelope)
    {
        return JsonSerializer.Serialize(envelope, Options);
    }
}
