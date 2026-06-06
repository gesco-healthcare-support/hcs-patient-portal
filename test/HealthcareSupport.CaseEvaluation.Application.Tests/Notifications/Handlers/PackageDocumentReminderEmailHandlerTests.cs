using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Group L (G-05-02 Option B) test for
/// <see cref="PackageDocumentReminderEmailHandler"/>: a Joint Declaration row
/// dispatches the distinct JDF template, while a package-document row dispatches
/// the generic UploadPendingDocuments template. Both ride the same reminder
/// cadence (one handler, one event) -- only the rendered template differs.
/// </summary>
public class PackageDocumentReminderEmailHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (PackageDocumentReminderEmailHandler Handler, INotificationDispatcher Dispatcher) Build()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        // DocumentEmailContextResolver.ResolveAsync is virtual; stub it to a
        // non-null context so the handler proceeds to dispatch.
        var contextResolver = Substitute.For<DocumentEmailContextResolver>(
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<Patient, Guid>>(),
            Substitute.For<IRepository<AppointmentDocument, Guid>>(),
            Substitute.For<IRepository<AppointmentInjuryDetail, Guid>>(),
            Substitute.For<IRepository<IdentityUser, Guid>>(),
            Substitute.For<IAccountUrlBuilder>());
        contextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>())
            .Returns(new DocumentEmailContext
            {
                RequestConfirmationNumber = "A00001",
                DueDate = new DateTime(2026, 1, 1),
                DocumentName = "Joint Declaration Form",
                PortalBaseUrl = "http://portal.example",
            });

        var recipientResolver = Substitute.For<IAppointmentRecipientResolver>();
        recipientResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>())
            .Returns(new List<SendAppointmentEmailArgs>
            {
                new() { To = "stakeholder@example.com" },
            });

        // Real appender with a substituted repo that returns no row -> AppendAsync
        // is a no-op (the method is non-virtual, so it cannot be stubbed).
        var ccAppender = new CcRecipientAppender(
            Substitute.For<ISystemParameterRepository>(),
            NullLogger<CcRecipientAppender>.Instance);

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        var handler = new PackageDocumentReminderEmailHandler(
            dispatcher,
            contextResolver,
            recipientResolver,
            ccAppender,
            currentTenant,
            NullLogger<PackageDocumentReminderEmailHandler>.Instance,
            Substitute.For<IAccountUrlBuilder>());

        return (handler, dispatcher);
    }

    [Fact]
    public async Task Jdf_row_dispatches_the_jdf_template()
    {
        var (handler, dispatcher) = Build();

        await handler.HandleEventAsync(new PackageDocumentReminderEto
        {
            AppointmentId = Guid.NewGuid(),
            AppointmentDocumentId = Guid.NewGuid(),
            TenantId = TenantId,
            IsJointDeclaration = true,
        });

        await dispatcher.Received(1).DispatchAsync(
            NotificationTemplateConsts.Codes.JointDeclarationUploadReminder,
            Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<string>(),
            Arg.Any<PacketAttachmentRef?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Package_row_dispatches_the_generic_template()
    {
        var (handler, dispatcher) = Build();

        await handler.HandleEventAsync(new PackageDocumentReminderEto
        {
            AppointmentId = Guid.NewGuid(),
            AppointmentDocumentId = Guid.NewGuid(),
            TenantId = TenantId,
            IsJointDeclaration = false,
        });

        await dispatcher.Received(1).DispatchAsync(
            NotificationTemplateConsts.Codes.UploadPendingDocuments,
            Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<string>(),
            Arg.Any<PacketAttachmentRef?>(),
            Arg.Any<CancellationToken>());
    }
}
