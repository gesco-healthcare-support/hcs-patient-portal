using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
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
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IBlobContainer<AppointmentPacketsContainer> _blobContainer;

    public AppointmentPacketsAppService(
        IRepository<AppointmentPacket, Guid> packetRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IBlobContainer<AppointmentPacketsContainer> blobContainer)
    {
        _packetRepository = packetRepository;
        _appointmentRepository = appointmentRepository;
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

    [Authorize(CaseEvaluationPermissions.AppointmentPackets.Default)]
    public virtual async Task<List<AppointmentPacketDto>> GetListByAppointmentAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        var queryable = await _packetRepository.GetQueryableAsync();
        var entities = queryable
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.Kind)
            .ToList();
        return entities
            .Select(e => ObjectMapper.Map<AppointmentPacket, AppointmentPacketDto>(e))
            .ToList();
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPackets.Default)]
    public virtual async Task<DownloadResult> DownloadByKindAsync(Guid appointmentId, PacketKind kind)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        var queryable = await _packetRepository.GetQueryableAsync();
        var packet = queryable.FirstOrDefault(x => x.AppointmentId == appointmentId && x.Kind == kind)
            ?? throw new EntityNotFoundException(typeof(AppointmentPacket), $"{appointmentId}/{kind}");

        if (packet.Status != PacketGenerationStatus.Generated)
        {
            throw new UserFriendlyException("Packet is not ready yet. Please wait for generation to finish.");
        }

        var stream = await _blobContainer.GetAsync(packet.BlobName);
        if (stream == null)
        {
            throw new UserFriendlyException("Packet file is missing from storage.");
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        var confirmation = appointment?.RequestConfirmationNumber ?? appointmentId.ToString("N");
        var fileName = BuildKindFileName(confirmation, kind, packet.GeneratedAt);

        // Phase 1 produces DOCX. Phase 2 will switch to application/pdf
        // when the DOCX -> PDF conversion is wired in. Detect from the
        // blob's extension so legacy rows generated before Phase 1C still
        // download with the correct content-type.
        var contentType = packet.BlobName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            ? DocxContentType
            : "application/pdf";

        return new DownloadResult
        {
            Content = stream,
            FileName = fileName,
            ContentType = contentType,
        };
    }

    /// <summary>
    /// OLD-verbatim filename pattern. Matches PacketAttachmentProvider so
    /// downloads via the UI and via email attachment use identical names.
    /// </summary>
    private static string BuildKindFileName(string confirmation, PacketKind kind, DateTime generatedAt)
    {
        var kindName = kind switch
        {
            PacketKind.Patient => "Patient Packet",
            PacketKind.Doctor => "Doctor Packet",
            PacketKind.AttorneyClaimExaminer => "Attorney Claim Examiner Packet",
            _ => kind.ToString(),
        };
        var timestamp = generatedAt.ToString("ddMMyyyy_hhmmss", CultureInfo.InvariantCulture);
        return $"{confirmation}_{kindName}_{timestamp}.docx";
    }
}
