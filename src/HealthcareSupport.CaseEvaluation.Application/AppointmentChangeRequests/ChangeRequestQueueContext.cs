using System;
using System.Collections.Generic;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 4b (2026-08-04) -- pure projection of appointment + slot context onto approval-queue
/// DTOs. The change-request row stores only <c>AppointmentId</c> and (optionally)
/// <c>NewDoctorAvailabilityId</c>, so the queue has to join the values the supervisor UI needs:
/// location + appointment type to drive the availability calendar staff pick the new date with,
/// and the proposed slot's date + start time so the queue can SHOW what was asked for rather
/// than a bare GUID.
///
/// <para>Split out from the AppService because that class is not unit-testable (10 constructor
/// dependencies plus ABP ambient services), matching the existing pure-helper precedent in this
/// folder: <see cref="ChangeRequestListFilter"/>, <see cref="ChangeRequestApprovalValidator"/>,
/// <c>RescheduleInPlacePolicy</c>. The caller owns the set-based queries; this owns the
/// per-row decisions.</para>
///
/// <para><c>internal static</c> for unit-testability via <c>InternalsVisibleTo</c>.</para>
/// </summary>
internal static class ChangeRequestQueueContext
{
    /// <summary>Appointment fields the queue needs, keyed by appointment id.</summary>
    internal readonly record struct AppointmentContext(Guid LocationId, Guid AppointmentTypeId);

    /// <summary>Slot fields the queue needs, keyed by doctor-availability id.</summary>
    internal readonly record struct SlotContext(DateTime AvailableDate, TimeOnly FromTime);

    /// <summary>
    /// Phase 4c (2026-08-05) -- the current consent round for one change request: the date staff
    /// confirmed, where each side stands on it, and how many times it has been sent.
    /// </summary>
    internal readonly record struct ConsentRoundContext(
        int RoundNumber,
        Guid ProposedDoctorAvailabilityId,
        ChangeRequestConsentStatus SideAStatus,
        ChangeRequestConsentStatus SideBStatus,
        int SendAttempts);

    /// <summary>
    /// Fills each DTO's appointment context, and its requested-slot context ONLY when that row
    /// actually proposed a slot. A row with no proposal keeps null date/time -- the normal case
    /// after 4b, and the signal the UI uses to say "no date requested" rather than showing a
    /// stale one. Rows whose lookups are missing are left untouched rather than zeroed, so a
    /// deleted appointment or slot degrades to "unknown" instead of a misleading value.
    ///
    /// <para>Phase 4c (2026-08-05) additionally projects the CURRENT consent round, keyed by
    /// change-request id. A row with no round keeps every current-round field null, which is what
    /// the approval modal reads as "staff still need to pick a date".</para>
    /// </summary>
    internal static void Apply(
        IEnumerable<AppointmentChangeRequestDto> dtos,
        IReadOnlyDictionary<Guid, AppointmentContext> appointmentsById,
        IReadOnlyDictionary<Guid, SlotContext> slotsById,
        IReadOnlyDictionary<Guid, ConsentRoundContext> roundsByChangeRequestId)
    {
        foreach (var dto in dtos)
        {
            if (appointmentsById.TryGetValue(dto.AppointmentId, out var appointment))
            {
                dto.AppointmentLocationId = appointment.LocationId;
                dto.AppointmentTypeId = appointment.AppointmentTypeId;
            }

            if (dto.NewDoctorAvailabilityId.HasValue &&
                slotsById.TryGetValue(dto.NewDoctorAvailabilityId.Value, out var slot))
            {
                dto.RequestedSlotDate = slot.AvailableDate;
                dto.RequestedSlotFromTime = FormatFromTime(slot.FromTime);
            }

            if (!roundsByChangeRequestId.TryGetValue(dto.Id, out var round))
            {
                continue;
            }

            dto.CurrentConsentRoundNumber = round.RoundNumber;
            dto.CurrentRoundProposedSlotId = round.ProposedDoctorAvailabilityId;
            dto.CurrentRoundSideAStatus = round.SideAStatus;
            dto.CurrentRoundSideBStatus = round.SideBStatus;
            dto.CurrentRoundSendAttempts = round.SendAttempts;

            // The round's slot is looked up from the same dictionary as the requested slot, so a
            // round pointing at a deleted slot degrades to "unknown date" rather than throwing.
            if (slotsById.TryGetValue(round.ProposedDoctorAvailabilityId, out var roundSlot))
            {
                dto.CurrentRoundProposedDate = roundSlot.AvailableDate;
                dto.CurrentRoundProposedFromTime = FormatFromTime(roundSlot.FromTime);
            }
        }
    }

    /// <summary>
    /// "HH:mm", matching <c>DoctorAvailabilityDto.fromTime</c> -- the availability calendar
    /// already uses that exact shape as its time-option value, so a picked time and a requested
    /// time are directly comparable in the UI.
    /// </summary>
    internal static string FormatFromTime(TimeOnly fromTime) =>
        fromTime.ToString("HH\\:mm", CultureInfo.InvariantCulture);
}
