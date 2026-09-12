using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Settings;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using HealthcareSupport.CaseEvaluation.Timing;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Shared NSubstitute fixtures for the Group L reminder-job tests. Builds the
/// collaborators each date-driven job needs: an in-memory appointment
/// repository, a tenant work runner that invokes the per-office delegate once,
/// and a setting provider stubbed with the <c>RemindersEnabled</c> gate plus one
/// anchor list. The <c>GetAsync&lt;bool&gt;</c> extension the jobs call resolves
/// through <c>GetOrNullAsync</c>, so stubbing the raw string is enough.
/// </summary>
internal static class ReminderJobTestHarness
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// The instant every reminder-job test runs at: 2026-08-31 02:30 UTC, which is 2026-08-30
    /// 19:30 Pacific. Deliberately an EVENING Pacific instant, so the UTC date (the 31st) and the
    /// Pacific date (the 30th) DIFFER.
    ///
    /// <para>That choice is what makes these tests worth having. Every one of these jobs used to
    /// compute its "today" as <c>DateTime.UtcNow.Date</c>, which was correct only because the crons
    /// fire at 07:00-08:15 Pacific, when the two dates agree. Anchoring the tests here means the
    /// day-anchor arithmetic is measured across the boundary: a job that reverts to the UTC date
    /// gets every elapsed-day count off by one and the tests fail.</para>
    ///
    /// <para>It also removes an existing flakiness. The tests previously dated their appointments
    /// from the real <c>DateTime.UtcNow.Date</c>, so they silently depended on when they ran.</para>
    /// </summary>
    public static readonly DateTime NowUtc = new DateTime(2026, 8, 31, 2, 30, 0, DateTimeKind.Utc);

    /// <summary>Pacific calendar date at <see cref="NowUtc"/> -- 2026-08-30, not the 31st.</summary>
    public static DateTime PacificToday => PacificTime.TodayFrom(NowUtc);

    /// <summary>A clock pinned to <see cref="NowUtc"/>, kinded Utc as the real one now is.</summary>
    public static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        // Configure Now explicitly: NSubstitute auto-mocks unconfigured interface members, so an
        // unset DateTime property returns default(DateTime) rather than failing loudly.
        clock.Now.Returns(NowUtc);
        clock.Kind.Returns(DateTimeKind.Utc);
        return clock;
    }

    public static IRepository<Appointment, Guid> AppointmentRepo(params Appointment[] appointments)
    {
        var repo = Substitute.For<IRepository<Appointment, Guid>>();
        repo.GetQueryableAsync().Returns(_ => appointments.AsQueryable());
        return repo;
    }

    public static IRepository<AppointmentDocument, Guid> DocumentRepo(params AppointmentDocument[] documents)
    {
        var repo = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        repo.GetQueryableAsync().Returns(_ => documents.AsQueryable());
        return repo;
    }

    /// <summary>
    /// A tenant work runner that invokes the per-office delegate once, for the
    /// single synthetic office the in-memory appointment repo is scoped to. The
    /// fake repo ignores tenant filtering, so one pass exercises the per-office body.
    /// </summary>
    public static ITenantWorkRunner TenantWorkRunner()
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(call => call.Arg<Func<Guid, Task>>().Invoke(TenantId));
        return runner;
    }

    public static ISettingProvider Settings(bool enabled, string anchorName, string anchorValue)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled)
            .Returns(enabled ? "true" : "false");
        settings.GetOrNullAsync(anchorName).Returns(anchorValue);
        return settings;
    }

    /// <summary>Synthetic appointment with a non-null tenant so the per-tenant loop runs.</summary>
    public static Appointment Appt(Guid id, AppointmentStatusType status, DateTime appointmentDate)
    {
        return new Appointment(
            id: id,
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: appointmentDate,
            requestConfirmationNumber: "TEST-" + id.ToString("N")[..6],
            appointmentStatus: status)
        {
            TenantId = TenantId,
        };
    }
}
