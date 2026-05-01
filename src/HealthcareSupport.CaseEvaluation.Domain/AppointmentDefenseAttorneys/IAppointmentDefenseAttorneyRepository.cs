using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public interface IAppointmentDefenseAttorneyRepository : IRepository<AppointmentDefenseAttorney, Guid>
{
    Task<AppointmentDefenseAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AppointmentDefenseAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? appointmentId = null, Guid? defenseAttorneyId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<AppointmentDefenseAttorney>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, Guid? appointmentId = null, Guid? defenseAttorneyId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);
}
