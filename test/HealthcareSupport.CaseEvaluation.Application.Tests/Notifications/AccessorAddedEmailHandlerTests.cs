using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Issue #3 (2026-07-16): the existing-account accessor "you were added" handler
/// dispatches the AccessorAppointmentAdded template to the accessor, and no-ops when
/// the appointment is gone. DocumentEmailContextResolver is substituted (its
/// ResolveAsync is virtual; the null ctor args are never used because the override
/// short-circuits the real body). The dispatcher is an interface (clean mock).
/// </summary>
public class AccessorAddedEmailHandlerTests
{
    private static DocumentEmailContext SampleContext() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        RequestConfirmationNumber = "A00042",
        AppointmentDate = new DateTime(2026, 7, 20),
        PatientFirstName = "Sandra",
        PatientLastName = "Rivera",
        PatientEmail = "sandra.rivera@example.test",
        PortalBaseUrl = "https://falkinstein.portal.example.test",
    };

    private static DocumentEmailContextResolver StubResolver(DocumentEmailContext? ctx)
    {
        var resolver = Substitute.For<DocumentEmailContextResolver>(
            null, null, null, null, null, null, null, null, null, null);
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(ctx);
        return resolver;
    }

    private static AccessorAddedEmailHandler BuildHandler(
        INotificationDispatcher dispatcher, DocumentEmailContext? ctx) =>
        new(
            dispatcher,
            StubResolver(ctx),
            Substitute.For<ICurrentTenant>(),
            NullLogger<AccessorAddedEmailHandler>.Instance,
            Substitute.For<ITenantStore>());

    [Fact]
    public async Task HandleEventAsync_ExistingAccountAccessor_DispatchesAddedTemplateToAccessor()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var handler = BuildHandler(dispatcher, SampleContext());

        await handler.HandleEventAsync(new AppointmentAccessorAddedEto
        {
            AppointmentId = Guid.NewGuid(),
            AccessorUserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "accessor@example.test",
            RoleName = "Applicant Attorney",
            AccessTypeId = 23,
        });

        await dispatcher.Received(1).DispatchAsync(
            NotificationTemplateConsts.Codes.AccessorAppointmentAdded,
            Arg.Is<IReadOnlyCollection<NotificationRecipient>>(r => r.Any(x => x.Email == "accessor@example.test")),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentMissing_DoesNotDispatch()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var handler = BuildHandler(dispatcher, ctx: null);

        await handler.HandleEventAsync(new AppointmentAccessorAddedEto
        {
            AppointmentId = Guid.NewGuid(),
            Email = "a@x.test",
            RoleName = "Patient",
            TenantId = Guid.NewGuid(),
        });

        await dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<string>());
    }
}
