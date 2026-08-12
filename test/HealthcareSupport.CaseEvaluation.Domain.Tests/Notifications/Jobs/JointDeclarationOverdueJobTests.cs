using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Pins the two behaviours the 2026-08-08 change exists to guarantee: the job no longer CANCELS,
/// and it tells staff ONCE.
///
/// <para>The job used to set <c>CancelledNoBill</c> with no human involved. That is the behaviour
/// Adrian removed, so a test that merely checked "the marker got stamped" would pass just as
/// happily with the cancellation put back. These assert the absence.</para>
///
/// <para>Pure NSubstitute unit tests -- no DB, no ABP fixture -- matching
/// <c>ApprovalReconciliationJobTests</c>.</para>
/// </summary>
public class JointDeclarationOverdueJobTests
{
    private static readonly Guid OfficeId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AppointmentId = new("22222222-2222-2222-2222-222222222222");

    private const int CutoffDays = 5;

    /// <summary>Two days PAST the cutoff boundary, so the predicate fires without ambiguity.</summary>
    private static readonly DateTime DueDate = DateTime.UtcNow.Date.AddDays(CutoffDays - 2);

    [Fact]
    public async Task ExecuteAsync_WhenTheDeadlinePasses_LeavesTheStatusAloneAndPublishesNoStatusChange()
    {
        var appointment = OverdueAmeAppointment();
        var h = new Harness(appointment);

        await h.Job.ExecuteAsync();

        // The whole point of the change: the appointment survives.
        appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
        appointment.CancellationReason.ShouldBeNull();
        await h.Bus.DidNotReceive().PublishAsync(Arg.Any<AppointmentStatusChangedEto>());

        // ...and instead carries the marker that makes it visible to staff.
        appointment.JointDeclarationOverdueAt.ShouldNotBeNull();
        await h.Bus.Received(1).PublishAsync(
            Arg.Is<AppointmentJointDeclarationOverdueEto>(e =>
                e.AppointmentId == AppointmentId && e.TenantId == OfficeId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheFormStaysMissing_NotifiesStaffOnlyOnTheFirstRun()
    {
        // One appointment, two consecutive daily runs. The job mutates the instance the queryable
        // yields, so the second run re-reads a row that is already flagged -- exactly what happens
        // in production on day two.
        var appointment = OverdueAmeAppointment();
        var h = new Harness(appointment);

        await h.Job.ExecuteAsync();
        var stampedOnFirstRun = appointment.JointDeclarationOverdueAt;

        await h.Job.ExecuteAsync();

        await h.Bus.Received(1).PublishAsync(Arg.Any<AppointmentJointDeclarationOverdueEto>());
        await h.Appointments.Received(1).UpdateAsync(
            Arg.Any<Appointment>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());

        // The stamp records WHEN the deadline passed, not when the job last looked at it.
        appointment.JointDeclarationOverdueAt.ShouldBe(stampedOnFirstRun);
    }

    private static Appointment OverdueAmeAppointment()
    {
        return new Appointment(
            id: AppointmentId,
            patientId: Guid.NewGuid(),
            identityUserId: null,
            appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Ame,
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: DueDate.AddDays(-30),
            requestConfirmationNumber: "TEST-JDF-0001",
            appointmentStatus: AppointmentStatusType.Approved,
            dueDate: DueDate)
        {
            TenantId = OfficeId,
        };
    }

    /// <summary>
    /// The job's five collaborators wired to one overdue appointment and NO uploaded documents.
    /// </summary>
    private sealed class Harness
    {
        public Harness(Appointment appointment)
        {
            Appointments = Substitute.For<IRepository<Appointment, Guid>>();
            Appointments.GetQueryableAsync()
                .Returns(_ => new List<Appointment> { appointment }.AsQueryable());
            Appointments.GetAsync(appointment.Id).Returns(appointment);

            var documents = Substitute.For<IRepository<AppointmentDocument, Guid>>();
            documents.GetQueryableAsync()
                .Returns(_ => new List<AppointmentDocument>().AsQueryable());

            // GetAsync<int> is an EXTENSION over GetOrNullAsync, so the interface member is what a
            // substitute can intercept.
            var settings = Substitute.For<ISettingProvider>();
            settings.GetOrNullAsync(CaseEvaluationSettings.DocumentsPolicy.JointDeclarationUploadCutoffDays)
                .Returns(CutoffDays.ToString(CultureInfo.InvariantCulture));

            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
                .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

            Bus = Substitute.For<ILocalEventBus>();

            Job = new JointDeclarationOverdueJob(
                Appointments,
                documents,
                settings,
                tenantRunner,
                Bus,
                NullLogger<JointDeclarationOverdueJob>.Instance);
        }

        public IRepository<Appointment, Guid> Appointments { get; }

        public ILocalEventBus Bus { get; }

        public JointDeclarationOverdueJob Job { get; }
    }
}
