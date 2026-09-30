using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Unit coverage for how <see cref="BookerCcDispatcher"/> picks the To address when the booker's own
/// address is NOT known, and what it does when nobody qualifies.
///
/// <para><b>WHAT IS PINNED.</b> With no booker email the To falls back to the first Patient-role party
/// (a party with no role counts as Patient), then to the first party of any role; with nobody at all
/// there is no To, no CC, and nothing is sent.</para>
///
/// <para><b>The results asserted are the partition returned and the send made.</b> The dispatcher is a
/// substitute, so no mail can leave; the CC appender is real over a substituted repository that
/// returns no office CC list. The first Fact is the positive control for the "sends nothing" Fact.
/// Synthetic data only (HIPAA).</para>
/// </summary>
public class BookerCcDispatcherFallbackTests
{
    private static NotificationRecipient Party(string email, RecipientRole? role) =>
        new(email: email, role: role, isRegistered: true);

    private static BookerCcDispatcher Build(INotificationDispatcher dispatcher)
    {
        var systemParameters = Substitute.For<ISystemParameterRepository>();
        systemParameters.GetCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((SystemParameter?)null);
        return new BookerCcDispatcher(
            dispatcher,
            new CcRecipientAppender(systemParameters, NullLogger<CcRecipientAppender>.Instance),
            NullLogger<BookerCcDispatcher>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void PartitionToBookerCc_NoBookerEmail_PicksTheFirstPatientRoleParty(string? bookerEmail)
    {
        var parties = new[]
        {
            Party("TEST-aa@test.local", RecipientRole.ApplicantAttorney),
            Party("TEST-no-role@test.local", role: null),
            Party("TEST-patient@test.local", RecipientRole.Patient),
        };

        var (to, cc) = BookerCcDispatcher.PartitionToBookerCc(parties, bookerEmail);

        to.ShouldNotBeNull();
        to.Email.ShouldBe("TEST-no-role@test.local", "a party with no role counts as the Patient");
        cc.Select(r => r.Email).ShouldNotContain("TEST-no-role@test.local");
    }

    [Fact]
    public void PartitionToBookerCc_NoBookerEmailAndNoPatientRole_PicksTheFirstPartyOfAnyRole()
    {
        var parties = new[]
        {
            Party("TEST-aa@test.local", RecipientRole.ApplicantAttorney),
            Party("TEST-da@test.local", RecipientRole.DefenseAttorney),
        };

        var (to, _) = BookerCcDispatcher.PartitionToBookerCc(parties, bookerEmail: null);

        to.ShouldNotBeNull();
        to.Email.ShouldBe("TEST-aa@test.local");
    }

    [Fact]
    public void PartitionToBookerCc_NobodyAtAll_ReturnsNoToAndAnEmptyCcList()
    {
        var (to, cc) = BookerCcDispatcher.PartitionToBookerCc(Array.Empty<NotificationRecipient>(), bookerEmail: null);

        to.ShouldBeNull();
        cc.ShouldBeEmpty();
    }

    /// <summary><b>POSITIVE CONTROL.</b> With a fallback To available, one email is sent.</summary>
    [Fact]
    public async Task DispatchToBookerWithCcAsync_AFallbackToExists_SendsOneEmail_PositiveControl()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).DispatchToBookerWithCcAsync(
            "TEST-template", bookerEmail: null,
            new[] { Party("TEST-patient@test.local", RecipientRole.Patient) },
            new Dictionary<string, object?>(), "TEST-ctx/1");

        var to = (NotificationRecipient)dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchToWithCcAsync))
            .ShouldHaveSingleItem().GetArguments()[1]!;
        to.Email.ShouldBe("TEST-patient@test.local");
    }

    [Fact]
    public async Task DispatchToBookerWithCcAsync_NobodyToAddress_SendsNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).DispatchToBookerWithCcAsync(
            "TEST-template", bookerEmail: null, Array.Empty<NotificationRecipient>(),
            new Dictionary<string, object?>(), "TEST-ctx/2");

        dispatcher.ReceivedCalls().ShouldBeEmpty("with no To there is nobody to address the email to");
    }
}
