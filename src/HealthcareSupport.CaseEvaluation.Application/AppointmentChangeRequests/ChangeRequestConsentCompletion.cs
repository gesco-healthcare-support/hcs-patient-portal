using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>Outcome of <see cref="ChangeRequestConsentCompletion.TryCompleteAsync"/>.</summary>
public enum ChangeRequestCompletionResult
{
    /// <summary>The request was decided and the appointment changed.</summary>
    Completed = 0,

    /// <summary>
    /// Nothing to do: not Pending, no proposed outcome (legacy round / party-requested
    /// cancellation), or a solicited side has not agreed. Staff approve it by hand.
    /// </summary>
    NotApplicable = 1,

    /// <summary>Completion was attempted and failed; logged, nothing changed, staff approve by hand.</summary>
    Failed = 2,
}

/// <summary>
/// Completes a change request ON ITS OWN once every solicited side has agreed (2026-10-09), so staff
/// no longer come back to click Approve.
///
/// <list type="bullet">
/// <item>Reschedule: the CURRENT consent round is fully granted AND carries the outcome staff chose
/// when they set the date. Acting user = the staff member who set the date.</item>
/// <item>Staff-filed cancellation: every side is granted AND the request carries the outcome staff
/// chose when they filed it. Acting user = whoever filed it. A party-requested cancellation never has
/// a proposed outcome, so it never completes here -- staff still pick the outcome at Approve.</item>
/// </list>
///
/// <para>Completion runs in its OWN unit of work and swallows (but logs) any failure, so a failure
/// to complete can never roll back or fail the consent a party just recorded; the request simply
/// stays Pending and granted and the existing staff Approve path finishes it. Callable from the
/// anonymous public consent page (no <c>CurrentUser</c>) and from the in-portal consent feature.</para>
/// </summary>
public class ChangeRequestConsentCompletion : ITransientDependency
{
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly IChangeRequestConsentRoundRepository _consentRoundRepository;
    private readonly RescheduleFinalizer _rescheduleFinalizer;
    private readonly CancellationFinalizer _cancellationFinalizer;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ChangeRequestConsentCompletion> _logger;

    public ChangeRequestConsentCompletion(
        IAppointmentChangeRequestRepository changeRequestRepository,
        IChangeRequestConsentRoundRepository consentRoundRepository,
        RescheduleFinalizer rescheduleFinalizer,
        CancellationFinalizer cancellationFinalizer,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        ILogger<ChangeRequestConsentCompletion> logger)
    {
        _changeRequestRepository = changeRequestRepository;
        _consentRoundRepository = consentRoundRepository;
        _rescheduleFinalizer = rescheduleFinalizer;
        _cancellationFinalizer = cancellationFinalizer;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>
    /// Schedules <see cref="TryCompleteAsync"/> for AFTER the current unit of work commits, so the
    /// consent decision (or the round / request the caller just inserted) is visible to the
    /// completion's own unit of work. With no ambient unit of work it runs immediately.
    /// </summary>
    public virtual async Task RequestCompletionAsync(Guid changeRequestId)
    {
        var current = _unitOfWorkManager.Current;
        if (current == null)
        {
            await TryCompleteAsync(changeRequestId);
            return;
        }

        // Captured now: the callback runs when the unit of work completes, which can be after the
        // caller's tenant scope has ended, and the request lives in that office's database.
        var tenantId = _currentTenant.Id;
        current.OnCompleted(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await TryCompleteAsync(changeRequestId);
            }
        });
    }

    /// <summary>
    /// Completes the request when the decision table allows it, in its own unit of work. Never
    /// throws: a failure is logged and reported as <see cref="ChangeRequestCompletionResult.Failed"/>.
    /// </summary>
    public virtual async Task<ChangeRequestCompletionResult> TryCompleteAsync(Guid changeRequestId)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var result = await CompleteCoreAsync(changeRequestId);
            await uow.CompleteAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ChangeRequest {ChangeRequestId}: completing on consent failed; staff can still approve it.",
                changeRequestId);
            return ChangeRequestCompletionResult.Failed;
        }
    }

    private async Task<ChangeRequestCompletionResult> CompleteCoreAsync(Guid changeRequestId)
    {
        var changeRequest = await _changeRequestRepository.FindAsync(changeRequestId);
        if (changeRequest == null || changeRequest.RequestStatus != RequestStatusType.Pending)
        {
            return ChangeRequestCompletionResult.NotApplicable;
        }

        if (changeRequest.ChangeRequestType == ChangeRequestType.Reschedule)
        {
            var round = await _consentRoundRepository.GetCurrentAsync(changeRequestId);
            if (round?.ProposedOutcome == null || !round.AreAllRequiredSidesGranted())
            {
                return ChangeRequestCompletionResult.NotApplicable;
            }

            await _rescheduleFinalizer.FinalizeAsync(
                changeRequest,
                round,
                round.ProposedOutcome.Value,
                round.ProposedByUserId,
                completedOnConsentNotifyUserId: round.ProposedByUserId);
        }
        else
        {
            if (changeRequest.ProposedOutcome == null || !changeRequest.AreAllRequiredSidesGranted())
            {
                return ChangeRequestCompletionResult.NotApplicable;
            }

            await _cancellationFinalizer.FinalizeAsync(
                changeRequest,
                changeRequest.ProposedOutcome.Value,
                changeRequest.SubmittedByUserId,
                completedOnConsentNotifyUserId: changeRequest.SubmittedByUserId);
        }

        _logger.LogInformation(
            "ChangeRequest {ChangeRequestId} ({Type}) completed on consent.",
            changeRequestId,
            changeRequest.ChangeRequestType);
        return ChangeRequestCompletionResult.Completed;
    }
}
