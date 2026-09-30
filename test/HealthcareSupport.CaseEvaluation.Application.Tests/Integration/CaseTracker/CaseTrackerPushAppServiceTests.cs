using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for <see cref="CaseTrackerPushAppService"/>, the "push to Case Tracker" button on an
/// appointment.
///
/// <para><b>WHAT IS PINNED.</b> An empty id and a still-PENDING appointment are refused -- only an
/// approved appointment is a case, so pushing earlier would open a case the office has not accepted.
/// Otherwise the appointment is queued for its own office, and the result reports the queued row and
/// whether the office's push switch is on (queued-but-off is a real state the screen must show).</para>
///
/// <para><b>The results asserted are the thrown error, the queued call and the returned DTO.</b> The
/// intake queue is substituted (its <c>EnqueueIntakeAsync</c> is virtual), so nothing is built or
/// pushed. The service is built with <c>new</c> and a substituted <see cref="IAbpLazyServiceProvider"/>.
/// Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerPushAppServiceTests
{
    private static readonly Guid OfficeId = new("cccccccc-0000-0000-0000-00000000000c");

    private static Appointment NewAppointment(AppointmentStatusType status) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 11, 5, 9, 0, 0),
        requestConfirmationNumber: "TEST-PU0001",
        appointmentStatus: status)
    {
        TenantId = OfficeId,
    };

    private static (CaseTrackerPushAppService Service, CaseTrackerIntakeQueue Queue, IntegrationOutboxItem Row) Build(
        Appointment appointment, string? pushSwitch)
    {
        var appointments = Substitute.For<IRepository<Appointment, Guid>>();
        appointments.GetAsync(appointment.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(appointment);
        var queue = Substitute.For<CaseTrackerIntakeQueue>(null, null, null, null, null);
        var row = new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake, "TEST/intake", appointment.Id, "{}", "TEST-key");
        queue.EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(row);
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled).Returns(pushSwitch);

        var service = new CaseTrackerPushAppService(appointments, queue, settings, NullLogger<CaseTrackerPushAppService>.Instance)
        {
            LazyServiceProvider = Substitute.For<IAbpLazyServiceProvider>(),
        };
        return (service, queue, row);
    }

    /// <summary>
    /// <b>POSITIVE CONTROL for the refusals.</b> An approved appointment is queued for its own office,
    /// and the result reports the queued row and the office's push switch.
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task PushAppointmentAsync_AnApprovedAppointment_IsQueuedForItsOffice_PositiveControl(string pushSwitch, bool expectedEnabled)
    {
        var appointment = NewAppointment(AppointmentStatusType.Approved);
        var (service, queue, row) = Build(appointment, pushSwitch);

        var result = await service.PushAppointmentAsync(appointment.Id);

        result.AppointmentId.ShouldBe(appointment.Id);
        result.OutboxItemId.ShouldBe(row.Id);
        result.Status.ShouldBe("Pending");
        result.PushEnabled.ShouldBe(expectedEnabled);
        await queue.Received(1).EnqueueIntakeAsync(appointment.Id, OfficeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PushAppointmentAsync_AnEmptyId_IsRefused()
    {
        var (service, queue, _) = Build(NewAppointment(AppointmentStatusType.Approved), "true");

        await Should.ThrowAsync<UserFriendlyException>(() => service.PushAppointmentAsync(Guid.Empty));

        await queue.DidNotReceive().EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A request still awaiting the office's decision is not a case yet, so it cannot be pushed.
    /// </summary>
    [Fact]
    public async Task PushAppointmentAsync_AStillPendingAppointment_IsRefused()
    {
        var appointment = NewAppointment(AppointmentStatusType.Pending);
        var (service, queue, _) = Build(appointment, "true");

        await Should.ThrowAsync<UserFriendlyException>(() => service.PushAppointmentAsync(appointment.Id));

        await queue.DidNotReceive().EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
