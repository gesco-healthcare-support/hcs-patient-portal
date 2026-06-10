using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public interface IAppointmentDocumentTypeRepository : IRepository<AppointmentDocumentType, Guid>
{
    Task DeleteAllAsync(string? filterText = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default);

    Task<List<AppointmentDocumentType>> GetListAsync(
        string? filterText = null,
        Guid? appointmentTypeId = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(string? filterText = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default);

    /// <summary>True when another active row already uses <paramref name="name"/>
    /// for the same appointment type (case-insensitive), excluding
    /// <paramref name="excludeId"/>. Enforces name uniqueness per type.</summary>
    Task<bool> NameExistsAsync(string name, Guid? appointmentTypeId, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
