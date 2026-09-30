using System;
using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 17 (2026-05-04) -- input DTO for
/// <c>GetPendingChangeRequestsAsync</c>. The supervisor's inbox
/// supports filtering by request status (Pending / Accepted /
/// Rejected -- defaults to Pending), change-request type (Cancel /
/// Reschedule), and a creation-date range. Paged via ABP's
/// <see cref="PagedAndSortedResultRequestDto"/>.
/// </summary>
public class GetChangeRequestsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Defaults to Pending if not supplied.</summary>
    public RequestStatusType? RequestStatus { get; set; }

    public ChangeRequestType? ChangeRequestType { get; set; }

    public DateTime? CreatedFromUtc { get; set; }

    public DateTime? CreatedToUtc { get; set; }

    [CanBeNull]
    public string? FilterText { get; set; }
}
