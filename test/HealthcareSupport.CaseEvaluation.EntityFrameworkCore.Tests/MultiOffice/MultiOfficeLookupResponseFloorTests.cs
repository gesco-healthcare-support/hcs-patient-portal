using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// 2026-11-20 -- the two lookups pad every answer they must not tell apart (<see cref="LookupResponseFloor"/>).
/// The harness swaps the floor for <see cref="RecordingLookupResponseFloor"/>, which counts the waits.
///
/// <para>A partial of <see cref="MultiOfficeAtomicBookingSubmitTests"/> so it reuses that class's offices, sources
/// and caller helpers. Days 67-68 and offsets 1200-1210 are this file's own.</para>
/// </summary>
public partial class MultiOfficeAtomicBookingSubmitTests
{
    private RecordingLookupResponseFloor Floor =>
        GetRequiredService<LookupResponseFloor>().ShouldBeOfType<RecordingLookupResponseFloor>();

    [Fact]
    public async Task ReEvaluationLookup_PadsAnUnknownNumberAndAWrongDateOfBirth_ButNotACaseTheCallerMayLoad()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 67, dayOffset: 1200);
        var stranger = Guid.NewGuid();
        Floor.Reset();

        await InOfficeAsAsync(office, stranger, "Applicant Attorney", async () =>
            (await _reEvaluationSources.LookupAsync(Lookup("NOPE-FLOOR", source.DateOfBirth))).ShouldBeNull());
        Floor.Pads.ShouldBe(1, "an unknown number waits out the floor");

        await InOfficeAsAsync(office, stranger, "Applicant Attorney", async () =>
            (await _reEvaluationSources.LookupAsync(Lookup(source.Number, source.DateOfBirth.AddDays(1)))).ShouldBeNull());
        Floor.Pads.ShouldBe(2, "a known number with the wrong date of birth waits the same way");

        await InOfficeAsAsync(office, stranger, "Applicant Attorney", async () =>
            (await _reEvaluationSources.LookupAsync(Lookup(source.Number, source.DateOfBirth))).ShouldNotBeNull());
        Floor.Pads.ShouldBe(2, "a case the caller may load is not padded: it reveals the match anyway");
    }

    [Fact]
    public async Task ReEvaluationLookup_PadsTheThrottleRefusal_ThePathThatThrows()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var stranger = Guid.NewGuid();
        var guess = Lookup("NOPE-FLOOR-LIMIT", new DateTime(1980, 1, 1));
        for (var attempt = 0; attempt < LookupThrottleBudget.ReEvaluationSource.MaxPerUserPerHour; attempt++)
        {
            await InOfficeAsAsync(office, stranger, "Applicant Attorney", async () =>
                (await _reEvaluationSources.LookupAsync(guess)).ShouldBeNull());
        }

        Floor.Reset();
        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, stranger, "Applicant Attorney", () => _reEvaluationSources.LookupAsync(guess)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ReEvaluationLookupThrottled);
        Floor.Pads.ShouldBe(1, "an answer that throws waits out the floor too");
    }

    [Fact]
    public async Task NumberLookup_PadsAMatchAndAMiss_Alike()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 68, dayOffset: 1210);
        var details = await DetailsOfAsync(office, source.Id, source.PatientId);
        Floor.Reset();

        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-floor-1@example.test", "Applicant Paralegal",
            () => Lookups.RequestAsync(details));
        (await LookupMailAsync(office, source.Id)).ShouldHaveSingleItem("the match really sent the number");
        Floor.Pads.ShouldBe(1, "a match waits out the floor");

        details.ClaimNumber = "NOT-THE-CLAIM";
        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), "r3-floor-2@example.test", "Applicant Paralegal",
            () => Lookups.RequestAsync(details));
        Floor.Pads.ShouldBe(2, "a miss waits the same way");
    }
}
