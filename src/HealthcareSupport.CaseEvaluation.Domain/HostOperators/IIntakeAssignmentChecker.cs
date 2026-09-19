using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- the deny-by-default office boundary for host Intake
/// operators. The custom impersonation grant calls this BEFORE letting an
/// operator land in an office; a false result blocks the impersonation. Reads
/// <see cref="IntakeOfficeAssignment"/> rows in HOST context (they live in the
/// host/management DB). This is security-critical (a hole lets an operator reach
/// an unassigned office's PHI), so it is enforced server-side here, never in the
/// SPA office switcher.
/// </summary>
public interface IIntakeAssignmentChecker
{
    /// <summary>
    /// True only when an explicit assignment row links the operator to the
    /// office. Deny-by-default: no row -> false.
    /// </summary>
    Task<bool> IsAssignedAsync(Guid operatorUserId, Guid officeId);
}
