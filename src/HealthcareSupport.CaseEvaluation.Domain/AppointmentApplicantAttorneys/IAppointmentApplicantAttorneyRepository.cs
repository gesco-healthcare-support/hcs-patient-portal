using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public interface IAppointmentApplicantAttorneyRepository : IRepository<AppointmentApplicantAttorney, Guid>
{
    Task<AppointmentApplicantAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AppointmentApplicantAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? appointmentId = null, Guid? applicantAttorneyId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<AppointmentApplicantAttorney>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, Guid? appointmentId = null, Guid? applicantAttorneyId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);
}