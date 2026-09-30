using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: thin domain service for AppointmentPacket. The Hangfire job
/// (<see cref="Jobs.GenerateAppointmentPacketJob"/>) reaches into the
/// repository directly for the merge path; this manager exposes the
/// idempotent "find-or-create the Generating row" call so the AppService
/// + the on-Approved event handler share one entry point.
/// </summary>
public class AppointmentPacketManager : DomainService
{
    protected IRepository<AppointmentPacket, Guid> _packetRepository;

    public AppointmentPacketManager(IRepository<AppointmentPacket, Guid> packetRepository)
    {
        _packetRepository = packetRepository;
    }

    /// <summary>
    /// Idempotency guard (T1): claims the (appointment, kind) row for generation.
    /// <list type="bullet">
    ///   <item>No row yet -> insert a Generating row.</item>
    ///   <item>Already <see cref="PacketGenerationStatus.Generated"/> -> return it
    ///     UNTOUCHED so the caller skips re-render + re-publish. Flipping it back to
    ///     Generating here is what re-fired duplicate PHI-bearing packet emails on
    ///     every Hangfire retry / crash re-fetch.</item>
    ///   <item>Failed or still Generating -> reset to Generating for the re-attempt.</item>
    /// </list>
    /// A concurrent claim races the filtered unique index and surfaces as
    /// <c>AbpDbConcurrencyException</c>; the caller catches it and skips the kind.
    /// </summary>
    public virtual async Task<AppointmentPacket> EnsureGeneratingAsync(Guid? tenantId, Guid appointmentId, PacketKind kind, string blobName)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(blobName, nameof(blobName));

        var queryable = await _packetRepository.GetQueryableAsync();
        var existing = queryable.FirstOrDefault(x => x.AppointmentId == appointmentId && x.Kind == kind);
        if (existing == null)
        {
            existing = new AppointmentPacket(GuidGenerator.Create(), tenantId, appointmentId, kind, blobName, PacketGenerationStatus.Generating);
            return await _packetRepository.InsertAsync(existing, autoSave: true);
        }

        // Already generated -> return as-is; the caller (GenerateAppointmentPacketJob)
        // skips render + PacketGeneratedEto so a retry cannot duplicate the email.
        if (existing.Status == PacketGenerationStatus.Generated)
        {
            return existing;
        }

        // Failed or still Generating -> reset for the re-attempt. Re-stamp
        // LastAttemptAt (T11) so a freshly-retried row is not read as stale by
        // the reconciliation sweep.
        existing.Status = PacketGenerationStatus.Generating;
        existing.ErrorMessage = null;
        existing.BlobName = blobName;
        existing.LastAttemptAt = DateTime.UtcNow;
        return await _packetRepository.UpdateAsync(existing, autoSave: true);
    }

    public virtual async Task MarkGeneratedAsync(Guid id, [CanBeNull] string? newBlobName = null)
    {
        var packet = await _packetRepository.GetAsync(id);
        var alreadyGenerated = packet.Status == PacketGenerationStatus.Generated;
        if (!string.IsNullOrWhiteSpace(newBlobName))
        {
            packet.BlobName = newBlobName;
        }
        packet.Status = PacketGenerationStatus.Generated;
        packet.ErrorMessage = null;
        if (alreadyGenerated)
        {
            packet.RegeneratedAt = DateTime.UtcNow;
        }
        else
        {
            packet.GeneratedAt = DateTime.UtcNow;
        }
        await _packetRepository.UpdateAsync(packet);
    }

    public virtual async Task MarkFailedAsync(Guid id, string errorMessage)
    {
        var packet = await _packetRepository.GetAsync(id);
        packet.Status = PacketGenerationStatus.Failed;
        packet.ErrorMessage = (errorMessage ?? string.Empty).Length > AppointmentPacketConsts.ErrorMessageMaxLength
            ? errorMessage!.Substring(0, AppointmentPacketConsts.ErrorMessageMaxLength)
            : errorMessage;
        await _packetRepository.UpdateAsync(packet);
    }
}
