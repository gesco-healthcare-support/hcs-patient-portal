using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public interface IDoctorRepository : IRepository<Doctor, Guid>
{
    Task<DoctorWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DoctorWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? email = null, Guid? identityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<Doctor>> GetListAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? email = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? email = null, Guid? identityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, CancellationToken cancellationToken = default);
}