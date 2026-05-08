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
    /// Ensures a Generating row exists for the (appointment, kind) tuple.
    /// If a Generated or Failed row already exists for that tuple, flips
    /// it back to Generating (the caller is about to re-run the merge).
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

        existing.Status = PacketGenerationStatus.Generating;
        existing.ErrorMessage = null;
        existing.BlobName = blobName;
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
