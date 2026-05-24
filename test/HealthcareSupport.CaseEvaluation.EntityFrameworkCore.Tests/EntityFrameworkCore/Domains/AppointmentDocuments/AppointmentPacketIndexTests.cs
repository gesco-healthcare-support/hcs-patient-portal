using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.AppointmentDocuments;

/// <summary>
/// BUG-036 (2026-05-23): the composite unique index on
/// (TenantId, AppointmentId, Kind) must NOT apply to soft-deleted rows,
/// otherwise PacketAttachmentProvider.NotifySendCompletedAsync's
/// retention prune (which soft-deletes the AttyCE row after a successful
/// send) leaves an IsDeleted=1 row that blocks every subsequent Regenerate
/// with SQL Server error 2601 (surfaced as AbpDbConcurrencyException via
/// EF Core's batched-INSERT mis-classification).
///
/// Adding the IsDeleted filter to the unique index is the primary fix
/// in BUG-036's 3-layer plan.
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
