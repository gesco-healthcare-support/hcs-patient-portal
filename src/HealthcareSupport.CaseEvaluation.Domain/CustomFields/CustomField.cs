using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// IT-Admin-defined intake field that is rendered on the patient
/// appointment-request form when <c>SystemParameter.IsCustomField = true</c>.
/// Mirrors OLD's <c>spm.CustomFields</c> table verbatim (Phase 6,
/// 2026-05-03). Distinct from the W2-5 <c>AppointmentTypeFieldConfig</c>
/// (which overrides existing form fields, not adds new ones) -- see
/// <c>docs/parity/it-admin-custom-fields.md</c> for the audit-corrected
/// distinction.
///
/// Per-AppointmentType cap is enforced at the AppService layer
/// (<c>CustomFieldsAppService.EnsureUnderActiveCapAsync</c>) because the
/// schema permits more than 10 rows -- the rule is "at most 10 ACTIVE
/// rows per AppointmentTypeId". OLD enforces a buggy GLOBAL count;
/// NEW corrects to per-AppointmentTypeId per spec intent (audit-doc
/// OLD-bug-fix exception).
///
/// <c>AppointmentTypeId</c> is nullable (matches OLD schema). In
/// practice IT Admin always sets it; null means "applies to all types".
/// </summary>
[Audited]
public class CustomField : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string FieldLabel { get; set; } = null!;

    /// <summary>
    /// 1-based render order on the booking form. Auto-assigned to
    /// <c>max(DisplayOrder) + 1</c> on create per OLD
    /// <c>CustomFieldDomain.cs:55-61</c>. Caller-supplied values are
    /// ignored on create; updates may renumber explicitly.
    /// </summary>
    public virtual int DisplayOrder { get; set; }

    public virtual CustomFieldType FieldType { get; set; }

    /// <summary>Max length for <c>Text</c> inputs; null for Date / Number.</summary>
    public virtual int? FieldLength { get; set; }

    /// <summary>Comma-separated list of options when the field renders as a select.</summary>
    [CanBeNull]
    public virtual string? MultipleValues { get; set; }

    [CanBeNull]
    public virtual string? DefaultValue { get; set; }

    public virtual bool IsMandatory { get; set; }

    public virtual Guid? AppointmentTypeId { get; set; }

    public virtual bool IsActive { get; set; }

    protected CustomField()
    {
    }

    public CustomField(
        Guid id,
        Guid? tenantId,
        string fieldLabel,
        int displayOrder,
        CustomFieldType fieldType,
        Guid? appointmentTypeId,
        int? fieldLength = null,
        string? multipleValues = null,
        string? defaultValue = null,
        bool isMandatory = false,
        bool isActive = true)
    {
        Id = id;
        TenantId = tenantId;
        Check.NotNullOrWhiteSpace(fieldLabel, nameof(fieldLabel));
        Check.Length(fieldLabel, nameof(fieldLabel), CustomFieldConsts.FieldLabelMaxLength);
        Check.Length(multipleValues, nameof(multipleValues), CustomFieldConsts.MultipleValuesMaxLength);
        Check.Length(defaultValue, nameof(defaultValue), CustomFieldConsts.DefaultValueMaxLength);
        FieldLabel = fieldLabel;
        DisplayOrder = displayOrder;
        FieldType = fieldType;
        FieldLength = fieldLength;
        MultipleValues = multipleValues;
        DefaultValue = defaultValue;
        IsMandatory = isMandatory;
        AppointmentTypeId = appointmentTypeId;
        IsActive = isActive;
    }
}
