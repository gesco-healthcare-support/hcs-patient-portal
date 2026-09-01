using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Decides when an appointment's packets are worth publishing. Pure, so the two callers -- the
/// per-packet handler and the reconciliation release -- cannot disagree, and so the rule is testable
/// without a database.
///
/// <para>Packets publish as ONE batch rather than three dribbles because a partial packet set is not
/// useful to their staff, and three separate pushes for a single approval would triple the traffic to
/// say the same thing.</para>
/// </summary>
public static class PacketSetPolicy
{
    /// <summary>
    /// Every kind an approval renders. Read from the enum rather than hardcoded, so adding a fourth
    /// template cannot silently leave the completeness check checking three.
    /// </summary>
    public static IReadOnlyList<PacketKind> AllKinds { get; } = Enum.GetValues<PacketKind>();

    /// <summary>
    /// How long a packet set must sit unchanged before it counts as stalled rather than still
    /// rendering. Comfortably longer than a normal render (seconds to a couple of minutes) so a slow
    /// job is never mistaken for a stuck one, but short enough that their staff are not waiting on a
    /// dead template.
    ///
    /// <para>ONE constant, deliberately: both the document feed's stalled release and
    /// <see cref="IntakeSettlePolicy"/>'s settle check time out on the same boundary. Two constants
    /// would let the intake and its packets disagree about when a set has given up.</para>
    /// </summary>
    public const int SettleAfterMinutes = 30;

    /// <summary>The instant a packet row must predate to count as idle. See <see cref="SettleAfterMinutes"/>.</summary>
    public static DateTime Cutoff(DateTime nowUtc) => nowUtc.AddMinutes(-SettleAfterMinutes);

    /// <summary>True once every kind has reached <c>Generated</c>.</summary>
    public static bool IsComplete(IEnumerable<AppointmentPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        var generated = packets
            .Where(p => p.Status == PacketGenerationStatus.Generated)
            .Select(p => p.Kind)
            .ToHashSet();

        return AllKinds.All(generated.Contains);
    }

    /// <summary>
    /// True when the set will never complete on its own and holding the rest back no longer serves
    /// anyone: it is incomplete, at least one kind DID generate (so there is something to publish),
    /// and no row has changed since <paramref name="cutoffUtc"/> -- meaning the missing kind is not
    /// still rendering, it is stuck or failed.
    ///
    /// <para>Without this, one permanently failed template would withhold the other two packets from
    /// the Case Tracker forever.</para>
    /// </summary>
    public static bool ShouldRelease(IEnumerable<AppointmentPacket> packets, DateTime cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(packets);

        var rows = packets as IReadOnlyCollection<AppointmentPacket> ?? packets.ToList();
        if (rows.Count == 0 || IsComplete(rows))
        {
            return false;
        }

        if (!rows.Any(p => p.Status == PacketGenerationStatus.Generated))
        {
            return false; // nothing fetchable yet -- releasing would publish an empty set
        }

        return rows.All(p => LastChangedAt(p) < cutoffUtc);
    }

    /// <summary>
    /// When this row last moved. Uses the audit stamps rather than <c>GeneratedAt</c>, which is still
    /// <c>default</c> on a row that has only ever been <c>Generating</c>.
    /// </summary>
    public static DateTime LastChangedAt(AppointmentPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.LastModificationTime ?? packet.CreationTime;
    }
}
