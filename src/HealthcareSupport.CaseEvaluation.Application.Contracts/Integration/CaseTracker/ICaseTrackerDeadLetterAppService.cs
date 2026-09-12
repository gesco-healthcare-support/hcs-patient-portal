using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Backs the admin dead-letter screen: what failed permanently, across every office, and the action to
/// deal with it.
/// </summary>
public interface ICaseTrackerDeadLetterAppService : IApplicationService
{
    /// <summary>
    /// Outstanding dead letters from ALL offices, newest first, with the office identified on each row.
    /// Host-scoped because internal staff work on the host surface and should not have to visit each
    /// office in turn to find a failure they must chase.
    /// </summary>
    Task<List<CaseTrackerDeadLetterDto>> GetListAsync();

    /// <summary>
    /// Re-sends the appointment and marks the failed row resolved.
    ///
    /// <para>Builds a FRESH payload from current data rather than replaying the stored one: that payload
    /// is a snapshot from when it failed, and a row that failed hours ago is exactly the case where the
    /// appointment has since been corrected.</para>
    /// </summary>
    Task<CaseTrackerDeadLetterRetryResultDto> RetryAsync(Guid officeId, Guid outboxItemId);
}
