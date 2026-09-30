using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public interface IAppointmentDocumentTypesAppService : IApplicationService
{
    Task<PagedResultDto<AppointmentDocumentTypeDto>> GetListAsync(GetAppointmentDocumentTypesInput input);
    Task<AppointmentDocumentTypeDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<AppointmentDocumentTypeDto> CreateAsync(AppointmentDocumentTypeCreateDto input);
    Task<AppointmentDocumentTypeDto> UpdateAsync(Guid id, AppointmentDocumentTypeUpdateDto input);
    Task DeleteByIdsAsync(List<Guid> appointmentDocumentTypeIds);
    Task DeleteAllAsync(GetAppointmentDocumentTypesInput input);
}
