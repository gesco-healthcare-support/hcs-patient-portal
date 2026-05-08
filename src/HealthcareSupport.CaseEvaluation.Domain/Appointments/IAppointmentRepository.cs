using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public interface IAppointmentRepository : IRepository<Appointment, Guid>
{
    Task<AppointmentWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);

    // S-NEW-2 (2026-04-30): when `visibleAppointmentIds` is non-null, the
    // result is intersected with that list. Used by the AppService to enforce
    // per-party visibility (Patient / AA / DA / CE see only the appointments
    // they are involved in). null = no narrowing (admin / Clinic Staff / etc.
    // see everything in their tenant).
    Task<List<AppointmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, Guid? identityUserId = null, Guid? accessorIdentityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, AppointmentStatusType? appointmentStatus = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, IReadOnlyCollection<Guid>? visibleAppointmentIds = null, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetListAsync(string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? panelNumber = null, DateTime? appointmentDateMin = null, DateTime? appointmentDateMax = null, Guid? identityUserId = null, Guid? accessorIdentityUserId = null, Guid? appointmentTypeId = null, Guid? locationId = null, AppointmentStatusType? appointmentStatus = null, IReadOnlyCollection<Guid>? visibleAppointmentIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 11g (2026-05-04) -- locates the most recent appointment in
    /// the calling tenant by exact <paramref name="requestConfirmationNumber"/>
    /// match. Returns null if no row matches. Used by the Re-Submit
    /// (<c>IsReRequestForm</c>) and Reval (<c>IsRevolutionForm</c>) flows
    /// to load the source appointment and validate its lifecycle status
    /// before creating a new one. Tenant filter is automatic via ABP's
    /// <c>IMultiTenant</c> data filter.
    /// </summary>
    Task<Appointment?> FindByConfirmationNumberAsync(
        string requestConfirmationNumber,
        CancellationToken cancellationToken = default);
}