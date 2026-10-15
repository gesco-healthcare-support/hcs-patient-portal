using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Handlers;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The overdue marker must be tickable. Nothing cancels automatically any more, so the flag IS the
/// staff to-do list -- and a to-do item that never clears trains people to ignore the list.
/// </summary>
public class JdfOverdueMarkerClearHandlerTests
{
    private static readonly Guid OfficeId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AppointmentId = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime OverdueSince = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleEventAsync_WhenTheJointDeclarationArrives_ClearsTheMarker()
    {
        var appointment = OverdueAppointment();
        var repo = RepositoryFor(appointment);

        await BuildHandler(repo).HandleEventAsync(UploadedEto(isJointDeclaration: true));

        appointment.JointDeclarationOverdueAt.ShouldBeNull();
        await repo.Received(1).UpdateAsync(appointment, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleEventAsync_WhenSomeOtherDocumentArrives_LeavesTheMarkerAlone()
    {
        // An ad-hoc or package document says nothing about the Joint Declaration Form.
        var appointment = OverdueAppointment();
        var repo = RepositoryFor(appointment);

        await BuildHandler(repo).HandleEventAsync(UploadedEto(isJointDeclaration: false));

        appointment.JointDeclarationOverdueAt.ShouldBe(OverdueSince);
        await repo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [Fact]
    public async Task HandleEventAsync_WhenTheAppointmentWasNeverOverdue_WritesNothing()
    {
        // The common case -- most forms arrive on time. No write, so no pointless audit row.
        var appointment = OverdueAppointment();
        appointment.JointDeclarationOverdueAt = null;
        var repo = RepositoryFor(appointment);

        await BuildHandler(repo).HandleEventAsync(UploadedEto(isJointDeclaration: true));

        await repo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    private static JdfOverdueMarkerClearHandler BuildHandler(IRepository<Appointment, Guid> repo)
    {
        return new JdfOverdueMarkerClearHandler(
            repo,
            Substitute.For<ICurrentTenant>(),
            NullLogger<JdfOverdueMarkerClearHandler>.Instance);
    }

    private static IRepository<Appointment, Guid> RepositoryFor(Appointment appointment)
    {
        var repo = Substitute.For<IRepository<Appointment, Guid>>();
        repo.FindAsync(AppointmentId).Returns(appointment);
        return repo;
    }

    private static AppointmentDocumentUploadedEto UploadedEto(bool isJointDeclaration)
    {
        return new AppointmentDocumentUploadedEto
        {
            AppointmentId = AppointmentId,
            AppointmentDocumentId = Guid.NewGuid(),
            TenantId = OfficeId,
            IsAdHoc = false,
            IsJointDeclaration = isJointDeclaration,
            UploadedByUserId = Guid.NewGuid(),
            OccurredAt = OverdueSince.AddDays(1),
        };
    }

    private static Appointment OverdueAppointment()
    {
        return new Appointment(
            id: AppointmentId,
            patientId: Guid.NewGuid(),
            identityUserId: null,
            appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Ame,
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "TEST-JDF-0002",
            appointmentStatus: AppointmentStatusType.Approved,
            dueDate: new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc))
        {
            TenantId = OfficeId,
            JointDeclarationOverdueAt = OverdueSince,
        };
    }
}
