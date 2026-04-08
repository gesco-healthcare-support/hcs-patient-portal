using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public interface IAppointmentStatusRepository : IRepository<AppointmentStatus, Guid>
{
    Task DeleteAllAsync(string? filterText = null, CancellationToken cancellationToken = default);
    Task<List<AppointmentStatus>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, CancellationToken cancellationToken = default);
}