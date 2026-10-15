using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public interface IDefenseAttorneyRepository : IRepository<DefenseAttorney, Guid>
{
    Task<DefenseAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);

    // R2-2: email-authoritative matching -- find an existing master by email
    // (per-tenant via the multi-tenant filter) so booking reuses it instead of
    // creating a duplicate account for the same email.
    Task<DefenseAttorney?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<List<DefenseAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DefenseAttorney>> GetListAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);
}
