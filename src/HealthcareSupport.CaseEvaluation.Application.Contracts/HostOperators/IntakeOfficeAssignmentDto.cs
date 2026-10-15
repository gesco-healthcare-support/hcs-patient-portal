using System;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- one row in the intake office-assignment management
/// grid: which host Intake operator may enter which office, with display names
/// resolved for the UI.
/// </summary>
public class IntakeOfficeAssignmentDto
{
    public Guid Id { get; set; }
    public Guid OperatorUserId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string OperatorEmail { get; set; } = string.Empty;
    public Guid OfficeId { get; set; }
    public string OfficeName { get; set; } = string.Empty;
}
