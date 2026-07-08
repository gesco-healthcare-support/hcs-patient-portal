using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- host-operator office-assignment management + the
/// Intake operator's own office switcher feed. The management methods are gated
/// by <c>CaseEvaluation.IntakeAssignments(.Manage)</c> (IT Admin + host Staff
/// Supervisor); <see cref="GetMyOfficesAsync"/> is gated by
/// <c>CaseEvaluation.IntakeImpersonation</c> (the operator's own capability).
/// All assignment state lives in the host/management database.
/// </summary>
public interface IIntakeAssignmentsAppService : IApplicationService
{
    /// <summary>All assignments with operator + office display names (management grid).</summary>
    Task<ListResultDto<IntakeOfficeAssignmentDto>> GetListAsync();

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged + searchable assignments list for the
    /// management grid (filter on operator name / email / office, sorting, offset
    /// paging). Batch-loads operators + offices (no per-row lookups). The non-paged
    /// <see cref="GetListAsync"/> stays for back-compat.
    /// </summary>
    Task<PagedResultDto<IntakeOfficeAssignmentDto>> GetPagedListAsync(GetIntakeAssignmentsInput input);

    /// <summary>
    /// Assign an operator to an office (idempotent). Eagerly provisions the
    /// operator's limited shadow Intake user in that office's database.
    /// </summary>
    Task AssignAsync(AssignIntakeOfficeDto input);

    /// <summary>
    /// Remove an assignment. Disables the operator's shadow Intake user in that
    /// office (defense in depth -- removing the row already blocks the gate).
    /// </summary>
    Task UnassignAsync(Guid operatorUserId, Guid officeId);

    /// <summary>Host Intake operator logins, for the management assign dropdown.</summary>
    Task<ListResultDto<LookupDto<Guid>>> GetAssignableOperatorsAsync();

    /// <summary>All offices, for the management assign dropdown.</summary>
    Task<ListResultDto<LookupDto<Guid>>> GetOfficeOptionsAsync();

    /// <summary>The current operator's assigned offices, for the SPA office switcher.</summary>
    Task<ListResultDto<LookupDto<Guid>>> GetMyOfficesAsync();

    /// <summary>
    /// QA item 9: view-only per-practice metrics (pending requests, today's
    /// appointments, pending change-requests) for the current Intake operator's
    /// assigned offices. Gated by <c>CaseEvaluation.IntakeImpersonation</c>; each
    /// office's counts are read inside that office's own database (isolation).
    /// </summary>
    Task<ListResultDto<IntakeOfficeMetricsDto>> GetMyOfficeMetricsAsync();

    /// <summary>
    /// The offices the current in-office switcher caller may hop into directly
    /// (F Half 2 single-click office -> office). Resolves the host operator from the
    /// impersonation claim (<c>AbpClaimTypes.ImpersonatorUserId</c>) -- the in-office
    /// shadow Intake user does NOT hold <c>IntakeImpersonation</c>, so unlike
    /// <see cref="GetMyOfficesAsync"/> this is callable while impersonating. Returns
    /// the impersonator's assigned offices, or empty when the caller is not an
    /// impersonation. Convenience only: the impersonation grant's per-office
    /// assignment gate (deny-by-default) remains the actual boundary.
    /// </summary>
    Task<ListResultDto<LookupDto<Guid>>> GetSwitchableOfficesAsync();
}
