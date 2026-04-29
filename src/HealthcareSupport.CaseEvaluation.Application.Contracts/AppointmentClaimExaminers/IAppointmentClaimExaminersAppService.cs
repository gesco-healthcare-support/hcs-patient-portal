using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public interface IAppointmentClaimExaminersAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentClaimExaminerDto>> GetListAsync(GetAppointmentClaimExaminersInput input);
    Task<AppointmentClaimExaminerDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<AppointmentClaimExaminerDto> CreateAsync(AppointmentClaimExaminerCreateDto input);
    Task<AppointmentClaimExaminerDto> UpdateAsync(Guid id, AppointmentClaimExaminerUpdateDto input);
}
