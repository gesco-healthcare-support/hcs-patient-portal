using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Per-appointment value submitted for a <see cref="CustomField"/>.
/// Mirrors OLD's <c>spm.CustomFieldsValues</c> row, replacing OLD's
/// polymorphic <c>ReferenceId</c> with an explicit <c>AppointmentId</c>
/// FK -- OLD's polymorphism only ever pointed at appointments anyway,
/// so the FK is strictly more correct without changing visible behavior.
/// (Phase 6, 2026-05-03; flagged in the audit doc's "Things NOT to port"
/// section.)
/// </summary>
[Audited]
public class CustomFieldValue : FullAuditedEntity<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid CustomFieldId { get; set; }

    public virtual Guid AppointmentId { get; set; }

    [NotNull]
    public virtual string Value { get; set; } = null!;

    protected CustomFieldValue()
    {
    }

    public CustomFieldValue(
        Guid id,
        Guid? tenantId,
        Guid customFieldId,
        Guid appointmentId,
        string value)
    {
        Id = id;
        TenantId = tenantId;
        CustomFieldId = customFieldId;
        AppointmentId = appointmentId;
        Check.NotNull(value, nameof(value));
        Check.Length(value, nameof(value), CustomFieldConsts.ValueMaxLength);
        Value = value;
    }
}
