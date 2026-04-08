using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Locations;

public interface ILocationRepository : IRepository<Location, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default);
    Task<LocationWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<LocationWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<Location>> GetListAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default);
}