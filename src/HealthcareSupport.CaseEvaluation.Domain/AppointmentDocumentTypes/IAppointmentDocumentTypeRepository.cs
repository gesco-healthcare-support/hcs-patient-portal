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

    /// <summary>Loads a single category WITH its appointment-type join set so a
    /// reconcile (#4) compares against the current set.</summary>
    Task<AppointmentDocumentType> GetWithAppointmentTypesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when another ACTIVE row already uses <paramref name="name"/>
    /// in the current tenant (case-insensitive), excluding
    /// <paramref name="excludeId"/>. Uniqueness is now per-tenant (#4): a name
    /// is curated once and offered to many appointment types.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
