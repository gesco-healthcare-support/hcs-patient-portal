using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public interface IAppointmentTypeRepository : IRepository<AppointmentType, Guid>
{
    Task<List<AppointmentType>> GetListAsync(string? filterText = null, string? name = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? name = null, CancellationToken cancellationToken = default);
}