using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.DoctorAvailabilities;

/// <summary>
/// Measures how far the 2026-09-11 write normalisation (<c>AvailableDate = availableDate.Date</c>)
/// actually reaches, and what it leaves behind.
///
/// <para>WHY THIS IS AN EF TEST AND NOT AN APPSERVICE ONE. The obvious place to put these is
/// beside the existing date-only tests in <c>DoctorAvailabilitiesAppServiceTests</c>. That would
/// not work, and the reason is the whole point of the file:
/// <c>DoctorAvailabilityManager.EnsureNoOverlappingSlotAsync</c> compares on
/// <c>x.AvailableDate.Date == day</c> -- it normalises for comparison ITSELF, independently of the
/// constructor. So a test driven through <c>CreateAsync</c> gets the same answer whether or not
/// the constructor normalises: the guard masks the difference. Only storage can see it, so these
/// bypass the guard and assert on persisted rows.</para>
///
/// <para>WHAT THE NORMALISATION DOES NOT DO. It applies to WRITES ONLY (the constructor and
/// <c>UpdateAsync</c>); there is no backfill. Rows written before it keep whatever time component
/// they carried, which is exactly what the index configuration says at
/// <c>CaseEvaluationSharedModelConfiguration</c> ("rows written before that change keep any stray
/// time and this index cannot speak for them"). That sentence is a real limitation rather than a
/// caveat, and until now nothing tested it -- deleting it would have broken nothing.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class DoctorAvailabilityDateNormalizationReachTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly ICurrentTenant _currentTenant;

    // 2033 is unused by every other fixture in this suite. The collection shares ONE seeded SQLite
    // database and rows accumulate across test classes, so a date another class also seeds would
    // make these tests fail for a reason that has nothing to do with what they assert. That is not
    // hypothetical: it is precisely how the slot-identity index produced 17 unrelated failures.
    private static readonly DateTime CalendarDay = new(2033, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    public DoctorAvailabilityDateNormalizationReachTests()
    {
        _slotRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// THE COLLAPSE. Two slots whose AvailableDate differed only by time of day were two distinct
    /// rows before the normalisation and are one calendar day after it. This is the merge that
    /// hypothesis (b) was worried about, stated directly rather than inferred.
    /// </summary>
    [Fact]
    public void Normalisation_MergesTwoDatesThatDifferedOnlyByTimeOfDay()
    {
        var morning = new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: LocationsTestData.Location1Id,
            availableDate: CalendarDay.AddHours(8),
            fromTime: new TimeOnly(9, 0),
            toTime: new TimeOnly(10, 0),
            bookingStatusId: BookingStatus.Available);

        var evening = new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: LocationsTestData.Location1Id,
            availableDate: CalendarDay.AddHours(17).AddMinutes(45),
            fromTime: new TimeOnly(9, 0),
            toTime: new TimeOnly(10, 0),
            bookingStatusId: BookingStatus.Available);

        morning.AvailableDate.ShouldBe(
            evening.AvailableDate,
            "Two AvailableDate values on the same calendar day must normalise to the same stored " +
            "value. If they do not, the write normalisation is gone, and the slot-identity index " +
            "no longer means what the clash rule means: rows a second apart would be distinct to " +
            "the index and a clash to the product.");

        morning.AvailableDate.TimeOfDay.ShouldBe(
            TimeSpan.Zero,
            "AvailableDate must store the calendar date alone. The slot's real times are " +
            "FromTime/ToTime, so a time component here is redundant rather than meaningful.");
    }

    /// <summary>
    /// THE LIMIT, and the part that matters for production. A row that already carries a stray time
    /// -- which is what any pre-2026-09-11 row can look like -- is NOT the same slot as far as the
    /// index is concerned, so the two coexist. The uniqueness rule simply does not reach legacy data.
    ///
    /// <para>This is the test that makes the index's documented caveat falsifiable. It also sets the
    /// production question: the repo cannot say how many such rows exist, because the only writer
    /// that could have produced them is the AppService DTO coming from the UI/API. Only the data
    /// can answer that.</para>
    /// </summary>
    [Fact]
    public async Task ARowCarryingAStrayTime_IsNotReachedByTheSlotIdentityIndex()
    {
        var legacyId = Guid.NewGuid();
        var normalisedId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                // A pre-normalisation row, reproduced faithfully: built through the constructor and
                // then stamped with a time component directly, because the constructor is the very
                // thing that would strip it. This is what legacy data looks like, not a contrivance.
                var legacy = new DoctorAvailability(
                    id: legacyId,
                    locationId: LocationsTestData.Location1Id,
                    availableDate: CalendarDay,
                    fromTime: new TimeOnly(11, 0),
                    toTime: new TimeOnly(12, 0),
                    bookingStatusId: BookingStatus.Available);
                legacy.AvailableDate = CalendarDay.AddHours(13).AddMinutes(20);
                await _slotRepository.InsertAsync(legacy, autoSave: true);

                // Same tenant, same location, same calendar day, same times. Under the product's
                // rule this is the SAME slot as the legacy row above.
                var normalised = new DoctorAvailability(
                    id: normalisedId,
                    locationId: LocationsTestData.Location1Id,
                    availableDate: CalendarDay,
                    fromTime: new TimeOnly(11, 0),
                    toTime: new TimeOnly(12, 0),
                    bookingStatusId: BookingStatus.Available);

                // Asserted rather than just executed, so that a collision fails by NAME instead of
                // as a bare "SQLite Error 19: UNIQUE constraint failed". If the index ever starts
                // treating these two as one slot, the reader needs to be told which guarantee
                // broke, not handed a constraint violation to diagnose.
                await Should.NotThrowAsync(
                    () => _slotRepository.InsertAsync(normalised, autoSave: true),
                    "A legacy row carrying a stray time and a normalised row for the same calendar " +
                    "day, location and times must COEXIST: the unique index sits on the RAW " +
                    "AvailableDate column, so they are different keys even though the product " +
                    "considers them the same slot. A violation here means the index has begun " +
                    "reaching legacy rows -- a backfill landed, or the column changed shape -- and " +
                    "the pre-deploy duplicate check must be re-run before this ships.");
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var queryable = await _slotRepository.GetQueryableAsync();
                var sameDay = queryable
                    .Where(x => x.LocationId == LocationsTestData.Location1Id
                                && x.FromTime == new TimeOnly(11, 0)
                                && x.ToTime == new TimeOnly(12, 0))
                    .ToList()
                    .Where(x => x.AvailableDate.Date == CalendarDay.Date)
                    .ToList();

                sameDay.Count.ShouldBe(
                    2,
                    "Both rows must persist. The unique index is on the RAW AvailableDate column, so " +
                    "a legacy row carrying 13:20 and a normalised row at midnight are different keys " +
                    "even though the product considers them the same slot. If this ever returns 1, " +
                    "the index has started reaching legacy rows -- which means either a data backfill " +
                    "landed or the column changed shape, and the pre-deploy duplicate check must be " +
                    "re-run before trusting it.");

                sameDay.Single(x => x.Id == legacyId).AvailableDate.TimeOfDay
                    .ShouldBe(
                        TimeSpan.FromHours(13).Add(TimeSpan.FromMinutes(20)),
                        "The legacy row must keep its stray time. The normalisation is write-only " +
                        "and does not backfill; if this row were silently rewritten, existing " +
                        "production data would be mutating underneath a schema migration.");
            }
        });
    }
}
