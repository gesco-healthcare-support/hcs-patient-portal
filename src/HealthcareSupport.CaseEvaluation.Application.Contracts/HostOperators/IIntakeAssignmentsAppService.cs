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
}
