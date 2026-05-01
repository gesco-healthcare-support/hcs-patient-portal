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
    public string? DefaultValue { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

public class AppointmentTypeFieldConfigCreateDto
{
    public Guid AppointmentTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public bool Hidden { get; set; }
    public bool ReadOnly { get; set; }
    public string? DefaultValue { get; set; }
}

public class AppointmentTypeFieldConfigUpdateDto
{
    public bool Hidden { get; set; }
    public bool ReadOnly { get; set; }
    public string? DefaultValue { get; set; }
    public string? ConcurrencyStamp { get; set; }
}
