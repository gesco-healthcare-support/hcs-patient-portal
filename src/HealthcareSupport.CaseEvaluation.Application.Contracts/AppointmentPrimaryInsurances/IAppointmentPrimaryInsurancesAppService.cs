using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public interface IAppointmentPrimaryInsurancesAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentPrimaryInsuranceDto>> GetListAsync(GetAppointmentPrimaryInsurancesInput input);
    Task<AppointmentPrimaryInsuranceDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentPrimaryInsuranceDto> CreateAsync(AppointmentPrimaryInsuranceCreateDto input);
    Task<AppointmentPrimaryInsuranceDto> UpdateAsync(Guid id, AppointmentPrimaryInsuranceUpdateDto input);
}
