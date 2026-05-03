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
}