using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// One Send Back round, read by the external fix-it page (note + flagged
/// fields, un-masked) and the staff history view. Returned by GetOpenAsync.
/// </summary>
public class AppointmentInfoRequestDto
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }

    public string Note { get; set; } = string.Empty;

    public List<FlaggedFieldDto> FlaggedFields { get; set; } = new();

    public InfoRequestStatus Status { get; set; }

    public Guid? RequestedByUserId { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
