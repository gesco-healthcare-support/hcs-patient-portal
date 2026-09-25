using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// Master template catalog -- a Document is a blank PDF template that IT Admin
/// uploads once and links to one or more <c>PackageDetail</c>s. Mirrors OLD's
/// <c>Document</c> table (Phase 1.2, 2026-05-01).
/// </summary>
[Audited]
public class Document : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Name { get; set; } = null!;

    [NotNull]
    public virtual string BlobName { get; set; } = null!;

    [CanBeNull]
    public virtual string? ContentType { get; set; }

    public virtual bool IsActive { get; set; }

    protected Document()
    {
    }

    public Document(
        Guid id,
        Guid? tenantId,
        string name,
        string blobName,
        string? contentType,
        bool isActive = true)
    {
        Id = id;
        TenantId = tenantId;
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), DocumentConsts.NameMaxLength);
        Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        Check.Length(blobName, nameof(blobName), DocumentConsts.BlobNameMaxLength);
        Check.Length(contentType, nameof(contentType), DocumentConsts.ContentTypeMaxLength);
        Name = name;
        BlobName = blobName;
        ContentType = contentType;
        IsActive = isActive;
    }
}
