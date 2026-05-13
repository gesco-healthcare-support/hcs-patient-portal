using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.WcabOffices.Default)]
public class WcabOfficesAppService : CaseEvaluationAppService, IWcabOfficesAppService
{
    protected IDistributedCache<WcabOfficeDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IWcabOfficeRepository _wcabOfficeRepository;
    protected WcabOfficeManager _wcabOfficeManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;

    public WcabOfficesAppService(IWcabOfficeRepository wcabOfficeRepository, WcabOfficeManager wcabOfficeManager, IDistributedCache<WcabOfficeDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _wcabOfficeRepository = wcabOfficeRepository;
        _wcabOfficeManager = wcabOfficeManager;
        _stateRepository = stateRepository;
    }

    public virtual async Task<PagedResultDto<WcabOfficeWithNavigationPropertiesDto>> GetListAsync(GetWcabOfficesInput input)
    {
        var totalCount = await _wcabOfficeRepository.GetCountAsync(input.FilterText, input.Name, input.Abbreviation, input.Address, input.City, input.ZipCode, input.IsActive, input.StateId);
        var items = await _wcabOfficeRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.Abbreviation, input.Address, input.City, input.ZipCode, input.IsActive, input.StateId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<WcabOfficeWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<WcabOfficeWithNavigationProperties>, List<WcabOfficeWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<WcabOfficeWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<WcabOfficeWithNavigationProperties, WcabOfficeWithNavigationPropertiesDto>((await _wcabOfficeRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    public virtual async Task<WcabOfficeDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<WcabOffice, WcabOfficeDto>(await _wcabOfficeRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.WcabOffices.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _wcabOfficeRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.WcabOffices.Create)]
    public virtual async Task<WcabOfficeDto> CreateAsync(WcabOfficeCreateDto input)
    {
        var wcabOffice = await _wcabOfficeManager.CreateAsync(input.StateId, input.Name, input.Abbreviation, input.IsActive, input.Address, input.City, input.ZipCode);
        return ObjectMapper.Map<WcabOffice, WcabOfficeDto>(wcabOffice);
    }

    [Authorize(CaseEvaluationPermissions.WcabOffices.Edit)]
    public virtual async Task<WcabOfficeDto> UpdateAsync(Guid id, WcabOfficeUpdateDto input)
    {
        var wcabOffice = await _wcabOfficeManager.UpdateAsync(id, input.StateId, input.Name, input.Abbreviation, input.IsActive, input.Address, input.City, input.ZipCode, input.ConcurrencyStamp);
        return ObjectMapper.Map<WcabOffice, WcabOfficeDto>(wcabOffice);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(WcabOfficeExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var wcabOffices = await _wcabOfficeRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Name, input.Abbreviation, input.Address, input.City, input.ZipCode, input.IsActive, input.StateId);
        var items = wcabOffices.Select(item => new { Name = item.WcabOffice.Name, Abbreviation = item.WcabOffice.Abbreviation, Address = item.WcabOffice.Address, City = item.WcabOffice.City, ZipCode = item.WcabOffice.ZipCode, IsActive = item.WcabOffice.IsActive, State = item.State?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "WcabOffices.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(CaseEvaluationPermissions.WcabOffices.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> wcabofficeIds)
    {
        await _wcabOfficeRepository.DeleteManyAsync(wcabofficeIds);
    }

    [Authorize(CaseEvaluationPermissions.WcabOffices.Delete)]
    public virtual async Task DeleteAllAsync(GetWcabOfficesInput input)
    {
        await _wcabOfficeRepository.DeleteAllAsync(input.FilterText, input.Name, input.Abbreviation, input.Address, input.City, input.ZipCode, input.IsActive, input.StateId);
    }

    public virtual async Task<HealthcareSupport.CaseEvaluation.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new WcabOfficeDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HealthcareSupport.CaseEvaluation.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}