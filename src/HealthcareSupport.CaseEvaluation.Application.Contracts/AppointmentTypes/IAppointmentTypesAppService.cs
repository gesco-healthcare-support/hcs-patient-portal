using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public interface IAppointmentTypesAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentTypeDto>> GetListAsync(GetAppointmentTypesInput input);
    Task<AppointmentTypeDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<AppointmentTypeDto> CreateAsync(AppointmentTypeCreateDto input);
    Task<AppointmentTypeDto> UpdateAsync(Guid id, AppointmentTypeUpdateDto input);
}