using System;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Unit tests for <see cref="SendAppointmentEmailArgs.BuildIdempotencyKey"/> (T3):
/// the deterministic, bounded key that lets the Phase 2 notification outbox
/// guarantee effectively-once delivery under Hangfire's at-least-once retries.
/// </summary>
public class EmailIdempotencyKeyTests
{
    private static readonly Guid Tenant =
        new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    [Fact]
    public void BuildIdempotencyKey_IsDeterministicForSameInputs()
    {
        var a = SendAppointmentEmailArgs.BuildIdempotencyKey(
            Tenant, "pat@x.com", "Transition/Approved/appt1", PacketKind.Patient);
        var b = SendAppointmentEmailArgs.BuildIdempotencyKey(
            Tenant, "pat@x.com", "Transition/Approved/appt1", PacketKind.Patient);

        a.ShouldBe(b);
        a.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildIdempotencyKey_NormalizesRecipientCaseAndWhitespace()
    {
        // Email is case-insensitive; a retry must not miss a match on casing/space.
        var a = SendAppointmentEmailArgs.BuildIdempotencyKey(Tenant, "Pat@X.com", "ctx", null);
        var b = SendAppointmentEmailArgs.BuildIdempotencyKey(Tenant, " pat@x.com ", "ctx", null);

        a.ShouldBe(b);
    }

    [Theory]
    [InlineData("other@x.com", "ctx", null)]              // different recipient
    [InlineData("pat@x.com", "ctx-2", null)]              // different context (appt/event)
    [InlineData("pat@x.com", "ctx", PacketKind.Doctor)]   // different packet kind
    public void BuildIdempotencyKey_DiffersWhenIdentityDiffers(string to, string context, PacketKind? kind)
    {
        var baseline = SendAppointmentEmailArgs.BuildIdempotencyKey(Tenant, "pat@x.com", "ctx", null);
        var other = SendAppointmentEmailArgs.BuildIdempotencyKey(Tenant, to, context, kind);

        other.ShouldNotBe(baseline);
    }
}
