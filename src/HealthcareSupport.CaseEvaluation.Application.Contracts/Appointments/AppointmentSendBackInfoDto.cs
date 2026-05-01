using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Read-only projection of an <c>AppointmentSendBackInfo</c> row, used by the
/// booker's AwaitingMoreInfo banner. <see cref="FlaggedFields"/> is the parsed
/// list (entity stores it as JSON; the Mapperly AfterMap deserialises here).
/// </summary>
public class AppointmentSendBackInfoDto
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid AppointmentId { get; set; }

    public List<string> FlaggedFields { get; set; } = new();

    public string? Note { get; set; }

    public DateTime SentBackAt { get; set; }

    public Guid? SentBackByUserId { get; set; }

    public bool IsResolved { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
