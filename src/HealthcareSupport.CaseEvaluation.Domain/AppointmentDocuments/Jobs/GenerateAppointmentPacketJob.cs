using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;

[Serializable]
public class GenerateAppointmentPacketArgs
{
    public Guid AppointmentId { get; set; }
    public Guid? TenantId { get; set; }
}

/// <summary>
/// W2-11: Hangfire-backed packet generation. Triggered when an appointment
/// transitions to Approved (via <see cref="PacketGenerationOnApprovedHandler"/>)
/// or when the office clicks Regenerate.
///
/// Pulls the appointment + its supporting entities, builds the cover page
/// (MigraDoc), pulls the approved documents from blob storage, merges via
/// <see cref="PacketMergeService"/>, writes the final PDF blob, and flips
/// the <see cref="AppointmentPacket"/> row to Generated.
///
/// On exception: marks Failed + ErrorMessage. Does NOT re-throw, to avoid
/// the Hangfire retry storm on a persistently-corrupt source document.
/// The office sees the error in the UI and can re-upload + Regenerate.
/// </summary>
public class GenerateAppointmentPacketJob :
    AsyncBackgroundJob<GenerateAppointmentPacketArgs>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IRepository<Location, Guid> _locationRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly AppointmentPacketManager _packetManager;
    private readonly IBlobContainer<AppointmentDocumentsContainer> _documentsContainer;
    private readonly IBlobContainer<AppointmentPacketsContainer> _packetsContainer;
    private readonly CoverPageGenerator _coverGenerator;
    private readonly PacketMergeService _mergeService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<GenerateAppointmentPacketJob> _logger;

    public GenerateAppointmentPacketJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        IRepository<Location, Guid> locationRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryRepository,
        AppointmentPacketManager packetManager,
        IBlobContainer<AppointmentDocumentsContainer> documentsContainer,
        IBlobContainer<AppointmentPacketsContainer> packetsContainer,
        CoverPageGenerator coverGenerator,
        PacketMergeService mergeService,
        ICurrentTenant currentTenant,
        ILogger<GenerateAppointmentPacketJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
        _documentRepository = documentRepository;
        _injuryRepository = injuryRepository;
        _packetManager = packetManager;
        _documentsContainer = documentsContainer;
        _packetsContainer = packetsContainer;
        _coverGenerator = coverGenerator;
        _mergeService = mergeService;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public override async Task ExecuteAsync(GenerateAppointmentPacketArgs args)
    {
        using (_currentTenant.Change(args.TenantId))
        {
            await GenerateInsideTenantAsync(args);
        }
    }

    private async Task GenerateInsideTenantAsync(GenerateAppointmentPacketArgs args)
    {
        var tenantSegment = _currentTenant.Id?.ToString() ?? "host";
        var blobName = $"{tenantSegment}/{args.AppointmentId}/packet/{Guid.NewGuid():N}.pdf";

        // Phase 1A.1 backward-compat: this legacy single-PDF job is replaced
        // by the per-kind orchestrator in Phase 1C.6. Until then, treat the
        // merged-PDF as Kind=Patient so the existing UI keeps working.
        var packet = await _packetManager.EnsureGeneratingAsync(_currentTenant.Id, args.AppointmentId, PacketKind.Patient, blobName);

        try
        {
            var appointment = await _appointmentRepository.GetAsync(args.AppointmentId);
            var patient = await _patientRepository.FindAsync(appointment.PatientId);
            var appointmentType = await _appointmentTypeRepository.FindAsync(appointment.AppointmentTypeId);
            var location = await _locationRepository.FindAsync(appointment.LocationId);

            var injuryQueryable = await _injuryRepository.GetQueryableAsync();
            var injury = injuryQueryable.FirstOrDefault(i => i.AppointmentId == args.AppointmentId);

            var coverBytes = _coverGenerator.RenderCoverPagePdf(
                appointment,
                patient,
                appointmentType,
                location,
                claimNumber: injury?.ClaimNumber,
                bodyPartsSummary: injury?.BodyPartsSummary,
                wcabAdj: injury?.WcabAdj);

            // Pull approved documents in CreationTime order.
            var docQueryable = await _documentRepository.GetQueryableAsync();
            var approvedDocs = docQueryable
                .Where(d => d.AppointmentId == args.AppointmentId && d.Status == DocumentStatus.Accepted)
                .OrderBy(d => d.CreationTime)
                .ToList();

            var inputs = new System.Collections.Generic.List<PacketMergeService.MergeInput>();
            foreach (var doc in approvedDocs)
            {
                var bytes = await _documentsContainer.GetAllBytesOrNullAsync(doc.BlobName);
                if (bytes == null)
                {
                    _logger.LogWarning(
                        "GenerateAppointmentPacketJob: blob missing for document {DocumentId} ({BlobName}); skipped.",
                        doc.Id, doc.BlobName);
                    continue;
                }
                inputs.Add(new PacketMergeService.MergeInput
                {
                    FileName = doc.FileName,
                    ContentType = doc.ContentType,
                    Bytes = bytes,
                });
            }

            var merged = _mergeService.Merge(coverBytes, inputs);
            await _packetsContainer.SaveAsync(blobName, new MemoryStream(merged), overrideExisting: true);

            await _packetManager.MarkGeneratedAsync(packet.Id, blobName);
            _logger.LogInformation(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} packet generated ({Pages} pages, {Bytes} bytes).",
                args.AppointmentId, "n/a", merged.Length);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex.GetType().FullName?.Contains("PdfSharp") == true)
        {
            _logger.LogError(ex,
                "GenerateAppointmentPacketJob: appointment {AppointmentId} packet generation failed; marking Failed (no retry).",
                args.AppointmentId);
            await _packetManager.MarkFailedAsync(packet.Id, ex.Message);
            // Do not rethrow -- avoid Hangfire retry storm on persistently corrupt input.
        }
    }
}
