using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// The deterministic reproduction of the defect that <c>CaseEvaluationMultiOfficeTestBase.TestToday</c>
/// exists to prevent: two slot-seeding sites whose day offsets differ by one alias onto ONE slot
/// key when they compute their day base on different calendar days.
///
/// <para><b>WHY THIS FILE EXISTS AS A TEST RATHER THAN A COMMENT.</b> The original failure is not
/// reproducible on demand -- it needs the run's wall clock to cross midnight between two specific
/// inserts. This reproduces the half that IS deterministic, which is the half that makes the
/// aliasing possible at all, and it does so in seconds rather than in the 17-30 minute suite.</para>
///
/// <para><b>OBSERVED IN CI, not theorised.</b> On 2026-09-19T00:00:04Z -- four seconds past UTC
/// midnight -- <c>MultiOfficeAppointmentsAppServiceTests.CreateAsync_WhenSlotTypesEmpty_AnyTypeWorks</c>
/// (+20, 09:00-10:00) failed with <c>SQLite Error 19: UNIQUE constraint failed</c> against the row
/// left by <c>MultiOfficeAtomicBookingSubmitTests.SubmitAsync_WithEveryChildGroup_PersistsAllOfThem</c>
/// (+21, 09:00-10:00). Both resolved to 2026-10-09. The same commit passed the same suite twice in
/// jobs that did not cross midnight.</para>
///
/// <para><b>WHAT THIS TEST DOES NOT PROVE, stated so nobody reads it as more.</b> It reproduces the
/// KEY ALIASING, not the SCHEDULING. Which test lands on which side of midnight is a property of
/// the run's start time, and no test can force that. The scheduling half rests on ONE CI
/// observation (run 35407439111, EF Core suite started ~23:58:34Z) plus four control runs that did
/// not straddle midnight during the MultiOffice block and all passed. This test is also NOT a guard
/// against the fix being reverted: it passes whether or not the call sites use <c>TestToday</c>,
/// because it supplies its own day bases. Reintroducing <c>DateTime.Today</c> at a call site is
/// caught by review, not by this file.</para>
/// </summary>
/// <remarks>
/// The <c>[Collection]</c> attribute is REQUIRED. Every MultiOffice class carries it because they
/// share the process-wide static offices and one SQLite database with no rollback; without it xUnit
/// runs this in parallel with them and the shared state races.
/// </remarks>
[Collection(MultiOfficeCollection.Name)]
public class SlotIdentityDayBaseTests : CaseEvaluationMultiOfficeTestBase
{
    // Offsets +300/+301 are reserved for this test and are used by nothing else: the largest offset
    // anywhere else in the MultiOffice tree is +47. Rows here persist for the rest of the run
    // (the rig never rolls back), so they must not land on another test's key.
    private const int ReservedDayBaseOffset = 300;

    private readonly IRepository<DoctorAvailability, Guid> _slots;
    private readonly ICurrentTenant _currentTenant;

