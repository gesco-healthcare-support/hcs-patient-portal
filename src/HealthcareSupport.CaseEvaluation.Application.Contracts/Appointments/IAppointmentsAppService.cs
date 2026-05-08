using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public interface IAppointmentsAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentWithNavigationPropertiesDto>> GetListAsync(GetAppointmentsInput input);
    Task<AppointmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<AppointmentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetPatientLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetDoctorAvailabilityLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentDto> CreateAsync(AppointmentCreateDto input);
    Task<AppointmentDto> UpdateAsync(Guid id, AppointmentUpdateDto input);

    /// <summary>
    /// Gets applicant attorney details for appointment booking by identity user id or email.
    /// Used when Applicant Attorney is logged in (identityUserId) or when selecting another user by email.
    /// </summary>
    Task<ApplicantAttorneyDetailsDto?> GetApplicantAttorneyDetailsForBookingAsync(Guid? identityUserId = null, string? email = null);

    /// <summary>
    /// Gets the applicant attorney linked to an appointment, if any.
    /// </summary>
    Task<ApplicantAttorneyDetailsDto?> GetAppointmentApplicantAttorneyAsync(Guid appointmentId);

    /// <summary>
    /// Creates or updates applicant attorney and links to appointment.
    /// </summary>
    Task UpsertApplicantAttorneyForAppointmentAsync(Guid appointmentId, ApplicantAttorneyDetailsDto input);

    /// <summary>
    /// W2-7: gets defense attorney details for appointment booking by identity user id or email.
    /// Mirrors GetApplicantAttorneyDetailsForBookingAsync for the parallel defense-side feature.
    /// </summary>
    Task<DefenseAttorneyDetailsDto?> GetDefenseAttorneyDetailsForBookingAsync(Guid? identityUserId = null, string? email = null);

    /// <summary>
    /// W2-7: gets the defense attorney linked to an appointment, if any.
    /// </summary>
    Task<DefenseAttorneyDetailsDto?> GetAppointmentDefenseAttorneyAsync(Guid appointmentId);

    /// <summary>
    /// W2-7: creates or updates defense attorney and links to appointment.
    /// </summary>
    Task UpsertDefenseAttorneyForAppointmentAsync(Guid appointmentId, DefenseAttorneyDetailsDto input);

    /// <summary>Transition: Pending -> Approved.</summary>
    Task<AppointmentDto> ApproveAsync(Guid id);

    /// <summary>Transition: Pending -> Rejected.</summary>
    Task<AppointmentDto> RejectAsync(Guid id, RejectAppointmentInput input);

    /// <summary>
    /// Phase 11g (2026-05-04) -- Re-Submit (OLD <c>IsReRequestForm</c>).
    /// Looks up the source appointment by <paramref name="sourceConfirmationNumber"/>,
    /// validates it is in status <c>Rejected</c>, then creates a new
    /// appointment from <paramref name="input"/> reusing the source
    /// confirmation number. The new appointment lands at status
    /// <c>Pending</c> for external bookers (slot Available -> Reserved)
    /// or <c>Approved</c> for internal bookers (slot Available -> Booked)
    /// per the Phase 11h fast-path.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">
    /// With code <c>AppointmentReSubmitSourceNotRejected</c> when the
    /// source is in any status other than <c>Rejected</c>.
    /// </exception>
    Task<AppointmentDto> ReSubmitAsync(string sourceConfirmationNumber, AppointmentCreateDto input);

    /// <summary>
    /// Phase 13 (2026-05-04) -- look up an appointment by its
    /// user-facing <c>RequestConfirmationNumber</c>. Mirrors OLD's
    /// "enter confirmation # to view the request" UX path. Same
    /// access policy as <see cref="GetWithNavigationPropertiesAsync"/>:
    /// internal users see anything in their tenant; external users
    /// see only appointments where they are the creator or an
    /// accessor. Returns <c>null</c> when no row matches the given
    /// number; throws <c>BusinessException(AppointmentAccessDenied)</c>
    /// when a row exists but the caller cannot read it (so the
    /// existence of a confirmation # is not leaked to strangers).
    /// </summary>
    Task<AppointmentWithNavigationPropertiesDto?> GetByConfirmationNumberAsync(string requestConfirmationNumber);

    /// <summary>
    /// Phase 11g (2026-05-04) -- Reval (OLD <c>IsRevolutionForm</c>).
    /// Looks up the source appointment by <paramref name="sourceConfirmationNumber"/>,
    /// validates it is in status <c>Approved</c> (admin override surfaces
    /// the OLD-verbatim hint message but does NOT bypass the gate, per
    /// strict-parity directive), then creates a new appointment with a
    /// freshly generated confirmation number. Used for follow-up
    /// evaluations on a previously approved IME.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">
    /// With code <c>AppointmentRevalSourceNotApproved</c> for non-admin
    /// callers, <c>AppointmentRevalSourceNotApprovedAdminHint</c> for IT
    /// Admin callers, when the source is not in status <c>Approved</c>.
    /// </exception>
    Task<AppointmentDto> CreateRevalAsync(string sourceConfirmationNumber, AppointmentCreateDto input);

    /// <summary>
    /// Wave 4 / #6 (NEW-only enhancement, PARITY-FLAG-NEW-003): returns
    /// the count of appointments in the current tenant whose
    /// <c>AppointmentStatus</c> is <c>Pending</c> -- the work queue an
    /// admin / staff supervisor / clinic staff user must triage. Powers
    /// the Appointments sidebar count badge polled every 60s by the
    /// Angular `appointment-route.provider.ts`. ABP's automatic
    /// IMultiTenant filter scopes the count to the caller's tenant.
    /// Authorization is gated by <c>Appointments.Edit</c> so external
    /// roles (Patient / AA / DA / CE) get a 403 -- they have no triage
    /// queue to display. Returns <c>0</c> when no rows match (never
    /// throws on empty).
    /// </summary>
    Task<int> GetPendingCountAsync();
}