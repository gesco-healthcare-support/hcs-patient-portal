using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.AppointmentDocuments;

/// <summary>
/// BUG-036 (2026-05-23): the composite unique index on
/// (TenantId, AppointmentId, Kind) must NOT apply to soft-deleted rows.
/// Historically PacketAttachmentProvider.NotifySendCompletedAsync soft-
/// deleted the AttyCE row after a successful send, and an IsDeleted=1 row
/// blocked every subsequent Regenerate with SQL Server error 2601
/// (surfaced as AbpDbConcurrencyException via EF Core's batched-INSERT
/// mis-classification).
///
/// The AttyCE retention prune was removed on 2026-06-09 (all packet kinds
/// now persist), so this scenario no longer arises in practice -- but the
/// filtered index is kept as defensive design for any future soft-delete
/// of a packet row, and this test guards it.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentPacketIndexTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;

    public AppointmentPacketIndexTests()
    {
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
    }

    [Fact]
    public async Task UniqueIndex_OnTenantAppointmentKind_ExcludesSoftDeletedRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await _dbContextProvider.GetDbContextAsync();

            var entityType = dbContext.Model.FindEntityType(typeof(AppointmentPacket));
            entityType.ShouldNotBeNull();

            var uniqueIndex = entityType.GetIndexes()
                .FirstOrDefault(i => i.IsUnique
                                  && i.Properties.Count == 3
                                  && i.Properties[0].Name == "TenantId"
                                  && i.Properties[1].Name == "AppointmentId"
                                  && i.Properties[2].Name == "Kind");
            uniqueIndex.ShouldNotBeNull(
                "AppointmentPacket should declare a unique index on (TenantId, AppointmentId, Kind).");

            var filter = uniqueIndex.GetFilter();
            filter.ShouldNotBeNull(
                "BUG-036: unique index needs a filter that excludes soft-deleted rows; current null filter applies the constraint to IsDeleted=1 rows too.");
            filter.ShouldContain("IsDeleted",
                customMessage: "BUG-036: unique index filter must reference IsDeleted so soft-deleted AttyCE rows do not block subsequent Regenerate inserts.");
        });
    }
}
