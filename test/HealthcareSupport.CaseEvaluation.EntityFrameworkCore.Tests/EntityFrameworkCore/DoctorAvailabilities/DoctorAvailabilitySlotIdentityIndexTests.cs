using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.DoctorAvailabilities;

/// <summary>
/// Pins the slot-identity unique index in BOTH DbContexts.
///
/// <para>WHY A MODEL ASSERTION RATHER THAN A BEHAVIOURAL ONE. The index is a BACKSTOP behind
/// <c>DoctorAvailabilityManager</c>'s clash check, and it only earns its keep in the race the
/// guard cannot close -- two concurrent creates that both read, both find nothing, and both
/// insert. A single-threaded integration suite cannot stage that, so every behavioural test in
/// this repo would still pass with the index deleted. Without the assertions below, deleting the
/// index configuration would break NOTHING, which would make the backstop unprotected against
/// exactly the kind of accidental removal these tests exist to catch.</para>
///
/// <para>Both contexts are checked because <c>DoctorAvailability</c> is a DbSet in each
/// (host and per-office), so the index needs a migration in each set, and divergence between the
/// two contexts has bitten this codebase before.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class DoctorAvailabilitySlotIdentityIndexTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly string[] ExpectedColumns =
    {
        nameof(DoctorAvailability.TenantId),
        nameof(DoctorAvailability.LocationId),
        nameof(DoctorAvailability.AvailableDate),
        nameof(DoctorAvailability.FromTime),
        nameof(DoctorAvailability.ToTime),
    };

    [Fact]
    public async Task HostContext_CarriesTheSlotIdentityIndex()
    {
        await AssertSlotIdentityIndexAsync<CaseEvaluationDbContext>();
    }

    [Fact]
    public async Task TenantContext_CarriesTheSlotIdentityIndex()
    {
        await AssertSlotIdentityIndexAsync<CaseEvaluationTenantDbContext>();
    }

    private async Task AssertSlotIdentityIndexAsync<TDbContext>()
        where TDbContext : AbpDbContext<TDbContext>
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetRequiredService<IDbContextProvider<TDbContext>>().GetDbContextAsync();
            var entityType = dbContext.Model.FindEntityType(typeof(DoctorAvailability));

            entityType.ShouldNotBeNull(
                $"{typeof(TDbContext).Name} does not map DoctorAvailability at all, so the slot " +
                "uniqueness rule cannot exist in this database.");

            var index = entityType!.GetIndexes().SingleOrDefault(i =>
                i.Properties.Select(p => p.Name).SequenceEqual(ExpectedColumns));

            index.ShouldNotBeNull(
                "The slot-identity index is missing from " + typeof(TDbContext).Name + ". Expected " +
                "an index over (" + string.Join(", ", ExpectedColumns) + "). Without it the only " +
                "thing standing between two concurrent creates and a duplicate slot is the " +
                "manager's clash check, which cannot close that race on its own. Found: " +
                string.Join(" | ", entityType.GetIndexes().Select(i =>
                    "(" + string.Join(", ", i.Properties.Select(p => p.Name)) + ")")));

            index!.IsUnique.ShouldBeTrue(
                "The slot-identity index exists but is not UNIQUE, so it enforces nothing and is " +
                "merely a lookup aid.");

            var filter = index.GetFilter();
            filter.ShouldNotBeNullOrWhiteSpace(
                "The slot-identity index has no filter, so it will also count soft-deleted rows. " +
                "A deleted row still occupying the key makes every later insert on that slot fail " +
                "-- the exact defect Fix_UniqueIndexesExcludeSoftDeleted was raised to correct.");

            filter!.ShouldContain(
                "IsDeleted",
                Case.Sensitive,
                "The filter must exclude soft-deleted rows. Filter found: " + filter);
        });
    }
}
