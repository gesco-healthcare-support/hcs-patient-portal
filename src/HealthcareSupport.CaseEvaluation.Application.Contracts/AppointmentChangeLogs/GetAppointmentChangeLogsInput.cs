using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Filter input for the global (internal-only) change-log list. When
/// <see cref="AppointmentId"/> or <see cref="RequestConfirmationNumber"/> is set the
/// query scopes to that one appointment; otherwise it spans all audited intake
/// entities within the optional time window.
/// </summary>
public class GetAppointmentChangeLogsInput : PagedAndSortedResultRequestDto
{
    public Guid? AppointmentId { get; set; }

    public string? RequestConfirmationNumber { get; set; }

    /// <summary>Friendly entity label filter (e.g. "Appointment", "Injury Detail").</summary>
    public string? EntityType { get; set; }

    /// <summary>Substring match on the changed property name.</summary>
    public string? FieldName { get; set; }

    /// <summary>Created / Updated / Deleted (case-insensitive); null = any.</summary>
    public string? ChangeType { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }
}
