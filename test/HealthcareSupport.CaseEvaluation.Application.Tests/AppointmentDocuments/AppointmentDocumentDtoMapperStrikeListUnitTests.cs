using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// AF5 (2026-06-04) -- regression test for the Mapperly auto-map of the new
/// <see cref="AppointmentDocumentDto.IsPanelStrikeList"/> flag. Riok.Mapperly
/// maps source properties to target properties by name when both exist;
/// without this test a future refactor that drops the field from the DTO would
/// silently regress the staff document-list strike-list badge. Proves the flag
/// is server-queryable on the read path. Plan:
/// docs/plans/2026-06-04-panel-strike-list-flag.md.
/// </summary>
public class AppointmentDocumentDtoMapperStrikeListUnitTests
{
    private static AppointmentDocument NewDocument() => new(
        id: Guid.NewGuid(),
        tenantId: Guid.NewGuid(),
        appointmentId: Guid.NewGuid(),
        documentName: "synthetic strike list",
        fileName: "strike-list.pdf",
        blobName: "blob-synthetic",
        contentType: "application/pdf",
        fileSize: 1024,
        uploadedByUserId: Guid.NewGuid());

    [Fact]
    public void Map_DocumentFlaggedAsStrikeList_FlowsToDto()
    {
        var document = NewDocument();
        document.IsPanelStrikeList = true;

        var dto = new AppointmentDocumentToDtoMapper().Map(document);

        dto.IsPanelStrikeList.ShouldBeTrue();
    }

    [Fact]
    public void Map_DocumentNotFlaggedAsStrikeList_DefaultsFalseOnDto()
    {
        var dto = new AppointmentDocumentToDtoMapper().Map(NewDocument());

        dto.IsPanelStrikeList.ShouldBeFalse();
    }
}
