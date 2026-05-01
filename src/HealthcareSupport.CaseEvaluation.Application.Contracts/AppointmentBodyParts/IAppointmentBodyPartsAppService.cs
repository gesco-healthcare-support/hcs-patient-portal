using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

public interface IAppointmentBodyPartsAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentBodyPartDto>> GetListAsync(GetAppointmentBodyPartsInput input);
    Task<AppointmentBodyPartDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<AppointmentBodyPartDto> CreateAsync(AppointmentBodyPartCreateDto input);
    Task<AppointmentBodyPartDto> UpdateAsync(Guid id, AppointmentBodyPartUpdateDto input);
}
