using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Send Back / Request-more-information flow. Staff send an appointment back to
/// the external user with flagged fields + a note (Pending -&gt; InfoRequested);
/// the external user resubmits their corrections (InfoRequested -&gt; Pending).
/// </summary>
public interface IAppointmentInfoRequestsAppService : IApplicationService
{
    /// <summary>Staff-only. Open an info request + move the appointment to InfoRequested.</summary>
    Task<AppointmentInfoRequestDto> SendBackAsync(Guid appointmentId, SendBackAppointmentInput input);

    /// <summary>
    /// External party. Apply the requester's corrections to the flagged fields only
    /// (server-side locked to the open request's flagged set). Does NOT change status;
    /// the requester resubmits separately via <see cref="ResubmitAsync"/>.
    /// </summary>
    Task SaveCorrectionsAsync(Guid appointmentId, SaveInfoRequestCorrectionsInput input);

    /// <summary>External party. Resolve the open request + move the appointment back to Pending.</summary>
    Task ResubmitAsync(Guid appointmentId);

    /// <summary>
    /// External party. The appointment's current Claim Information (injury-detail) rows, so
    /// the fix-it page can prefill its editor (QA item 11, 2026-07-01). Gated by the
    /// read-access guard rather than the injury-details CRUD permission, which external
    /// roles lack; returns an empty list when none exist.
    /// </summary>
    Task<List<InjuryDetailCorrectionDto>> GetInjuryDetailsForCorrectionAsync(Guid appointmentId);

    /// <summary>The open (unresolved) info request for the appointment, or null when none is open.</summary>
    Task<AppointmentInfoRequestDto?> GetOpenAsync(Guid appointmentId);

    /// <summary>
    /// The full Send Back history for the appointment (newest-first), each round with
    /// its note, requester/resubmitter names, fixed/flagged counts, and a per-field
    /// old-&gt;new diff. Staff review surface; gated by the read-access guard.
    /// </summary>
    Task<List<AppointmentInfoRequestRoundDto>> GetHistoryAsync(Guid appointmentId);
}
