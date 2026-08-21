using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

public class AppointmentTypeFieldConfigDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid AppointmentTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

public class AppointmentTypeFieldConfigCreateDto
{
    public Guid AppointmentTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
}

public class AppointmentTypeFieldConfigUpdateDto
{
    public bool Hidden { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

/// <summary>
/// Prompt 15 (2026-06-15): one row in a batch save of an AppointmentType's
/// field configuration. Keyed by <see cref="FieldName"/> (not Id) -- the batch
/// upsert reconciles the desired set against the stored rows (create / update /
/// delete) so the admin saves the whole panel at once.
/// </summary>
public class AppointmentTypeFieldConfigBatchItemDto
{
    public string FieldName { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
}
