using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 15 (2026-05-04) -- read DTO for the cancel / reschedule
/// change request. Phase 16 reuses the same DTO for the reschedule
/// submit endpoint. Phase 17 (Session B) reuses for the supervisor
/// approve / reject endpoints.
/// </summary>
public class AppointmentChangeRequestDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Human-facing appointment confirmation number (e.g. "A00077") copied from
    /// the referenced appointment so the supervisor approval queues can display
    /// it instead of the raw appointment GUID. Read-only on this DTO; populated
    /// in <c>GetPendingChangeRequestsAsync</c>, not by the Mapperly mapper.
    /// </summary>
    public string? AppointmentConfirmationNumber { get; set; }

    /// <summary>
    /// Phase 4b (2026-08-04) -- the referenced appointment's location and appointment type, so
    /// the approval queue can drive the availability calendar staff now pick the new date with.
    /// The change-request entity stores only <see cref="AppointmentId"/>, so both are filled
    /// set-based in <c>GetPendingChangeRequestsAsync</c>, NOT by the Mapperly mapper.
    ///
    /// <para>POPULATED BY THE QUEUE QUERY ONLY. Other endpoints returning this DTO (notably
    /// <c>GetActiveForAppointmentAsync</c> and the submit/approve responses) leave these null.</para>
    /// </summary>
    public Guid? AppointmentLocationId { get; set; }

    /// <inheritdoc cref="AppointmentLocationId"/>
    public Guid? AppointmentTypeId { get; set; }

    /// <summary>
    /// Phase 4b (2026-08-04) -- date and start time of the slot proposed at SUBMIT time, resolved
    /// from <see cref="NewDoctorAvailabilityId"/> so the queue can show what was asked for instead
    /// of a bare GUID. Null when nothing was proposed, which after 4b is the normal external case.
    /// Same population caveat as <see cref="AppointmentLocationId"/>.
    ///
    /// <para>A CALENDAR DATE, so exempt from normalization: without the attribute ABP's API
    /// serializer stamps it UTC midnight and a Pacific browser shows the day before (found
    /// 2026-10-14; <c>ChangeRequestSlotDateSerializationTests</c>).</para>
    /// </summary>
    [DisableDateTimeNormalization]
    public DateTime? RequestedSlotDate { get; set; }

    /// <inheritdoc cref="RequestedSlotDate"/>
    public string? RequestedSlotFromTime { get; set; }

    /// <summary>
    /// Phase 4c (2026-08-05) -- the CURRENT consent round: which date staff confirmed, where each
    /// side stands on it, and how many times it has been sent. This is what drives the approval
    /// modal's three stages (needs-date / awaiting-consent / granted). Null when staff have not
    /// confirmed a date yet, which is how a freshly submitted reschedule arrives.
    ///
    /// <para>Distinct from <see cref="RequestedSlotDate"/>, which is what the REQUESTOR proposed
    /// at submit. Same population caveat as <see cref="AppointmentLocationId"/>: the queue query
    /// fills these, the Mapperly mapper does not.</para>
    /// </summary>
    public int? CurrentConsentRoundNumber { get; set; }

    /// <inheritdoc cref="CurrentConsentRoundNumber"/>
    public Guid? CurrentRoundProposedSlotId { get; set; }

    /// <inheritdoc cref="CurrentConsentRoundNumber"/>
    /// <remarks>A calendar date; exempt for the reason on <see cref="RequestedSlotDate"/>.</remarks>
    [DisableDateTimeNormalization]
    public DateTime? CurrentRoundProposedDate { get; set; }

    /// <inheritdoc cref="CurrentConsentRoundNumber"/>
    public string? CurrentRoundProposedFromTime { get; set; }

    /// <inheritdoc cref="CurrentConsentRoundNumber"/>
    public ChangeRequestConsentStatus? CurrentRoundSideAStatus { get; set; }

    /// <inheritdoc cref="CurrentConsentRoundNumber"/>
    public ChangeRequestConsentStatus? CurrentRoundSideBStatus { get; set; }

    /// <inheritdoc cref="CurrentConsentRoundNumber"/>
    public int? CurrentRoundSendAttempts { get; set; }

    /// <summary>
    /// 2026-10-14 -- the billing outcome staff chose when they set the date (RescheduledNoBill or
    /// RescheduledLate); the reschedule completes with it once both sides agree. Null on a round
    /// opened before outcomes were chosen at Set date. The approve modal shows it read-only and
    /// sends it back on the manual fallback, so staff are not asked twice. Same population caveat
    /// as <see cref="AppointmentLocationId"/>.
    /// </summary>
    public AppointmentStatusType? CurrentRoundProposedOutcome { get; set; }

    public ChangeRequestType ChangeRequestType { get; set; }

    public string? CancellationReason { get; set; }

    public string? ReScheduleReason { get; set; }

    public Guid? NewDoctorAvailabilityId { get; set; }

    public RequestStatusType RequestStatus { get; set; }

    public string? RejectionNotes { get; set; }

    public Guid? RejectedById { get; set; }

    public Guid? ApprovedById { get; set; }

    public string? AdminReScheduleReason { get; set; }

    public Guid? AdminOverrideSlotId { get; set; }

    public bool IsBeyondLimit { get; set; }

    public AppointmentStatusType? CancellationOutcome { get; set; }

    /// <summary>
    /// Two-sided consent state (2026-07-01). Side A = Patient/Applicant Attorney; Side B =
    /// Defense Attorney/Claim Examiner. Per side: Pending = awaiting that side's consent;
    /// Approved = granted; Rejected/Expired = declined (needs staff mediation); NotRequired =
    /// not solicited (gating off, no rep, or the requestor's own side once auto-granted).
    /// The finalize gate passes when every non-NotRequired side is Approved.
    /// </summary>
    public ChangeRequestConsentStatus SideAConsentStatus { get; set; }

    public ChangeRequestConsentStatus SideBConsentStatus { get; set; }

    /// <summary>Which side submitted (party-initiated); null when staff initiated.</summary>
    public ChangeRequestSide? RequestingSide { get; set; }
}
