using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>
/// Writes the PHI access log. Call it once the caller is authorised and the record exists, and
/// before the data is returned.
///
/// <para>FAILS CLOSED. The rows are committed in their own unit of work, and if that write fails
/// the exception propagates, so the caller gets an error instead of the data. A read that cannot
/// be recorded does not happen. The opposite choice, logging and carrying on, would make the log
/// silently incomplete exactly when the database is struggling, and a log that may be missing
/// rows cannot show that someone did NOT see a record. Recording before the data leaves means a
/// later failure (a file missing from storage, say) leaves an extra row, never a missing one.</para>
///
/// <para>Its own unit of work, rather than the caller's, so the row is committed before the data
/// is handed back and never rides on a transaction that might roll back after the response has
/// gone.</para>
/// </summary>
public class PhiAccessRecorder : DomainService
{
    private readonly IRepository<PhiAccessLogEntry, Guid> _repository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public PhiAccessRecorder(
        IRepository<PhiAccessLogEntry, Guid> repository,
        IRepository<Appointment, Guid> appointmentRepository,
        ICurrentUser currentUser,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _repository = repository;
        _appointmentRepository = appointmentRepository;
        _currentUser = currentUser;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public virtual Task RecordAsync(PhiAccessAction action, PhiAccessTarget target)
    {
        return RecordManyAsync(action, new[] { target });
    }

    /// <summary>
    /// One row per target, all with the same time, so the rows of one export read as one event.
    /// Nothing is written for an empty list: an export with no rows disclosed nothing. A target
    /// naming an appointment but no patient gets the appointment's patient filled in, so "who saw
    /// this patient" is one indexed query whatever was read.
    /// </summary>
    public virtual async Task RecordManyAsync(PhiAccessAction action, IReadOnlyCollection<PhiAccessTarget> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        var patientByAppointment = await FindMissingPatientIdsAsync(targets);
        var accessedAt = Clock.Now;
        var entries = targets
            .Select(target => new PhiAccessLogEntry(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                _currentUser.Id,
                _currentUser.FindImpersonatorUserId(),
                action,
                WithPatient(target, patientByAppointment),
                accessedAt))
            .ToList();

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await _repository.InsertManyAsync(entries);
        await uow.CompleteAsync();
    }

    private async Task<Dictionary<Guid, Guid>> FindMissingPatientIdsAsync(IReadOnlyCollection<PhiAccessTarget> targets)
    {
        var appointmentIds = targets
            .Where(t => t.PatientId == null && t.AppointmentId != null)
            .Select(t => t.AppointmentId!.Value)
            .Distinct()
            .ToList();
        if (appointmentIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var queryable = await _appointmentRepository.GetQueryableAsync();
        var pairs = await AsyncExecuter.ToListAsync(queryable
            .Where(a => appointmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.PatientId }));
        return pairs.ToDictionary(p => p.Id, p => p.PatientId);
    }

    private static PhiAccessTarget WithPatient(PhiAccessTarget target, Dictionary<Guid, Guid> patientByAppointment)
    {
        if (target.PatientId != null || target.AppointmentId == null)
        {
            return target;
        }

        return patientByAppointment.TryGetValue(target.AppointmentId.Value, out var patientId)
            ? target with { PatientId = patientId }
            : target;
    }
}
