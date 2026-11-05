using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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

    private static OfficeNotificationRecipients Recipients(string address)
    {
        // The methods are virtual, so the constructor arguments are never used by the substitute.
        var recipients = Substitute.For<OfficeNotificationRecipients>(
            Substitute.For<ISettingProvider>(), null!, Substitute.For<ICurrentTenant>(),
            NullLogger<OfficeNotificationRecipients>.Instance);
        recipients.GetOfficeInboxAndIntakeAsync(Arg.Any<string>()).Returns(new List<NotificationRecipient>
        {
            new(email: address, role: Appointments.Notifications.RecipientRole.OfficeAdmin, isRegistered: false),
        });
        return recipients;
    }

    [Fact]
    public async Task OfficeSummary_LinksStaffToHostPortal()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var handler = new OfficeSummaryEmailHandler(
            dispatcher,
            Recipients("TEST-inbox@test.local"),
            Substitute.For<ICurrentTenant>(),
            HostVsTenantUrlBuilder(),
            NullLogger<OfficeSummaryEmailHandler>.Instance);

        await handler.HandleEventAsync(new OfficeSummaryEto
        {
            TenantId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 7, 23, 15, 0, 0, DateTimeKind.Utc),
            Counts = new StaffSummaryCounts { Pending = 1 },
        });

        await dispatcher.Received(1).DispatchAsync(
            Arg.Any<NotificationCase>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
            Arg.Is<IReadOnlyDictionary<string, object?>>(v => PortalUrlOf(v) == HostUrl),
            Arg.Any<string>());
    }
}
