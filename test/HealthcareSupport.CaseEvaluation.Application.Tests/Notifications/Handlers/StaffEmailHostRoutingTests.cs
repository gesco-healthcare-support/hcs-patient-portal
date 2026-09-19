using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// 2026-07-23 -- staff-ONLY notification digests must link staff to the HOST portal
/// (admin.&lt;base&gt;), not the office's {tenant}.&lt;base&gt; surface. Pure unit tests: the
/// dispatcher + URL builder are interface mocks (mirrors AccessorAddedEmailHandlerTests). The
/// URL builder returns distinct host vs tenant URLs so a passing test proves the handler asked
/// for the HOST url (null tenant), not the tenant one.
/// </summary>
public class StaffEmailHostRoutingTests
{
    private const string HostUrl = "https://admin.portal.test";
    private const string TenantUrl = "https://falkinstein.portal.test";

    private static IAccountUrlBuilder HostVsTenantUrlBuilder()
    {
        var urls = Substitute.For<IAccountUrlBuilder>();
        urls.BuildPortalRootUrlAsync(null).Returns(HostUrl);
        urls.BuildPortalRootUrlAsync(Arg.Is<Guid?>(g => g.HasValue)).Returns(TenantUrl);
        return urls;
    }

    private static string? PortalUrlOf(IReadOnlyDictionary<string, object?> v)
        => v.TryGetValue("PortalUrl", out var u) ? u as string : null;

    [Fact]
    public async Task InternalStaffQueueDigest_LinksStaffToHostPortal()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var handler = new InternalStaffQueueDigestEmailHandler(
            dispatcher,
            Substitute.For<ICurrentTenant>(),
            NullLogger<InternalStaffQueueDigestEmailHandler>.Instance,
            HostVsTenantUrlBuilder());

        await handler.HandleEventAsync(new InternalStaffQueueDigestEto
        {
            TenantId = Guid.NewGuid(),
            StaffUserId = Guid.NewGuid(),
            StaffEmail = "staff@example.test",
            StaffFirstName = "Denise",
            PendingAppointmentCount = 3,
            ApprovedAppointmentCount = 1,
        });

        await dispatcher.Received(1).DispatchAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
            Arg.Is<IReadOnlyDictionary<string, object?>>(v => PortalUrlOf(v) == HostUrl),
            Arg.Any<string>());
    }

    [Fact]
    public async Task PendingDailyDigest_LinksStaffToHostPortal()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail)
            .Returns("office@example.test");
        var sysParams = Substitute.For<ISystemParameterRepository>();
        sysParams.GetCurrentTenantAsync().Returns((SystemParameter?)null);

        var handler = new PendingDailyDigestEmailHandler(
            dispatcher,
            settings,
            sysParams,
            Substitute.For<ICurrentTenant>(),
            NullLogger<PendingDailyDigestEmailHandler>.Instance,
            HostVsTenantUrlBuilder());

        await handler.HandleEventAsync(new PendingDailyDigestEto
        {
            TenantId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 7, 23),
            Rows = new List<PendingDailyDigestRow>
            {
                new()
                {
                    RequestConfirmationNumber = "A0001",
                    PatientName = "Pat Example",
                    AppointmentDate = new DateTime(2026, 8, 1),
                    RequestedAt = new DateTime(2026, 7, 20),
                },
            },
        });

        await dispatcher.Received(1).DispatchAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
            Arg.Is<IReadOnlyDictionary<string, object?>>(v => PortalUrlOf(v) == HostUrl),
            Arg.Any<string>());
    }
}
