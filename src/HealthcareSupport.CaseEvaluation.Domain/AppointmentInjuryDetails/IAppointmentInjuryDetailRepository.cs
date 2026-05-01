using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public interface IAppointmentInjuryDetailRepository : IRepository<AppointmentInjuryDetail, Guid>
{
    Task<AppointmentInjuryDetailWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AppointmentInjuryDetailWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? appointmentId = null, string? claimNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<AppointmentInjuryDetail>> GetListAsync(string? filterText = null, Guid? appointmentId = null, string? claimNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, Guid? appointmentId = null, string? claimNumber = null, CancellationToken cancellationToken = default);
}
