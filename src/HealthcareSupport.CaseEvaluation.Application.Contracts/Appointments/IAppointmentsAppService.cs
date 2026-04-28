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

    /// <summary>W1-1 transition: Pending|AwaitingMoreInfo -> Approved.</summary>
    Task<AppointmentDto> ApproveAsync(Guid id);

    /// <summary>W1-1 transition: Pending|AwaitingMoreInfo -> Rejected.</summary>
    Task<AppointmentDto> RejectAsync(Guid id, RejectAppointmentInput input);

    /// <summary>W1-1 transition: Pending -> AwaitingMoreInfo. Captures office flagged fields + note.</summary>
    Task<AppointmentDto> SendBackAsync(Guid id, SendBackAppointmentInput input);

    /// <summary>W1-1 auto-transition: AwaitingMoreInfo -> Pending. Fires when the booker re-submits the booking form with edits.</summary>
    Task<AppointmentDto> SaveAndResubmitAsync(Guid id);
}