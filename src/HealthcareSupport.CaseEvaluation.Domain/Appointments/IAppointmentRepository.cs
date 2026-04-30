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
}