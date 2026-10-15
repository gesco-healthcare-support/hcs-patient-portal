using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for the in-app bell notifications raised to office staff
/// (<c>InAppNotificationHandlers.cs</c>): a user question, a document upload, and the null-event
/// guards on the submission and change-request handlers.
///
/// <para><b>The result asserted is the notification RAISED</b> -- its type, title, body and link --
/// read back from a substituted <see cref="AppNotificationManager"/>. The manager's constructor only
/// stores its arguments and <c>RaiseForOfficeStaffAsync</c> is virtual, so the fan-out to staff users
/// (the part that needs <c>IdentityUserManager</c>) never runs and no row is written.</para>
///
/// <para>Each "raises nothing" Fact sits beside a Fact on the same handler that DOES raise, so none of
/// them can pass merely because the handler bailed out early. Synthetic data only (HIPAA).</para>
/// </summary>
public class InAppNotificationHandlersTests
{
    private sealed record Raised(AppNotificationType Type, string Title, string Body, string? Url);

    private static AppNotificationManager NewManager() =>
        Substitute.For<AppNotificationManager>(null, null, null);

    private static List<Raised> RaisedBy(AppNotificationManager manager) =>
        manager.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(AppNotificationManager.RaiseForOfficeStaffAsync))
            .Select(c => c.GetArguments())
            .Select(a => new Raised((AppNotificationType)a[0]!, (string)a[1]!, (string)a[2]!, (string?)a[3]))
            .ToList();

    private static IRepository<Appointment, Guid> RepositoryReturning(Appointment? appointment)
    {
        var repository = Substitute.For<IRepository<Appointment, Guid>>();
        repository.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(appointment);
        return repository;
    }

    private static Appointment NewAppointment(string confirmation) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 11, 3, 9, 0, 0),
        requestConfirmationNumber: confirmation,
        appointmentStatus: AppointmentStatusType.Pending);

    // ----- AppointmentSubmittedInAppNotificationHandler -----

    [Fact]
    public async Task AppointmentSubmitted_RaisesARequestNotificationLinkingTheAppointment_PositiveControl()
    {
        var manager = NewManager();
        var appointmentId = Guid.NewGuid();

        await new AppointmentSubmittedInAppNotificationHandler(manager, Substitute.For<ICurrentTenant>())
            .HandleEventAsync(new AppointmentSubmittedEto { AppointmentId = appointmentId, RequestConfirmationNumber = "TEST-S0001" });

        var raised = RaisedBy(manager).ShouldHaveSingleItem();
        raised.Type.ShouldBe(AppNotificationType.AppointmentRequested);
        raised.Body.ShouldContain("TEST-S0001");
        raised.Url.ShouldBe($"/appointments/view/{appointmentId}");
    }

    [Fact]
    public async Task AppointmentSubmitted_NullEvent_RaisesNothing()
    {
        var manager = NewManager();

        await new AppointmentSubmittedInAppNotificationHandler(manager, Substitute.For<ICurrentTenant>()).HandleEventAsync(null!);

        RaisedBy(manager).ShouldBeEmpty();
    }

    // ----- ChangeRequestSubmittedInAppNotificationHandler -----

    [Fact]
    public async Task ChangeRequestSubmitted_ACancellation_NamesTheAppointmentsConfirmationNumber()
    {
        var manager = NewManager();

        await new ChangeRequestSubmittedInAppNotificationHandler(manager, RepositoryReturning(NewAppointment("TEST-C0001")), Substitute.For<ICurrentTenant>())
            .HandleEventAsync(new AppointmentChangeRequestSubmittedEto { AppointmentId = Guid.NewGuid(), ChangeRequestType = ChangeRequestType.Cancel });

        var raised = RaisedBy(manager).ShouldHaveSingleItem();
        raised.Title.ShouldBe("Cancellation request");
        raised.Body.ShouldContain("TEST-C0001");
    }

    [Fact]
    public async Task ChangeRequestSubmitted_NullEvent_RaisesNothing()
    {
        var manager = NewManager();

        await new ChangeRequestSubmittedInAppNotificationHandler(manager, RepositoryReturning(null), Substitute.For<ICurrentTenant>())
            .HandleEventAsync(null!);

        RaisedBy(manager).ShouldBeEmpty();
    }

    // ----- UserQuerySubmittedInAppNotificationHandler -----

    /// <summary>
    /// A question about a known request names it; a general question says so. Neither links anywhere
    /// (a question is not tied to an appointment page).
    /// </summary>
    [Theory]
    [InlineData("TEST-Q0001", "A user submitted a question about TEST-Q0001.")]
    [InlineData(null, "A user submitted a question.")]
    [InlineData("   ", "A user submitted a question.")]
    public async Task UserQuerySubmitted_RaisesAQuestionNotification(string? confirmation, string expectedBody)
    {
        var manager = NewManager();

        await new UserQuerySubmittedInAppNotificationHandler(manager, Substitute.For<ICurrentTenant>())
            .HandleEventAsync(new UserQuerySubmittedEto { UserQueryId = Guid.NewGuid(), Message = "TEST-message", RequestConfirmationNumber = confirmation });

        var raised = RaisedBy(manager).ShouldHaveSingleItem();
        raised.Type.ShouldBe(AppNotificationType.QuerySubmitted);
        raised.Title.ShouldBe("New question");
        raised.Body.ShouldBe(expectedBody);
        raised.Url.ShouldBeNull();
    }

    [Fact]
    public async Task UserQuerySubmitted_NullEvent_RaisesNothing()
    {
        var manager = NewManager();

        await new UserQuerySubmittedInAppNotificationHandler(manager, Substitute.For<ICurrentTenant>()).HandleEventAsync(null!);

        RaisedBy(manager).ShouldBeEmpty();
    }

    // ----- DocumentUploadedInAppNotificationHandler -----

    /// <summary>
    /// An upload names the appointment's confirmation number when the appointment is found, and falls
    /// back to "an appointment" when it is not -- the notification is still raised either way.
    /// </summary>
    [Theory]
    [InlineData(true, "A document was uploaded for TEST-D0001.")]
    [InlineData(false, "A document was uploaded for an appointment.")]
    public async Task DocumentUploaded_RaisesAnUploadNotificationLinkingTheAppointment(bool appointmentFound, string expectedBody)
    {
        var manager = NewManager();
        var appointmentId = Guid.NewGuid();
        var repository = RepositoryReturning(appointmentFound ? NewAppointment("TEST-D0001") : null);

        await new DocumentUploadedInAppNotificationHandler(manager, repository, Substitute.For<ICurrentTenant>())
            .HandleEventAsync(new AppointmentDocumentUploadedEto { AppointmentId = appointmentId, AppointmentDocumentId = Guid.NewGuid() });

        var raised = RaisedBy(manager).ShouldHaveSingleItem();
        raised.Type.ShouldBe(AppNotificationType.DocumentUploaded);
        raised.Title.ShouldBe("Document uploaded");
        raised.Body.ShouldBe(expectedBody);
        raised.Url.ShouldBe($"/appointments/view/{appointmentId}");
    }

    [Fact]
    public async Task DocumentUploaded_NullEvent_RaisesNothing()
    {
        var manager = NewManager();

        await new DocumentUploadedInAppNotificationHandler(manager, RepositoryReturning(null), Substitute.For<ICurrentTenant>())
            .HandleEventAsync(null!);

        RaisedBy(manager).ShouldBeEmpty();
    }
}
