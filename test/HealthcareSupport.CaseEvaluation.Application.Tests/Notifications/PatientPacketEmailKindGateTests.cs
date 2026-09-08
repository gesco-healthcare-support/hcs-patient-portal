using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 3 task 6 (path 3.4a) -- guardian for the packet-KIND gate on the patient packet
/// email handler.
///
/// <para><b>WHAT THE GATE IS.</b> Three packet kinds are generated for every approved
/// appointment, and each has its own handler subscribed to the SAME event
/// (<c>PacketGeneratedEto</c>). The only thing stopping every handler from firing on every
/// kind is a single early return -- <c>PatientPacketEmailHandler.cs:59</c>,
/// <c>eventData.Kind != PacketKind.Patient</c>. Delete it and the patient packet email
/// dispatches for the Doctor and Attorney/Claim-Examiner kinds too, sending the wrong packet
/// to the patient (or, when the patient email is unset, to their applicant attorney).</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL -- read before changing this fixture.</b>
/// The obvious test is "assert the dispatcher was not called for a non-Patient kind". That test
/// PASSES WITH THE GATE DELETED, for the wrong reason: with the gate gone the handler runs on to
/// <c>_contextResolver.ResolveAsync</c>, and an unconfigured substitute returns null, so the
/// handler logs "appointment not found" and returns without dispatching. The assertion holds and
/// proves nothing.</para>
///
/// <para>So the fixture configures the resolver to return a real context whose
/// <c>PatientEmail</c> is set -- enough to reach <c>DispatchAsync</c> -- and
/// <see cref="HandleEventAsync_PatientKind_DispatchesTheEmail_PositiveControl"/> asserts that a
/// Patient-kind event DOES dispatch. <b>That control is what gives the two negative assertions
/// their meaning.</b> If it ever fails, the negative tests below are vacuous and must not be
/// trusted until it is fixed. This is the same defect as a one-kind fixture in
/// <c>PacketKindVisibilityTests</c> and as phase 3 task 1's empty <c>ServiceCollection</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <c>INotificationDispatcher</c> is an interface and is fully
/// substituted, so no template renders, nothing is enqueued and no transport is reached -- the
/// standing hazard from the Pacific epic (packet generate/regenerate fans out to every party and
/// attaches documents) cannot fire here at all. That is stronger than asserting against the
/// outbox, because the send path is never entered.</para>
///
/// <para><b>NOT COVERED HERE, deliberately and with the reason.</b>
/// <c>AttyCEPacketEmailHandler:94</c> carries the mirror-image gate
/// (<c>Kind != PacketKind.AttorneyClaimExaminer</c>) and is NOT tested. Its constructor takes the
/// concrete <c>IdentityUserManager</c>, which NSubstitute cannot construct without arguments, and
/// its dispatch path dereferences it at <c>:158</c> (<c>FindByEmailAsync</c>) -- so a null stands
/// in only for the early-return paths, which is exactly where a guardian would be vacuous: with
/// the gate deleted it would also need <c>IAppointmentRecipientResolver</c> configured to return
/// AA/DA/CE recipients, or <c>recipients.Count == 0</c> makes it return without dispatching and
/// the test passes anyway. Both are fixture-building, the same family as issue #710. Stated rather
/// than left as a silent half-coverage.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class PatientPacketEmailKindGateTests
{
    private static DocumentEmailContext SampleContext() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        RequestConfirmationNumber = "A00931",
        AppointmentDate = new DateTime(2026, 9, 21),
        PatientFirstName = "Dolores",
        PatientLastName = "Abernathy",
        PatientEmail = "dolores.abernathy@example.test",
        PortalBaseUrl = "https://office-a.portal.example.test",
    };

    /// <summary>
    /// The ten nulls are never dereferenced: <c>ResolveAsync</c> is <c>virtual</c> and the
    /// override below short-circuits the real body. Same pattern as
    /// <c>AccessorAddedEmailHandlerTests</c>.
    /// </summary>
    private static DocumentEmailContextResolver StubResolver(DocumentEmailContext? ctx)
    {
        var resolver = Substitute.For<DocumentEmailContextResolver>(
            null, null, null, null, null, null, null, null, null, null);
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(ctx);
        return resolver;
    }

    private static PatientPacketEmailHandler BuildHandler(INotificationDispatcher dispatcher) =>
        new(
            dispatcher,
            StubResolver(SampleContext()),
            Substitute.For<ICurrentTenant>(),
            NullLogger<PatientPacketEmailHandler>.Instance,
            Substitute.For<IAccountUrlBuilder>());

    private static PacketGeneratedEto EventFor(PacketKind kind) => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        PacketId = Guid.NewGuid(),
        Kind = kind,
        OccurredAt = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// <b>POSITIVE CONTROL. This is load-bearing, not a happy-path nicety.</b> It proves the
    /// fixture can reach <c>DispatchAsync</c> at all. Without it, the two guardians below would
    /// pass even with the gate deleted, because a handler that bails out early also fails to
    /// dispatch. If this test fails, treat the guardians as vacuous until it is green again.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_PatientKind_DispatchesTheEmail_PositiveControl()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await BuildHandler(dispatcher).HandleEventAsync(EventFor(PacketKind.Patient));

        await dispatcher.ReceivedWithAnyArgs(1).DispatchAsync(
            default!, default!, default!, default!, default, default);
    }

    /// <summary>
    /// GUARDIAN. Delete <c>PatientPacketEmailHandler.cs:59</c> and this fails: the patient
    /// packet email dispatches for a kind the patient must never be sent.
    /// </summary>
    [Theory]
    [InlineData(PacketKind.Doctor)]
    [InlineData(PacketKind.AttorneyClaimExaminer)]
    public async Task HandleEventAsync_NonPatientKind_DispatchesNothing(PacketKind kind)
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await BuildHandler(dispatcher).HandleEventAsync(EventFor(kind));

        dispatcher.ReceivedCalls().ShouldBeEmpty(
            $"the {kind} packet must not trigger the PATIENT packet email. This handler and the "
            + "attorney handler subscribe to the same PacketGeneratedEto, so the kind check is "
            + "the only thing separating them -- and the positive control in this file proves "
            + "this fixture does reach DispatchAsync when the kind matches, so an empty call "
            + "list here is the gate working rather than the handler bailing out early");
    }

    /// <summary>
    /// GUARDIAN for the other half of the same line. <c>eventData == null</c> shares the early
    /// return, so a refactor that splits the condition can drop this case silently.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_NullEvent_DispatchesNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await BuildHandler(dispatcher).HandleEventAsync(null!);

        dispatcher.ReceivedCalls().ShouldBeEmpty(
            "a null event must be ignored, not dispatched on -- it shares the early return with "
            + "the kind check at PatientPacketEmailHandler.cs:59");
    }
}
