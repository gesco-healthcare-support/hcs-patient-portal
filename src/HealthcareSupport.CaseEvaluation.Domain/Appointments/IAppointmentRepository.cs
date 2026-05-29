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

    /// <summary>
    /// 2026-05-15 -- counts the appointments tied to the given slot that
    /// are NOT in a slot-freed terminal status. The five freed statuses
    /// excluded are: <c>Rejected</c>, <c>CancelledNoBill</c>,
    /// <c>CancelledLate</c>, <c>RescheduledNoBill</c>,
    /// <c>RescheduledLate</c>. Caller uses this count against the slot's
    /// <c>Capacity</c> to determine whether the slot is bookable.
    /// </summary>
    Task<long> GetActiveCountForSlotAsync(
        Guid doctorAvailabilityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 2026-05-15 -- bulk variant of <see cref="GetActiveCountForSlotAsync"/>.
    /// Used by the booking-form lookup endpoint to compute remaining
    /// capacity for a paged set of slots in a single round-trip. Returns
    /// a dictionary keyed by slot id; slots with zero active appointments
    /// are absent from the result (caller treats missing as 0).
    /// </summary>
    Task<Dictionary<Guid, long>> GetActiveCountsForSlotsAsync(
        List<Guid> doctorAvailabilityIds,
        CancellationToken cancellationToken = default);
}