using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Patients;

public interface IPatientsAppService : IApplicationService
{
    Task<PagedResultDto<PatientWithNavigationPropertiesDto>> GetListAsync(GetPatientsInput input);
    Task<PatientWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<PatientWithNavigationPropertiesDto> GetPatientForAppointmentBookingAsync(Guid id);
    Task<PatientWithNavigationPropertiesDto?> GetPatientByEmailForAppointmentBookingAsync(string email);
    Task<PatientWithNavigationPropertiesDto> GetOrCreatePatientForAppointmentBookingAsync(CreatePatientForAppointmentBookingInput input);
    Task<PatientDto> UpdatePatientForAppointmentBookingAsync(Guid id, PatientUpdateDto input);
    Task<PatientWithNavigationPropertiesDto> GetMyProfileAsync();
    Task<PatientDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLanguageLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<PatientDto> CreateAsync(PatientCreateDto input);
    Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto input);
    Task<PatientDto> UpdateMyProfileAsync(PatientUpdateDto input);
}