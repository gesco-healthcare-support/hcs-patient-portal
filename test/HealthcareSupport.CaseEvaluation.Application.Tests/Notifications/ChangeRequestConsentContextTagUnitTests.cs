using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Pins the consent-email context tag (epic phase 4c, 2026-08-05). This is the single fact
/// that makes consent rounds and resends work at all: the outbox idempotency key is
/// <c>SHA256(tenantId | recipientEmail | contextTag | packetKind)</c> and
/// <c>NotificationOutboxManager.EnqueueAsync</c> SILENTLY RETURNS THE EXISTING ROW on a match
/// -- no throw, no log. A tag that omits the round and the attempt therefore makes round 2's
/// email, and every resend, disappear with no error anywhere.
///
/// <para>Asserted through the REAL key derivation rather than by comparing tag strings, so the
/// test proves the property that matters (two dispatches produce two rows) instead of the
/// format this code happens to emit.</para>
/// </summary>
public class ChangeRequestConsentContextTagUnitTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-0000-4000-9000-000000000001");
    private static readonly Guid ChangeRequestId = Guid.Parse("bbbbbbbb-0000-4000-9000-000000000002");
    private const string Recipient = "rep-b@example.test";

    private static ChangeRequestConsentRequestedEto Eto(int roundNumber, int sendAttempt) =>
        new()
        {
            ChangeRequestId = ChangeRequestId,
            TenantId = TenantId,
            ChangeRequestType = ChangeRequestType.Reschedule,
            OpposingRecipientEmail = Recipient,
            RoundNumber = roundNumber,
            SendAttempt = sendAttempt,
        };

    private static string KeyFor(ChangeRequestConsentRequestedEto eventData) =>
        SendAppointmentEmailArgs.BuildIdempotencyKey(
            eventData.TenantId,
            eventData.OpposingRecipientEmail,
            ChangeRequestConsentRequestEmailHandler.BuildContextTag(eventData),
            kind: null);

    [Fact]
    public void Two_rounds_to_the_same_recipient_produce_different_idempotency_keys()
    {
        // Without this, confirming a second date silently sends nothing.
        KeyFor(Eto(roundNumber: 1, sendAttempt: 1))
            .ShouldNotBe(KeyFor(Eto(roundNumber: 2, sendAttempt: 1)));
    }

    [Fact]
    public void Two_attempts_within_one_round_produce_different_idempotency_keys()
    {
        // Without this, the resend button silently sends nothing.
        KeyFor(Eto(roundNumber: 1, sendAttempt: 1))
            .ShouldNotBe(KeyFor(Eto(roundNumber: 1, sendAttempt: 2)));
    }

    [Fact]
    public void The_same_round_and_attempt_produces_the_same_key_so_a_retry_collapses_to_one_row()
    {
        // The idempotency guarantee must survive: a Hangfire redelivery of the SAME dispatch
        // must still collapse, or one confirm sends duplicate emails.
        KeyFor(Eto(roundNumber: 2, sendAttempt: 3))
            .ShouldBe(KeyFor(Eto(roundNumber: 2, sendAttempt: 3)));
    }

    [Fact]
    public void A_cancellation_keeps_the_original_untagged_context()
    {
        // Phase 4c left the cancel path untouched: no rounds, one send per request.
        var cancellation = new ChangeRequestConsentRequestedEto
        {
            ChangeRequestId = ChangeRequestId,
            TenantId = TenantId,
            ChangeRequestType = ChangeRequestType.Cancel,
            OpposingRecipientEmail = Recipient,
        };

        ChangeRequestConsentRequestEmailHandler.BuildContextTag(cancellation)
            .ShouldBe($"ChangeRequestConsent/{ChangeRequestId}");
    }

    [Fact]
    public void A_round_dispatch_carries_the_round_and_attempt_in_its_context()
    {
        ChangeRequestConsentRequestEmailHandler.BuildContextTag(Eto(roundNumber: 2, sendAttempt: 3))
            .ShouldBe($"ChangeRequestConsent/{ChangeRequestId}/r2/a3");
    }
}
