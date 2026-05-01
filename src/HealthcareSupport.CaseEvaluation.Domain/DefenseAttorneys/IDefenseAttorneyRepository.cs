using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public interface IDefenseAttorneyRepository : IRepository<DefenseAttorney, Guid>
{
    Task<DefenseAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DefenseAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DefenseAttorney>> GetListAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);
}
