using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Domain service for the cancel / reschedule lifecycle. Phase 1.5
/// (2026-05-01) ships the entity + repository scaffolding only; the
/// actual <c>SubmitCancellationAsync</c>, <c>SubmitRescheduleAsync</c>,
/// <c>ApproveCancellationAsync</c>, <c>RejectCancellationAsync</c>,
/// <c>ApproveRescheduleAsync</c>, and <c>RejectRescheduleAsync</c>
/// implementations land in Phases 15 (cancel submit), 16 (reschedule
/// submit), and 17 (supervisor approve / reject) per the master plan.
///
/// Until those phases land, this manager exists as a registration anchor
/// so dependent services can resolve it via DI without forward-referencing
/// a missing type.
/// </summary>
public class AppointmentChangeRequestManager : DomainService
{
    private readonly IAppointmentChangeRequestRepository _repository;

    public AppointmentChangeRequestManager(IAppointmentChangeRequestRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Loads a change request by id. Throws if not found.
    /// </summary>
    public virtual Task<AppointmentChangeRequest> GetAsync(Guid id) => _repository.GetAsync(id);
}
