using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// One file an <see cref="AppointmentPacket"/> used to point at, recorded at the moment a re-render
/// replaced it. Append-only: <see cref="AppointmentPacketManager.ReplaceGeneratedFileAsync"/> inserts
/// these rows and nothing updates or deletes them. The packet counterpart of
/// <see cref="AppointmentDocumentVersion"/>.
///
/// <para>WHY IT EXISTS (2026-10-21). When staff change the attorneys or the claim examiner on a live
/// appointment, the attorney and claim examiner packet is rendered again so a new party gets a notice
/// that names them. The parties already mailed hold the earlier copy, and records are kept seven
/// years, so the earlier file must stay producible: it stays in storage and this row is how it is
/// found, by packet, by appointment or by storage key.</para>
/// </summary>
public class AppointmentPacketVersion : Entity<Guid>, IMultiTenant, IHasCreationTime
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual Guid AppointmentPacketId { get; protected set; }

    /// <summary>Copied from the packet so every file ever produced for a case is one indexed query.</summary>
    public virtual Guid AppointmentId { get; protected set; }

    public virtual PacketKind Kind { get; protected set; }

    /// <summary>Storage key of the replaced file in the packets container.</summary>
    [NotNull]
    public virtual string BlobName { get; protected set; } = null!;

    /// <summary>When the replaced file was rendered (UTC).</summary>
    public virtual DateTime FileGeneratedAt { get; protected set; }

    /// <summary>The staff member whose change caused the re-render, when known.</summary>
    public virtual Guid? ReplacedByUserId { get; protected set; }

    /// <summary>When it was replaced. Stamped by ABP on insert.</summary>
    public virtual DateTime CreationTime { get; protected set; }

    protected AppointmentPacketVersion()
    {
    }

    /// <summary>
    /// Captures <paramref name="replaced"/>'s CURRENT file. Construct it before the row's
    /// <see cref="AppointmentPacket.BlobName"/> is overwritten, or it records the new file instead.
    /// </summary>
    public AppointmentPacketVersion(Guid id, AppointmentPacket replaced, Guid? replacedByUserId)
        : base(id)
    {
        Check.NotNull(replaced, nameof(replaced));

        TenantId = replaced.TenantId;
        AppointmentPacketId = replaced.Id;
        AppointmentId = replaced.AppointmentId;
        Kind = replaced.Kind;
        BlobName = Check.NotNullOrWhiteSpace(replaced.BlobName, nameof(replaced.BlobName));
        FileGeneratedAt = replaced.RegeneratedAt ?? replaced.GeneratedAt;
        ReplacedByUserId = replacedByUserId;
    }
}
