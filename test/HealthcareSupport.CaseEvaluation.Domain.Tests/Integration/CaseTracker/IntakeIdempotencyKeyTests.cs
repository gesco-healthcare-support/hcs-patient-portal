using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The outbox dedup key must track the PAYLOAD, not one of the tables the payload is built from
/// (2026-08-13).
///
/// <para>It used to be versioned by the appointment's own <c>UpdatedAt</c>. Every correction to
/// something else -- patient demographics, an attorney, the employer, an insurance carrier, an
/// injury -- rebuilds the payload but leaves that timestamp alone, so the enqueue collapsed onto the
/// existing row and the corrected payload was DISCARDED. When the row had already been sent, nothing
/// was queued at all and the correction never reached the Case Tracker. That directly defeated the
/// address work Levon needs for proof-of-service.</para>
///
/// <para>Demonstrated live before the fix: correcting a patient's unit changed nothing on the wire,
/// while editing any appointment field pushed the identical correction immediately.</para>
///
/// <para>All fixture data is synthetic.</para>
/// </summary>
public class IntakeIdempotencyKeyTests
{
    private static readonly Guid AppointmentId = new("d4e5f6a7-b8c9-4d0e-8f1a-2b3c4d5e6f70");

    private static IntakeEnvelope EnvelopeWith(string? unit = "4B", string lastName = "Synthetica")
        => new()
        {
            Data = new IntakePayload
            {
                AppointmentId = AppointmentId,
                ConfirmationNumber = "A00099",
                UpdatedAt = "2026-08-01T00:00:00Z",
                Patient = new IntakePatientSection
                {
                    FirstName = "Testadora",
                    LastName = lastName,
                    Street = "1200 Sample Street",
                    Unit = unit,
                    City = "Sample City",
                    ZipCode = "90210",
                },
            },
            Meta = new IntakeMeta
            {
                RequestId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.ToString("O"),
            },
        };

    private static string KeyFor(IntakeEnvelope envelope)
        => IntegrationOutboxManager.BuildIdempotencyKey(
            IntegrationMessageType.Intake,
            AppointmentId,
            IntakePayloadSerializer.SerializeDataForVersioning(envelope));

    [Fact]
    public void AReplayOfTheSameStateCollapsesOntoOneKey()
    {
        // Meta differs on every build (fresh RequestId + Timestamp). If the key covered it, nothing
        // would ever dedup and every redelivered event would push a duplicate case.
        var first = EnvelopeWith();
        var second = EnvelopeWith();

        second.Meta.RequestId.ShouldNotBe(first.Meta.RequestId);
        KeyFor(second).ShouldBe(KeyFor(first));
    }

    [Fact]
    public void CorrectingTheUnitProducesANewKey()
    {
        // THE regression. The appointment row is untouched by this edit -- only the patient changed.
        var before = EnvelopeWith(unit: "4B");
        var after = EnvelopeWith(unit: "STE 1200");

        KeyFor(after).ShouldNotBe(KeyFor(before));
    }

    [Fact]
    public void CorrectingAnyDemographicProducesANewKey()
    {
        // Generalises the case above: it was never specific to the unit, so the test should not be.
        var before = EnvelopeWith(lastName: "Synthetica");
        var after = EnvelopeWith(lastName: "Corrected");

        KeyFor(after).ShouldNotBe(KeyFor(before));
    }

    [Fact]
    public void TheKeyIsStableAcrossBuildsOfIdenticalData()
    {
        // Determinism: the same content must hash the same way every time, or dedup is a coin toss.
        KeyFor(EnvelopeWith()).ShouldBe(KeyFor(EnvelopeWith()));
    }
}
