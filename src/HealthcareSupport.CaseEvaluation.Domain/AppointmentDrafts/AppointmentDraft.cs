using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

/// <summary>
/// #15 (2026-06-22): a server-persisted, in-progress booking-wizard draft.
/// Replaces the cosmetic localStorage-only autosave so a partially-filled
/// request survives navigate-away and resumes on return.
///
/// <para>One active draft per (tenant, creator) -- the self-scoped app service
/// upserts it. The form is stored opaquely in <see cref="PayloadJson"/> (the
/// wizard's form.getRawValue()), which IS PHI (patient name, DOB, SSN,
/// addresses); it is never logged and is purged by a TTL job. IMultiTenant, so
/// it lives in both the host and tenant DBs.</para>
///
/// <para>Base is <see cref="CreationAuditedAggregateRoot{TKey}"/> (NOT
/// FullAudited) on purpose: this row holds transient PHI, so discard and the TTL
/// purge MUST physically delete it. Soft-delete would leave the PHI payload at
/// rest behind an IsDeleted flag, defeating the minimize-retention goal (#15
/// D5). CreatorId (from creation audit) scopes the row to its owner.</para>
/// </summary>
public class AppointmentDraft : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>The serialized booking-form snapshot (the wizard's
    /// form.getRawValue() plus step). Opaque PHI blob -- never logged.</summary>
    [NotNull]
    public virtual string PayloadJson { get; set; } = null!;

    /// <summary>Wizard step index the draft was last on; resume lands here.</summary>
    public virtual int CurrentStep { get; set; }

    /// <summary>Short, NON-PHI label for a resume affordance (e.g. the
    /// appointment-type name). Optional.</summary>
    public virtual string? Label { get; set; }

    /// <summary>When the draft was last upserted; drives the TTL purge.</summary>
    public virtual DateTime LastSavedTime { get; set; }

    protected AppointmentDraft()
    {
    }

    public AppointmentDraft(
        Guid id,
        string payloadJson,
        int currentStep,
        DateTime lastSavedTime,
        string? label = null,
        Guid? tenantId = null)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(payloadJson, nameof(payloadJson));
        SetLabel(label);
        PayloadJson = payloadJson;
        CurrentStep = currentStep;
        LastSavedTime = lastSavedTime;
        TenantId = tenantId;
    }

    /// <summary>Replaces the draft contents on a subsequent checkpoint save.</summary>
    public virtual void UpdatePayload(
        string payloadJson,
        int currentStep,
        DateTime lastSavedTime,
        string? label = null)
    {
        Check.NotNullOrWhiteSpace(payloadJson, nameof(payloadJson));
        SetLabel(label);
        PayloadJson = payloadJson;
        CurrentStep = currentStep;
        LastSavedTime = lastSavedTime;
    }

    private void SetLabel(string? label)
    {
        if (!string.IsNullOrEmpty(label))
        {
            Check.Length(label, nameof(label), AppointmentDraftConsts.LabelMaxLength, 0);
        }
        Label = label;
    }
}
