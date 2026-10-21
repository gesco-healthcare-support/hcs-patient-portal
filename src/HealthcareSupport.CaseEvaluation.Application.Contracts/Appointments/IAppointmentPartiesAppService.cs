using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Staff change the parties on an existing appointment, at any status (2026-10-21): the applicant
/// attorney, the defense attorney, the claim examiner and the patient's email. Staff only.
/// </summary>
public interface IAppointmentPartiesAppService : IApplicationService
{
    /// <summary>
    /// Applies <paramref name="input"/> and returns the appointment as saved, so the caller holds
    /// the new concurrency stamp and party-email columns.
    /// </summary>
    Task<AppointmentDto> UpdateAsync(Guid appointmentId, AppointmentPartiesUpdateDto input);
}
