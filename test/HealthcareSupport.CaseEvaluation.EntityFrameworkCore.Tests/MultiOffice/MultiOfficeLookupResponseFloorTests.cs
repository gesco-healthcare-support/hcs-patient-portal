using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// 2026-11-20 -- the two lookups pad every answer they must not tell apart (<see cref="LookupResponseFloor"/>):
/// the re-evaluation source match at its choke point (<see cref="ReEvaluationSourceAccess.FindAsync"/>, which the
/// lookup AND the booking submit use), and the number request. The harness swaps the floor for
/// <see cref="RecordingLookupResponseFloor"/>, which counts the waits.
///
/// <para>A partial of <see cref="MultiOfficeAtomicBookingSubmitTests"/> so it reuses that class's offices, sources
/// and caller helpers. Sources on days 53-55 (18:00), re-evaluation bookings on days 56, 57 and 61 (19:00), and
/// offsets 1200-1250 are this file's own.</para>
/// </summary>
public partial class MultiOfficeAtomicBookingSubmitTests
{
    private RecordingLookupResponseFloor Floor =>
        GetRequiredService<LookupResponseFloor>().ShouldBeOfType<RecordingLookupResponseFloor>();

    // Records every background-job enqueue for this class (job execution is off in this rig anyway). The number
    // request test uses it to see WHEN the dispatcher's on-completed outbox drain was enqueued.
    private readonly EnqueueRecorder _enqueued = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IBackgroundJobManager>(_enqueued));
    }

    private sealed class EnqueueRecorder : IBackgroundJobManager
    {
        private readonly List<object> _calls = new();

        public List<object> Snapshot()
        {
            lock (_calls)
            {
                return _calls.ToList();
            }
        }

        public Task<string> EnqueueAsync<TArgs>(
            TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
        {
            lock (_calls)
            {
                _calls.Add(args!);
            }

            return Task.FromResult(Guid.NewGuid().ToString());
        }
    }

    [Fact]
    public async Task ReEvaluationLookup_PadsAnUnknownNumberAndAWrongDateOfBirth_ButNotACaseTheCallerMayLoad()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 53, dayOffset: 1200);
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
    public async Task RevalSubmit_PadsAnUnknownNumberAndAWrongDateOfBirth_ButNotAPartysSubmit()
    {
        // The booking submit reaches the same match as the lookup, so it is padded at the same choke point.
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 55, dayOffset: 1220);
        var stranger = Guid.NewGuid();
        Floor.Reset();

        var unknown = await RevalSubmitInputAsync(
            office, bookingDay: 56, dayOffset: 1230, "NOPE-FLOOR-SUBMIT", source.DateOfBirth, source.PatientId);
        await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, stranger, "Applicant Attorney", () => _appointments.SubmitAsync(unknown)));
        Floor.Pads.ShouldBe(1, "a submit naming an unknown number waits out the floor");

        var wrongDate = await RevalSubmitInputAsync(
            office, bookingDay: 57, dayOffset: 1240, source.Number, source.DateOfBirth.AddYears(1), source.PatientId);
        await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, stranger, "Applicant Attorney", () => _appointments.SubmitAsync(wrongDate)));
        Floor.Pads.ShouldBe(2, "a submit with the wrong date of birth waits the same way");

        var byStaff = await RevalSubmitInputAsync(
            office, bookingDay: 61, dayOffset: 1250, source.Number, null, source.PatientId);
        AppointmentSubmitResultDto? booked = null;
        await InOfficeAsync(office, async () => booked = await _appointments.SubmitAsync(byStaff));
        booked.ShouldNotBeNull();
        Floor.Pads.ShouldBe(2, "a party's submit is not padded");
    }

    [Fact]
    public async Task NumberLookup_PadsAMatchAndAMiss_Alike_AndAMatchHasFinishedItsWorkBeforeTheWait()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 54, dayOffset: 1210);
        var details = await DetailsOfAsync(office, source.Id, source.PatientId);
        int DrainsForThisOffice() =>
            _enqueued.Snapshot().OfType<OutboxDrainArgs>().Count(a => a.TenantId == office.OfficeId);

        // Setting up the source case already sent mail, so count from here.
        var drainsBefore = DrainsForThisOffice();
        var drainEnqueuedBeforeTheWait = false;
        Floor.Reset();
        Floor.OnPad = () =>
        {
            // The dispatcher defers its outbox drain enqueue to its unit of work's completion. If the work ran in the
            // request's own unit of work, that enqueue (and the commit with it) would come AFTER the wait, on a match
            // only. Seeing a new one already enqueued proves the inner unit of work completed before the wait.
            drainEnqueuedBeforeTheWait = DrainsForThisOffice() > drainsBefore;
            return Task.CompletedTask;
        };

        await InOfficeAsWithLoginInARequestTransactionAsync(office, Guid.NewGuid(), "r3-floor-1@example.test", "Applicant Paralegal",
            () => Lookups.RequestAsync(details));
        (await LookupMailAsync(office, source.Id)).ShouldHaveSingleItem("the match really sent the number");
        Floor.Pads.ShouldBe(1, "a match waits out the floor");
        drainEnqueuedBeforeTheWait.ShouldBeTrue(
            "the match's work (commit + drain enqueue) finishes before the wait, so none of it is measurable after");

        Floor.OnPad = null;
        details.ClaimNumber = "NOT-THE-CLAIM";
        await InOfficeAsWithLoginInARequestTransactionAsync(office, Guid.NewGuid(), "r3-floor-2@example.test", "Applicant Paralegal",
            () => Lookups.RequestAsync(details));
        Floor.Pads.ShouldBe(2, "a miss waits the same way");
    }

    /// <summary>
    /// Like the HTTP request in production: one TRANSACTIONAL unit of work around the call, so nothing the call writes
    /// commits before the request ends unless the call commits it itself. (The other helpers use a non-transactional
    /// unit of work, where saved rows are visible at once, which could not tell the inner unit of work apart.)
    /// </summary>
    private async Task InOfficeAsWithLoginInARequestTransactionAsync(
        SeededOffice office, Guid userId, string login, string role, Func<Task> body)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
        using (_currentTenant.Change(office.OfficeId))
        using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, login, role))
        {
            await body();
        }

        await uow.CompleteAsync();
    }

    private async Task<AppointmentSubmitDto> RevalSubmitInputAsync(
        SeededOffice office, int bookingDay, int dayOffset, string sourceNumber, DateTime? dateOfBirth, Guid patientId)
    {
        var date = TestToday.AddDays(bookingDay);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(19, 0), new TimeOnly(20, 0)));
        var input = BuildSubmitDto(office, slotId, date.AddHours(19).AddMinutes(15), dayOffset: dayOffset);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = sourceNumber;
        input.SourcePatientDateOfBirth = dateOfBirth;
        input.Patient = null;
        input.PatientId = patientId;
        input.Accessors = new List<AppointmentAccessorCreateDto>();
        return input;
    }
}