    public SlotIdentityDayBaseTests()
    {
        _slots = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// Transcribes the two real call sites and drives them with day bases one apart, which is what
    /// a run crossing midnight produces. Asserts BOTH halves: that the dates alias, and that the
    /// five-column unique index refuses the second row. Asserting only the second would pass for
    /// any duplicate key and would not show that ADJACENT OFFSETS are what produced it.
    ///
    /// <para><b>SEEN TO FAIL 2026-09-21.</b> The database-refusal half was poisoned by moving the
    /// second insert five days off the first, so the two rows no longer share a key. The Fact then
    /// failed BY NAME, on the guarantee rather than on a downstream symptom:</para>
    /// <code>
    /// Failed:  1, Passed:  0, Total:  1
    /// SlotIdentityDayBaseTests.AdjacentOffsets_ComputedOnDayBasesOneApart_
    ///   AliasOntoOneSlotKey_AndTheIndexRefusesTheSecond [FAIL]
    ///   should throw Microsoft.EntityFrameworkCore.DbUpdateException but did not
    /// </code>
    /// <para>So the refusal assertion is discriminating, not decorative: it is satisfied only when
    /// a duplicate key is actually presented. The arithmetic half above it was not poisoned
    /// separately -- it is a pure equality on two computed dates and cannot pass vacuously.</para>
    /// </summary>
    [Fact]
    public async Task AdjacentOffsets_ComputedOnDayBasesOneApart_AliasOntoOneSlotKey_AndTheIndexRefusesTheSecond()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        // The two sites, transcribed. The offsets and times are theirs, not invented:
        //   partner  MultiOfficeAtomicBookingSubmitTests.cs:104,109  +21, 09:00-10:00, officeA
        //   victim   MultiOfficeAppointmentsAppServiceTests.cs:424,426  +20, 09:00-10:00, officeA
        var dayBaseBeforeMidnight = TestToday.AddDays(ReservedDayBaseOffset);
        var dayBaseAfterMidnight = dayBaseBeforeMidnight.AddDays(1);

        var partnerDate = dayBaseBeforeMidnight.AddDays(21);
        var victimDate = dayBaseAfterMidnight.AddDays(20);

        victimDate.ShouldBe(
            partnerDate,
            "offset N computed on the day AFTER offset N+1 resolves to the SAME calendar date. "
            + "This is the whole mechanism: the hand-allocated offsets are distinct only while "
            + "every site computes its day base on one calendar day. If this assertion ever fails, "
            + "the arithmetic premise behind CaseEvaluationMultiOfficeTestBase.TestToday is gone "
            + "and that field's justification must be rewritten, not just its comment.");

        await InsertSlotAsync(officeA, partnerDate);

        var ex = await Should.ThrowAsync<DbUpdateException>(
            async () => await InsertSlotAsync(officeA, victimDate),
            "the second insert shares (TenantId, LocationId, AvailableDate, FromTime, ToTime) with "
            + "the first, and that tuple is uniquely indexed, so the database must refuse it. If "
            + "nothing is thrown here the index is gone, and with it the only thing stopping two "
            + "MultiOffice tests from silently overwriting each other's slot.");

        // Contains(..., StringComparison) rather than ShouldContain: on a string, Shouldly's
        // ShouldContain binds to the IEnumerable<char> overload and the message argument is read
        // as a predicate, which does not compile.
        var chain = Flatten(ex);
        chain.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "the refusal must be the slot-identity index specifically. Asserting only the exception "
            + "TYPE would be satisfied by any save failure -- a missing FK, a null column -- and "
            + "would pass with the unique index dropped. Chain was: " + chain);
        chain.Contains("AppDoctorAvailabilities", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "and it must be the doctor-availability table, not some other unique index tripped on "
            + "the way in. Chain was: " + chain);
    }

    /// <summary>
    /// Every message in the exception chain. The constraint name arrives on the SQLite inner
    /// exception, not on the DbUpdateException, so asserting against the outer message alone
    /// silently matches nothing.
    /// </summary>
    private static string Flatten(Exception ex)
    {
        var text = string.Empty;
        for (var current = ex; current != null; current = current.InnerException)
        {
            text += current.Message + Environment.NewLine;
        }

        return text;
    }

    private Task InsertSlotAsync(SeededOffice office, DateTime date) =>
        WithUnitOfWorkAsync(
            async () =>
            {
                using (_currentTenant.Change(office.OfficeId))
                {
                    var slot = new DoctorAvailability(
                        id: Guid.NewGuid(),
                        locationId: office.LocationId,
                        availableDate: date,
                        fromTime: new TimeOnly(9, 0),
                        toTime: new TimeOnly(10, 0),
                        bookingStatusId: BookingStatus.Available,
                        capacity: 3)
                    {
                        TenantId = office.OfficeId,
                    };
                    await _slots.InsertAsync(slot, autoSave: true);
                }
            },
            requiresNew: true);
}
