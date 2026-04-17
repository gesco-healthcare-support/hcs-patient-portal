using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public interface IAppointmentAccessorRepository : IRepository<AppointmentAccessor, Guid>
{
    Task<AppointmentAccessorWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AppointmentAccessorWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, AccessType? accessTypeId = null, Guid? identityUserId = null, Guid? appointmentId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<AppointmentAccessor>> GetListAsync(string? filterText = null, AccessType? accessTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, AccessType? accessTypeId = null, Guid? identityUserId = null, Guid? appointmentId = null, CancellationToken cancellationToken = default);
}