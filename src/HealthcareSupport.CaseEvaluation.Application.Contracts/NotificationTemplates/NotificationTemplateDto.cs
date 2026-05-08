using System;
using JetBrains.Annotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Read DTO for <c>NotificationTemplate</c>. Surfaces the per-tenant
/// editable surface (Subject + BodyEmail + BodySms + IsActive) plus the
/// stable identifiers (<c>TemplateCode</c>, <c>TemplateTypeId</c>) and
/// audit columns from <see cref="FullAuditedEntityDto{TKey}"/>.
///
/// Carries <see cref="ConcurrencyStamp"/> so the client can round-trip it
/// on update for optimistic concurrency (matches Phase 3's
/// <c>SystemParameter</c> pattern -- additive safety, OLD lacked it).
/// </summary>
public class NotificationTemplateDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Stable event identifier (one of
    /// <c>NotificationTemplateConsts.Codes.*</c>). Immutable after seed --
    /// IT Admin cannot rename a code via the editor.
    /// </summary>
    public string TemplateCode { get; set; } = null!;

    /// <summary>
    /// FK to <c>NotificationTemplateType</c> (Email = static GUID
    /// <c>c0000001-0000-4000-9000-000000000001</c>, SMS =
    /// <c>c0000001-0000-4000-9000-000000000002</c>). Phase 4 leaves this
    /// immutable; future phases may surface change-type semantics.
    /// </summary>
    public Guid TemplateTypeId { get; set; }

    [CanBeNull]
    public string? Subject { get; set; }

    public string BodyEmail { get; set; } = null!;

    public string BodySms { get; set; } = null!;

    [CanBeNull]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
