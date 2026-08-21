using System;
using System.Collections.Generic;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the document-update wire body. Unlike intake, this body is a BARE JSON ARRAY --
/// contract section G -- so the shape itself is the thing under test: wrapping it in the standard
/// <c>{data,meta,errors}</c> envelope would make the receiver's deserializer fail on every push.
/// </summary>
public class DocumentUpdateSerializerTests
{
    private static readonly Guid DocumentId = new("f97796c9-365b-4ad3-a164-08f72981cae3");

    private static IntakeDocumentEntry SampleEntry() => new()
    {
        Id = DocumentId,
        Source = DocumentEntryMapper.DocumentSource,
        DocumentName = "Medical Records",
        FileName = "records.pdf",
        ContentType = "application/pdf",
        FileSize = 2048,
        Status = "Accepted",
        ObjectKey = "tenants/b8844bba-414c-e238-4a71-3a22841f21af/records",
        CreatedAtUtc = "2026-07-28T10:00:00.0000000Z",
        UpdatedAt = "2026-07-28T11:30:00.0000000Z",
        DocumentType = "Medical Records",
    };

    [Fact]
    public void SerializeDocumentEntries_EmitsATopLevelArray()
    {
        var json = IntakePayloadSerializer.SerializeDocumentEntries(new List<IntakeDocumentEntry> { SampleEntry() });

        json.TrimStart()[0].ShouldBe('[');
        using var parsed = JsonDocument.Parse(json);
        parsed.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        parsed.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public void SerializeDocumentEntries_UsesCamelCaseKeys()
    {
        var json = IntakePayloadSerializer.SerializeDocumentEntries(new List<IntakeDocumentEntry> { SampleEntry() });

        using var parsed = JsonDocument.Parse(json);
        var entry = parsed.RootElement[0];
        entry.GetProperty("objectKey").GetString().ShouldBe("tenants/b8844bba-414c-e238-4a71-3a22841f21af/records");
        entry.GetProperty("documentName").GetString().ShouldBe("Medical Records");
        entry.GetProperty("updatedAt").GetString().ShouldBe("2026-07-28T11:30:00.0000000Z");
        entry.TryGetProperty("ObjectKey", out _).ShouldBeFalse();
    }

    [Fact]
    public void SerializeDocumentEntries_WithNoEntries_EmitsAnEmptyArray()
    {
        // Never an empty object or null: the receiver treats the array as the authoritative set.
        var json = IntakePayloadSerializer.SerializeDocumentEntries(new List<IntakeDocumentEntry>());

        json.ShouldBe("[]");
    }

    [Fact]
    public void SerializeDeletionEntries_EmitsOnlyIdDeletedAndUpdatedAt()
    {
        var deletion = new DocumentDeletionEntry
        {
            Id = DocumentId,
            UpdatedAt = "2026-07-28T12:00:00.0000000Z",
        };

        var json = IntakePayloadSerializer.SerializeDeletionEntries(new List<DocumentDeletionEntry> { deletion });

        using var parsed = JsonDocument.Parse(json);
        parsed.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        var entry = parsed.RootElement[0];
        entry.GetProperty("id").GetGuid().ShouldBe(DocumentId);
        entry.GetProperty("deleted").GetBoolean().ShouldBeTrue();
        entry.GetProperty("updatedAt").GetString().ShouldBe("2026-07-28T12:00:00.0000000Z");

        // No stray nulls: a deletion carries no objectKey, so the receiver cannot be tricked into
        // re-fetching bytes for a document the portal has repudiated.
        entry.EnumerateObject().ShouldNotBeEmpty();
        foreach (var property in entry.EnumerateObject())
        {
            property.Name.ShouldBeOneOf("id", "deleted", "updatedAt");
        }
    }

    [Fact]
    public void DocumentDeletionEntry_DefaultsToDeleted()
    {
        // The type exists only to express a removal, so `deleted: false` would be meaningless.
        new DocumentDeletionEntry().Deleted.ShouldBeTrue();
    }
}
