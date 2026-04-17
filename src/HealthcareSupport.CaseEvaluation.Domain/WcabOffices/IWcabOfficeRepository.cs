using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public interface IWcabOfficeRepository : IRepository<WcabOffice, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null, CancellationToken cancellationToken = default);
    Task<WcabOfficeWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<WcabOfficeWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<WcabOffice>> GetListAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null, CancellationToken cancellationToken = default);
}