using System.Collections.Generic;
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

    /// <summary>
    /// Serializes ONLY the <c>data</c> section, for use as the outbox dedup version (2026-08-13).
    ///
    /// <para>Deliberately excludes <c>meta</c>: it carries a per-build <c>RequestId</c>, so hashing
    /// the whole envelope would make every enqueue unique and defeat de-duplication entirely --
    /// the opposite failure to the one this exists to fix.</para>
    /// </summary>
    public static string SerializeDataForVersioning(IntakeEnvelope envelope)
    {
        return JsonSerializer.Serialize(envelope.Data, Options);
    }

    /// <summary>
    /// Serializes a document-update body: a BARE JSON ARRAY, not the <c>{data,meta,errors}</c>
    /// envelope intake uses (contract section G). The asymmetry is the receiver's, not ours -- their
    /// document endpoint binds a list directly -- so it is enforced here rather than left to each
    /// caller to remember.
    /// </summary>
    public static string SerializeDocumentEntries(IReadOnlyList<IntakeDocumentEntry> entries)
    {
        return JsonSerializer.Serialize(entries, Options);
    }

    /// <summary>Serializes removals into the same bare array. See <see cref="DocumentDeletionEntry"/>.</summary>
    public static string SerializeDeletionEntries(IReadOnlyList<DocumentDeletionEntry> entries)
    {
        return JsonSerializer.Serialize(entries, Options);
    }
}
