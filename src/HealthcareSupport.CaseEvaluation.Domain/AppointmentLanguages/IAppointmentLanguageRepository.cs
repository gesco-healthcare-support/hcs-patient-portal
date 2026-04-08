using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public interface IAppointmentLanguageRepository : IRepository<AppointmentLanguage, Guid>
{
    Task<List<AppointmentLanguage>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, CancellationToken cancellationToken = default);
}