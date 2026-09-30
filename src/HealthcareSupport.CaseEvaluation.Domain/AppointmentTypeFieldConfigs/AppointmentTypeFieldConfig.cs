using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs;

/// <summary>
/// W2-5: per-AppointmentType field-level config row. The booker's
/// appointment-add form reads the matching set on AppointmentType selection
/// and applies <see cref="Hidden"/> / <see cref="ReadOnly"/> /
/// <see cref="DefaultValue"/> to the form control matching
/// <see cref="FieldName"/>.
///
/// FieldName matches the canonical key vocabulary in
/// `angular/src/app/appointments/appointment/send-back-fields.ts`
/// (FlaggableField.key). DB does NOT enforce the vocabulary; admin UI
/// constrains the choices and stale rows silently no-op when the
/// front-end registry renames a field.
///
/// Composite unique index on (TenantId, AppointmentTypeId, FieldName)
/// prevents duplicate config rows for the same field on the same
/// AppointmentType per tenant.
/// </summary>
[Audited]
public class AppointmentTypeFieldConfig : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentTypeId { get; set; }

    [NotNull]
    public virtual string FieldName { get; set; } = null!;

    public virtual bool Hidden { get; set; }

    public virtual bool ReadOnly { get; set; }

    /// <summary>
    /// Prompt 15 (2026-06-15): whether the booking form must require this field
    /// for this AppointmentType. The Configuration UI's third per-field toggle
    /// (Required / Optional). Booking-form enforcement is a separate change; this
    /// column stores the admin's choice.
    /// </summary>
    public virtual bool Required { get; set; }

    [CanBeNull]
    public virtual string? DefaultValue { get; set; }

    protected AppointmentTypeFieldConfig() { }

    public AppointmentTypeFieldConfig(
        Guid id,
        Guid? tenantId,
        Guid appointmentTypeId,
        string fieldName,
        bool hidden = false,
        bool readOnly = false,
        string? defaultValue = null,
        bool required = false)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(fieldName, nameof(fieldName));
        Check.Length(fieldName, nameof(fieldName), AppointmentTypeFieldConfigConsts.FieldNameMaxLength, 0);
        if (defaultValue != null)
        {
            Check.Length(defaultValue, nameof(defaultValue), AppointmentTypeFieldConfigConsts.DefaultValueMaxLength, 0);
        }
        TenantId = tenantId;
        AppointmentTypeId = appointmentTypeId;
        FieldName = fieldName;
        Hidden = hidden;
        ReadOnly = readOnly;
        Required = required;
        DefaultValue = defaultValue;
    }
}
