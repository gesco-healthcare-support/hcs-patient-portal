using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public interface IClaimExaminerRepository : IRepository<ClaimExaminer, Guid>
{
    Task<ClaimExaminerWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<ClaimExaminerWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);

    Task<List<ClaimExaminer>> GetListAsync(string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);
}
