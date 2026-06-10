using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Settings;
using NSubstitute;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Shared NSubstitute fixtures for the Group L reminder-job tests. Builds the
/// collaborators each date-driven job needs: an in-memory appointment
/// repository, no-op data filter + current tenant, and a setting provider
/// stubbed with the <c>RemindersEnabled</c> gate plus one anchor list. The
/// <c>GetAsync&lt;bool&gt;</c> extension the jobs call resolves through
/// <c>GetOrNullAsync</c>, so stubbing the raw string is enough.
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

    public static IDataFilter NoopDataFilter()
    {
        var filter = Substitute.For<IDataFilter>();
        filter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
        return filter;
    }

    public static ICurrentTenant NoopCurrentTenant()
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());
        return currentTenant;
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
