using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;

/// <summary>
/// T5: thrown by <see cref="GenerateAppointmentPacketJob"/> when one or more packet
/// kinds ended Failed. Surfacing it (instead of reporting the whole job Succeeded)
/// lets Hangfire retry -- T1 idempotency makes the retry skip already-Generated kinds
/// and re-render only the Failed one -- and, once the attempt policy is exhausted,
/// dead-letter the job visibly on /hangfire rather than losing the packet + its email
/// silently.
/// </summary>
public class PacketGenerationIncompleteException : Exception
{
    public Guid AppointmentId { get; }

    public PacketGenerationIncompleteException(Guid appointmentId)
        : base($"Packet generation incomplete for appointment {appointmentId}: one or more kinds failed.")
    {
        AppointmentId = appointmentId;
    }
}
