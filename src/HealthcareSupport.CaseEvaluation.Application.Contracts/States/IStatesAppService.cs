using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.States;

public interface IStatesAppService : IApplicationService
{
    Task<PagedResultDto<StateDto>> GetListAsync(GetStatesInput input);
    Task<StateDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<StateDto> CreateAsync(StateCreateDto input);
    Task<StateDto> UpdateAsync(Guid id, StateUpdateDto input);
}