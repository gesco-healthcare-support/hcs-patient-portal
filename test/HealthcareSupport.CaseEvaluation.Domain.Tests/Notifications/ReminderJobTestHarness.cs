using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Settings;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

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
