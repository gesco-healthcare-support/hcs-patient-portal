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
/// Unit coverage for the three exits of <see cref="JointDeclarationOverdueJob"/> that
/// <c>JointDeclarationOverdueJobTests</c> does not reach: the office switch that disables the gate,
/// an office with no qualifying appointment, and one bad row that must not stop the rest.
///
/// <para><b>The result asserted is the published <see cref="AppointmentJointDeclarationOverdueEto"/>
/// and the stamped marker</b>, the job's two outputs. Pure NSubstitute, matching the existing file:
/// in-memory queryables, the interface member <c>GetOrNullAsync</c> behind <c>GetAsync&lt;int&gt;</c>.
/// The due date is a fixed PAST date, so the cutoff keeps firing whatever day this runs.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class JointDeclarationOverdueJobGateTests
{
    private static readonly Guid OfficeId = new("66666666-6666-6666-6666-666666666666");
    private static readonly DateTime PastDueDate = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public List<Appointment> Appointments { get; } = new();
        public IRepository<Appointment, Guid> AppointmentRepository { get; } = Substitute.For<IRepository<Appointment, Guid>>();
        public ILocalEventBus Bus { get; } = Substitute.For<ILocalEventBus>();
        public JointDeclarationOverdueJob Job { get; }

        public Harness(int cutoffDays = 5)
        {
            AppointmentRepository.GetQueryableAsync().Returns(_ => Appointments.AsQueryable());
            AppointmentRepository.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => Appointments.Single(a => a.Id == ci.Arg<Guid>()));
            var documents = Substitute.For<IRepository<AppointmentDocument, Guid>>();
            documents.GetQueryableAsync().Returns(_ => new List<AppointmentDocument>().AsQueryable());
            var settings = Substitute.For<ISettingProvider>();
            settings.GetOrNullAsync(CaseEvaluationSettings.DocumentsPolicy.JointDeclarationUploadCutoffDays)
                .Returns(cutoffDays.ToString(CultureInfo.InvariantCulture));
            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
                .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

            Job = new JointDeclarationOverdueJob(
                AppointmentRepository,
                documents,
                settings,
                tenantRunner,
                Bus,
                NullLogger<JointDeclarationOverdueJob>.Instance);
        }

        public List<AppointmentJointDeclarationOverdueEto> Flags() =>
            Bus.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<AppointmentJointDeclarationOverdueEto>().ToList();
    }

    private static Appointment OverdueAme(Guid? appointmentTypeId = null) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: appointmentTypeId ?? CaseEvaluationSeedIds.AppointmentTypes.Ame,
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: PastDueDate.AddDays(-30),
        requestConfirmationNumber: "TEST-JDF-0101",
        appointmentStatus: AppointmentStatusType.Approved,
        dueDate: PastDueDate)
    {
        TenantId = OfficeId,
    };

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> An overdue AME appointment with no Joint Declaration is flagged. The
    /// "flags nothing" Facts in this file depend on this fixture reaching the bus.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OverdueAmeWithNoJointDeclaration_IsFlagged_PositiveControl()
    {
        var h = new Harness();
        var appointment = OverdueAme();
        h.Appointments.Add(appointment);

        await h.Job.ExecuteAsync();

        h.Flags().ShouldHaveSingleItem().AppointmentId.ShouldBe(appointment.Id);
        appointment.JointDeclarationOverdueAt.ShouldNotBeNull();
    }

    /// <summary>
    /// A cutoff of zero is how an office switches the gate off: nothing is flagged, and the
    /// appointments are never even read.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_CutoffOfZeroOrLess_DisablesTheGate(int cutoffDays)
    {
        var h = new Harness(cutoffDays);
        var appointment = OverdueAme();
        h.Appointments.Add(appointment);

        await h.Job.ExecuteAsync();

        h.Flags().ShouldBeEmpty("a cutoff of zero or less means the office has the gate switched off");
        appointment.JointDeclarationOverdueAt.ShouldBeNull();
        await h.AppointmentRepository.DidNotReceive().GetQueryableAsync();
    }

    [Fact]
    public async Task ExecuteAsync_NoAmeAppointment_FlagsNothing()
    {
        var h = new Harness();
        var notAme = OverdueAme(appointmentTypeId: Guid.NewGuid());
        h.Appointments.Add(notAme);

        await h.Job.ExecuteAsync();

        h.Flags().ShouldBeEmpty("only AME appointments carry a Joint Declaration Form");
        notAme.JointDeclarationOverdueAt.ShouldBeNull();
    }

    /// <summary>
    /// One row that cannot be flagged (the load throws) must not stop the office's pass: the next
    /// overdue appointment is still flagged.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OneRowThatFails_DoesNotStopTheRest()
    {
        var h = new Harness();
        var broken = OverdueAme();
        var healthy = OverdueAme();
        h.Appointments.Add(broken);
        h.Appointments.Add(healthy);
        h.AppointmentRepository.GetAsync(broken.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Appointment>(_ => throw new InvalidOperationException("TEST-simulated load failure"));

        await h.Job.ExecuteAsync();

        h.Flags().ShouldHaveSingleItem().AppointmentId.ShouldBe(healthy.Id);
        healthy.JointDeclarationOverdueAt.ShouldNotBeNull();
        broken.JointDeclarationOverdueAt.ShouldBeNull();
    }
}
