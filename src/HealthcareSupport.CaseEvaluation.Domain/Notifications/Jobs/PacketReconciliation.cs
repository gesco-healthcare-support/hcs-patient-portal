using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// T11: pure detection of which packet kinds an Approved appointment is still
/// missing, so the reconciliation sweep can re-enqueue only those (per-kind, via
/// T2's Kind arg) without touching the ones already done.
///
/// <para>Every appointment expects all three kinds
/// (<see cref="GenerateAppointmentPacketJob"/>'s F5 "generate all three" rule). A
/// kind is incomplete when its row is missing, Failed, or stuck Generating past a
/// staleness threshold. Re-enqueuing is safe even against a live/retrying job
/// because T1 idempotency (skip-if-Generated + concurrency-claim skip) prevents a
/// double render / double email; the threshold only avoids redundant work.</para>
/// </summary>
public static class PacketReconciliation
{
    /// <summary>The kinds every Approved appointment must end up with.</summary>
    public static readonly IReadOnlyList<PacketKind> ExpectedKinds = new[]
    {
        PacketKind.Patient,
        PacketKind.Doctor,
        PacketKind.AttorneyClaimExaminer,
    };

    /// <summary>
    /// Returns the kinds still needing generation for one appointment, given its
    /// current packet rows. A Generating row is only counted incomplete once its
    /// last attempt is older than <paramref name="staleAfter"/> (a fresh in-flight
    /// render is left alone).
    /// </summary>
    public static IReadOnlyList<PacketKind> IncompleteKinds(
        IEnumerable<AppointmentPacket> packetsForAppointment,
        DateTime nowUtc,
        TimeSpan staleAfter)
    {
        var packets = packetsForAppointment.ToList();
        var incomplete = new List<PacketKind>();

        foreach (var kind in ExpectedKinds)
        {
            var packet = packets.FirstOrDefault(p => p.Kind == kind);
            if (packet == null)
            {
                incomplete.Add(kind); // never generated
                continue;
            }

            switch (packet.Status)
            {
                case PacketGenerationStatus.Generated:
                    break; // complete
                case PacketGenerationStatus.Failed:
                    incomplete.Add(kind);
                    break;
                case PacketGenerationStatus.Generating:
                    if (IsStale(packet, nowUtc, staleAfter))
                    {
                        incomplete.Add(kind); // stuck (worker died mid-render)
                    }
                    break;
            }
        }

        return incomplete;
    }

    // A Generating row is stale when its most recent attempt predates the
    // threshold. LastAttemptAt is stamped on each claim; legacy rows that predate
    // that stamp fall back to GeneratedAt (the initial insert time).
    private static bool IsStale(AppointmentPacket packet, DateTime nowUtc, TimeSpan staleAfter)
    {
        var lastAttempt = packet.LastAttemptAt ?? packet.GeneratedAt;
        return nowUtc - lastAttempt > staleAfter;
    }
}
