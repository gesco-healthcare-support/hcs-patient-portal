using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.States;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Resolves <c>data.employer</c> for one appointment, or null when it has no employer row.
///
/// <para><b>Which row.</b> The table has no unique index, and more than one live row per appointment is reachable
/// (Create never checks; the edit screens POST when their load returns nothing; the reschedule copier copies every
/// row). So the choice must be deterministic: the NEWEST live row, by <c>CreationTime</c> then <c>Id</c>, both
/// descending. That is the row the edit screens load and save, so an edit always changes what the Case Tracker
/// receives. Soft-deleted rows are filtered out by the framework, so after a delete the next-newest row (or none)
/// is sent. The rows are ordered in memory: an appointment has one in practice, so there is nothing to page.</para>
/// </summary>
public class EmployerResolver : ITransientDependency
{
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerRepository;
    private readonly IRepository<State, Guid> _stateRepository;

    public EmployerResolver(
        IRepository<AppointmentEmployerDetail, Guid> employerRepository,
        IRepository<State, Guid> stateRepository)
    {
        _employerRepository = employerRepository;
        _stateRepository = stateRepository;
    }

    public virtual async Task<IntakeEmployerSection?> ResolveAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _employerRepository.GetListAsync(
            e => e.AppointmentId == appointmentId, cancellationToken: cancellationToken);
        var row = rows
            .OrderByDescending(e => e.CreationTime)
            .ThenByDescending(e => e.Id)
            .FirstOrDefault();
        if (row == null)
        {
            return null;
        }

        string? stateName = null;
        if (row.StateId is { } stateId)
        {
            stateName = (await _stateRepository.FindAsync(stateId, cancellationToken: cancellationToken))?.Name;
        }

        return IntakeEmployerSection.From(row, stateName);
    }
}
