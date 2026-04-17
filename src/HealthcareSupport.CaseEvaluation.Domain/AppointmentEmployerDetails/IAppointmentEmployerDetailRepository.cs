using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public interface IAppointmentEmployerDetailRepository : IRepository<AppointmentEmployerDetail, Guid>
{
    Task<AppointmentEmployerDetailWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AppointmentEmployerDetailWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, Guid? appointmentId = null, Guid? stateId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<AppointmentEmployerDetail>> GetListAsync(string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, Guid? appointmentId = null, Guid? stateId = null, CancellationToken cancellationToken = default);
}