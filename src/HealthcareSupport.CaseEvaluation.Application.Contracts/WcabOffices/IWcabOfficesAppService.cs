using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using HealthcareSupport.CaseEvaluation.Shared;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public interface IWcabOfficesAppService : IApplicationService
{
    Task<PagedResultDto<WcabOfficeWithNavigationPropertiesDto>> GetListAsync(GetWcabOfficesInput input);
    Task<WcabOfficeWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<WcabOfficeDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<WcabOfficeDto> CreateAsync(WcabOfficeCreateDto input);
    Task<WcabOfficeDto> UpdateAsync(Guid id, WcabOfficeUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(WcabOfficeExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> wcabofficeIds);
    Task DeleteAllAsync(GetWcabOfficesInput input);
    Task<HealthcareSupport.CaseEvaluation.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}