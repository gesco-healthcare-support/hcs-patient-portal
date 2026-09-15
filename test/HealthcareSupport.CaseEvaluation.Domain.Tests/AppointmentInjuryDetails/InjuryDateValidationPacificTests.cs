using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

/// <summary>
/// #623 -- the "injury date cannot be in the future" check must measure today in
/// PACIFIC, not UTC.
///
/// <para><c>AbpClockOptions.Kind</c> is pinned to Utc, so <c>IClock.Now.Date</c> is
/// the UTC date. From 5pm Pacific (midnight UTC) onwards that is TOMORROW's Pacific
/// date, and this validation compares against it -- so for the last 7-8 hours of
/// every Pacific working day it ACCEPTED an injury dated a day in the future.</para>
///
/// <para>The window is the dangerous part: the check works correctly for 16 hours a
/// day, which is why neither the sweep nor the banned-symbol gate caught it. The
/// gate bans <c>DateTime.Today</c> and <c>DateTime.Now</c>; <c>_clock.Now.Date</c>
/// is the same bug wearing the approved API.</para>
///
/// <para>The repositories are substitutes that THROW if touched. That is deliberate:
/// the future-date check runs before any load, so a passing test also pins that
/// ordering. If someone moves the repository calls above the guard, these fail.</para>
/// </summary>
public class InjuryDateValidationPacificTests
{
    // 02:00 UTC on 16 June is 19:00 Pacific on 15 June (PDT, UTC-7).
    // So "today" is the 15th in Pacific and the 16th in UTC -- the exact window.
    private static readonly DateTime EveningPacificUtcInstant =
        new(2026, 6, 16, 2, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime PacificToday = new(2026, 6, 15);
    private static readonly DateTime PacificTomorrow = new(2026, 6, 16);

    private static TestableManager Build(DateTime utcNow)
    {
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(utcNow);

        return new TestableManager(
            Substitute.For<IAppointmentInjuryDetailRepository>(),
            ThrowingRepository<Appointment>(),
            ThrowingRepository<Patient>(),
            clock);
    }

    private static IRepository<T, Guid> ThrowingRepository<T>() where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
    {
        var repo = Substitute.For<IRepository<T, Guid>>();
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns<T>(_ => throw new InvalidOperationException(
                "the future-date guard must reject before any entity is loaded"));
        return repo;
    }

    [Fact]
    public async Task AnInjuryDatedPacificTomorrow_IsRejected_DuringTheEveningWindow()
    {
        // THE REGRESSION. With `_clock.Now.Date` this passed validation, because the
        // UTC date already reads 16 June and `16 > 16` is false.
        var manager = Build(EveningPacificUtcInstant);

        var ex = await Should.ThrowAsync<UserFriendlyException>(() =>
            manager.ValidateAsync(Guid.NewGuid(), PacificTomorrow, false, null));

        ex.Message.ShouldContain("future");
    }

    [Fact]
    public async Task AnInjuryDatedPacificToday_IsStillAccepted_DuringTheEveningWindow()
    {
        // The complement, and it is load-bearing: a fix that simply subtracted a day
        // would pass the test above while rejecting today's legitimate injuries.
        // Reaching the repository load means the date guard let it through -- which
        // the throwing substitute reports as InvalidOperationException, not
        // UserFriendlyException.
        var manager = Build(EveningPacificUtcInstant);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            manager.ValidateAsync(Guid.NewGuid(), PacificToday, false, null));

        ex.Message.ShouldContain("before any entity is loaded");
    }

    [Fact]
    public async Task OutsideTheWindow_TomorrowIsStillRejected()
    {
        // 18:00 UTC on 15 June is 11:00 Pacific the same day, so UTC and Pacific
        // agree. This is the 16 hours a day during which the bug was invisible.
        var manager = Build(new DateTime(2026, 6, 15, 18, 0, 0, DateTimeKind.Utc));

        await Should.ThrowAsync<UserFriendlyException>(() =>
            manager.ValidateAsync(Guid.NewGuid(), PacificTomorrow, false, null));
    }

    [Fact]
    public async Task ACumulativeRangeEndingPacificTomorrow_IsRejected()
    {
        // The same `today` feeds the cumulative-trauma "To" bound a few lines later,
        // so it carried the identical defect.
        var manager = Build(EveningPacificUtcInstant);

        await Should.ThrowAsync<UserFriendlyException>(() =>
            manager.ValidateAsync(Guid.NewGuid(), PacificTomorrow, true, PacificTomorrow));
    }

    /// <summary>Exposes the protected validation without widening production access.</summary>
    private sealed class TestableManager : AppointmentInjuryDetailManager
    {
        public TestableManager(
            IAppointmentInjuryDetailRepository injuryRepository,
            IRepository<Appointment, Guid> appointmentRepository,
            IRepository<Patient, Guid> patientRepository,
            IClock clock)
            : base(injuryRepository, appointmentRepository, patientRepository, clock)
        {
        }

        public Task ValidateAsync(
            Guid appointmentId, DateTime dateOfInjury, bool isCumulative, DateTime? toDate) =>
            ValidateInjuryDatesAsync(appointmentId, dateOfInjury, isCumulative, toDate);
    }
}
