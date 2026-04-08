using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public interface IAppointmentStatusesAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentStatusDto>> GetListAsync(GetAppointmentStatusesInput input);
    Task<AppointmentStatusDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<AppointmentStatusDto> CreateAsync(AppointmentStatusCreateDto input);
    Task<AppointmentStatusDto> UpdateAsync(Guid id, AppointmentStatusUpdateDto input);
    Task DeleteByIdsAsync(List<Guid> appointmentstatusIds);
    Task DeleteAllAsync(GetAppointmentStatusesInput input);
}