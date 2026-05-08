using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentPacketsAppService : CaseEvaluationAppService, IAppointmentPacketsAppService
{
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IBlobContainer<AppointmentPacketsContainer> _blobContainer;

    public AppointmentPacketsAppService(
        IRepository<AppointmentPacket, Guid> packetRepository,
        IBlobContainer<AppointmentPacketsContainer> blobContainer)
    {
        _packetRepository = packetRepository;
        _blobContainer = blobContainer;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPackets.Default)]
    public virtual async Task<AppointmentPacketDto?> GetByAppointmentAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        // Phase 1A.1 backward-compat: existing UI fetches one packet per
        // appointment. Filter to Kind=Patient so this surface keeps
        // returning the single Patient packet until Phase 1D.9 expands
        // the surface to per-kind reads.
        var queryable = await _packetRepository.GetQueryableAsync();
        var entity = queryable.FirstOrDefault(x => x.AppointmentId == appointmentId && x.Kind == PacketKind.Patient);
        return entity == null
            ? null
            : ObjectMapper.Map<AppointmentPacket, AppointmentPacketDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPackets.Default)]
    public virtual async Task<DownloadResult> DownloadAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        var queryable = await _packetRepository.GetQueryableAsync();
        var packet = queryable.FirstOrDefault(x => x.AppointmentId == appointmentId && x.Kind == PacketKind.Patient)
            ?? throw new EntityNotFoundException(typeof(AppointmentPacket), appointmentId);

        if (packet.Status != PacketGenerationStatus.Generated)
        {
            throw new UserFriendlyException("Packet is not ready yet. Please wait for generation to finish.");
        }

        var stream = await _blobContainer.GetAsync(packet.BlobName);
        if (stream == null)
        {
            throw new UserFriendlyException("Packet file is missing from storage.");
        }
        return new DownloadResult
        {
            Content = stream,
            FileName = $"appointment-packet-{appointmentId:N}.pdf",
            ContentType = "application/pdf",
        };
    }
}
