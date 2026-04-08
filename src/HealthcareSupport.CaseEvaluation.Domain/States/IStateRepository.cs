using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.States;

public interface IStateRepository : IRepository<State, Guid>
{
    Task<List<State>> GetListAsync(string? filterText = null, string? name = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? name = null, CancellationToken cancellationToken = default);
}