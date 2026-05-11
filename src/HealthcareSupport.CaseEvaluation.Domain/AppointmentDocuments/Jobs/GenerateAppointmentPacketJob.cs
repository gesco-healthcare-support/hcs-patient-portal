using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;

[Serializable]
public class GenerateAppointmentPacketArgs
{
    public Guid AppointmentId { get; set; }
    public Guid? TenantId { get; set; }
}

/// <summary>
/// Hangfire-backed packet generation. Triggered by
/// <see cref="PacketGenerationOnApprovedHandler"/> on
/// <c>AppointmentStatusChangedEto</c> with
/// <c>ToStatus = Approved</c>, or by the office's Regenerate button.
///
/// <para>Phase 1C.6 (2026-05-08) replaced the legacy single-PDF cover-sheet
/// merger with a multi-kind orchestrator that mirrors OLD's per-recipient
/// fanout from <c>AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail</c>:</para>
/// <list type="bullet">
///   <item>Patient packet -- always generated.</item>
///   <item>Doctor packet -- always generated, storage-only (not emailed).</item>
///   <item>AttorneyClaimExaminer packet -- only for PQME / PQMEREEVAL /
///   AME / AMEREEVAL appointment types.</item>
/// </list>
///
/// <para>Each kind generates in its own try/catch so a failure on one
/// kind does not block the others. Each kind gets its own
/// <see cref="AppointmentPacket"/> row keyed by the (TenantId,
/// AppointmentId, Kind) composite unique index added in Phase 1A.1.</para>
///
/// <para>The cover-sheet PDF generator and PdfSharp merge service are kept
/// in the codebase but no longer wired here -- they will be re-used by
/// Phase 2's DOCX -&gt; PDF conversion path.</para>
/// </summary>
public class GenerateAppointmentPacketJob :
    AsyncBackgroundJob<GenerateAppointmentPacketArgs>,
    ITransientDependency
{
    /// <summary>
    /// AppointmentType.Name values that trigger the AttorneyClaimExaminer
    /// packet (case-insensitive match). Matches OLD's per-type branches at
    /// <c>AppointmentDocumentDomain.cs:643, :689, :740, :801</c>.
    /// </summary>
    private static readonly HashSet<string> AttorneyClaimExaminerTypes =
        new(StringComparer.OrdinalIgnoreCase) { "PQME", "PQMEREEVAL", "AME", "AMEREEVAL" };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly AppointmentPacketManager _packetManager;
    private readonly IBlobContainer<AppointmentPacketsContainer> _packetsContainer;
    private readonly IPacketTokenResolver _tokenResolver;
    private readonly IDocxTemplateRenderer _renderer;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<GenerateAppointmentPacketJob> _logger;

    public GenerateAppointmentPacketJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        AppointmentPacketManager packetManager,
        IBlobContainer<AppointmentPacketsContainer> packetsContainer,
        IPacketTokenResolver tokenResolver,
        IDocxTemplateRenderer renderer,
        ICurrentTenant currentTenant,
        ILogger<GenerateAppointmentPacketJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _packetManager = packetManager;
        _packetsContainer = packetsContainer;
        _tokenResolver = tokenResolver;
        _renderer = renderer;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    // [UnitOfWork] mirrors AppointmentDayReminderJob.cs:56. Without it, each
    // repo call auto-starts its own UoW that disposes the DbContext on return;
    // FirstOrDefaultAsync(predicate) materializes against a disposed context
    // and throws ObjectDisposedException. FindAsync(id) survives via the
    // identity-map fast path which is why the symptom is selective.
    [UnitOfWork]
    public override async Task ExecuteAsync(GenerateAppointmentPacketArgs args)
    {
        using (_currentTenant.Change(args.TenantId))
        {
            await GenerateInsideTenantAsync(args);
        }
    }

    private async Task GenerateInsideTenantAsync(GenerateAppointmentPacketArgs args)
    {
        var appointment = await _appointmentRepository.GetAsync(args.AppointmentId);
        var appointmentType = await _appointmentTypeRepository.FindAsync(appointment.AppointmentTypeId);

        // Resolve once -- the same context applies to all 3 templates.
        var context = await _tokenResolver.ResolveAsync(args.AppointmentId);

        var kindsToGenerate = new List<PacketKind> { PacketKind.Patient, PacketKind.Doctor };
        if (appointmentType != null && AttorneyClaimExaminerTypes.Contains(appointmentType.Name))
        {
            kindsToGenerate.Add(PacketKind.AttorneyClaimExaminer);
        }

        foreach (var kind in kindsToGenerate)
        {
            await GenerateKindAsync(args.AppointmentId, kind, context);
        }
    }

    private async Task GenerateKindAsync(Guid appointmentId, PacketKind kind, PacketTokenContext context)
    {
        var tenantSegment = _currentTenant.Id?.ToString() ?? "host";
        var blobName = $"{tenantSegment}/{appointmentId}/packet/{kind.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}.docx";

        var packet = await _packetManager.EnsureGeneratingAsync(_currentTenant.Id, appointmentId, kind, blobName);

        try
        {
            var templateBytes = EmbeddedTemplateResources.LoadTemplate(kind);
            var rendered = _renderer.Render(templateBytes, context);

            using var ms = new MemoryStream(rendered);
            await _packetsContainer.SaveAsync(blobName, ms, overrideExisting: true);

            await _packetManager.MarkGeneratedAsync(packet.Id, blobName);

            _logger.LogInformation(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} generated ({Bytes} bytes).",
                appointmentId, kind, rendered.Length);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ArgumentException)
        {
            _logger.LogError(ex,
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} failed; marking Failed (no retry).",
                appointmentId, kind);
            await _packetManager.MarkFailedAsync(packet.Id, ex.Message);
            // Do not rethrow -- avoid Hangfire retry storm. Other kinds
            // for this appointment continue generating.
        }
    }
}
