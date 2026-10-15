using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Decides whether an appointment's intake may be pushed AUTOMATICALLY yet. Pure, so every automatic
/// enqueue site shares one answer and cannot drift.
///
/// <para>Exists because approval alone is the wrong moment to push. Approval kicks off packet
/// rendering, so an intake sent at approval carries no packets and is superseded seconds later when
/// the packets land -- two messages to say one thing. Measured on the first live approval
/// (falkinstein A00004, 2026-07-30): two intake rows ten seconds apart, the second identical but for
/// a populated <c>documents</c> array. Holding the intake until the packet set settles collapses that
/// into one complete message.</para>
///
/// <para>ACCEPTED TRADE-OFF: the Case Tracker learns of an approval seconds-to-minutes later than it
/// used to. That is the cost of a single complete message, agreed 2026-07-30, and it reverses the
/// original "push immediately, packets follow" decision.</para>
/// </summary>
public static class IntakeSettlePolicy
{
    /// <summary>
    /// True once the packet set has stopped changing, so the intake we build now is the one the
    /// receiver should keep.
    ///
    /// <para>Settled means EITHER every kind rendered, OR nothing has moved since
    /// <paramref name="cutoffUtc"/> -- a set still mid-render is neither.</para>
    ///
    /// <para>Deliberately NOT <see cref="PacketSetPolicy.ShouldRelease"/>, which additionally requires
    /// at least one generated packet. That guard is right for the document feed, where publishing an
    /// empty set tells the receiver nothing. It is wrong here: an intake must eventually reach the
    /// Case Tracker even when every template failed, because the appointment itself is the news and a
    /// withheld intake is a case their staff never see. So a set stuck with nothing generated still
    /// settles once the cutoff passes, and pushes with an empty document list -- exactly what the old
    /// push-on-approval behaviour sent every time.</para>
    /// </summary>
    /// <param name="appointment">The appointment being considered.</param>
    /// <param name="packets">Its packet rows; may be empty if generation never started.</param>
    /// <param name="cutoffUtc">Rows must have been idle since this instant to count as stalled.</param>
    public static bool IsSettled(
        Appointment appointment,
        IEnumerable<AppointmentPacket> packets,
        DateTime cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        ArgumentNullException.ThrowIfNull(packets);

        var rows = packets as IReadOnlyCollection<AppointmentPacket> ?? packets.ToList();

        if (PacketSetPolicy.IsComplete(rows))
        {
            return true;
        }

        return LastMovedAt(appointment, rows) < cutoffUtc;
    }

    /// <summary>
    /// When this appointment's packet story last moved. With no packet rows there is nothing to time
    /// from, so the appointment's own stamp stands in -- otherwise an appointment whose packet
    /// generation never even created rows would never settle, and its intake would never be sent.
    /// </summary>
    private static DateTime LastMovedAt(
        Appointment appointment,
        IReadOnlyCollection<AppointmentPacket> packets)
    {
        if (packets.Count == 0)
        {
            return appointment.LastModificationTime ?? appointment.CreationTime;
        }

        return packets.Max(PacketSetPolicy.LastChangedAt);
    }
}
