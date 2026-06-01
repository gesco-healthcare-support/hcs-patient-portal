using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
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
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly AppointmentPacketManager _packetManager;
    private readonly IBlobContainer<AppointmentPacketsContainer> _packetsContainer;
    private readonly IPacketTokenResolver _tokenResolver;
    private readonly IDocxTemplateRenderer _renderer;
    private readonly IDocxToPdfConverter _docxToPdfConverter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<GenerateAppointmentPacketJob> _logger;

    public GenerateAppointmentPacketJob(
        IRepository<Appointment, Guid> appointmentRepository,
        AppointmentPacketManager packetManager,
        IBlobContainer<AppointmentPacketsContainer> packetsContainer,
        IPacketTokenResolver tokenResolver,
        IDocxTemplateRenderer renderer,
        IDocxToPdfConverter docxToPdfConverter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<GenerateAppointmentPacketJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _packetManager = packetManager;
        _packetsContainer = packetsContainer;
        _tokenResolver = tokenResolver;
        _renderer = renderer;
        _docxToPdfConverter = docxToPdfConverter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _unitOfWorkManager = unitOfWorkManager;
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
        // Existence guard -- throws EntityNotFoundException if the appointment
        // was deleted between enqueue and run (preserves prior behavior).
        await _appointmentRepository.GetAsync(args.AppointmentId);

        // Resolve once -- the same context applies to all 3 templates.
        var context = await _tokenResolver.ResolveAsync(args.AppointmentId);

        // F5 (2026-05-29): every appointment type generates all three packets.
        // The AttorneyClaimExaminer packet was previously gated to PQME/AME via
        // a substring match that silently missed "Panel QME" (the space breaks
        // Contains("PQME")); the gate is removed so all types get it.
        var kindsToGenerate = new List<PacketKind>
        {
            PacketKind.Patient,
            PacketKind.Doctor,
            PacketKind.AttorneyClaimExaminer,
        };

        foreach (var kind in kindsToGenerate)
        {
            await GenerateKindAsync(args.AppointmentId, kind, context);
        }
    }

    private async Task GenerateKindAsync(Guid appointmentId, PacketKind kind, PacketTokenContext context)
    {
        var tenantSegment = _currentTenant.Id?.ToString() ?? "host";
        // Phase 2 (2026-05-11): blob extension is .pdf, not .docx --
        // the renderer still emits DOCX but Gotenberg converts it before
        // we persist. PacketAttachmentProvider.PdfContentType matches.
        var blobName = $"{tenantSegment}/{appointmentId}/packet/{kind.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}.pdf";

        var packet = await _packetManager.EnsureGeneratingAsync(_currentTenant.Id, appointmentId, kind, blobName);

        try
        {
            var templateBytes = EmbeddedTemplateResources.LoadTemplate(kind);
            var docxBytes = _renderer.Render(templateBytes, context);
            // Phase 2 (2026-05-11): hand the rendered DOCX to the Gotenberg
            // sidecar for PDF conversion. Transport / timeout failures here
            // intentionally propagate -- they are NOT in the per-kind
            // catch filter below, so Hangfire's retry policy will re-run
            // the job and try again. Only permanent rendering failures
            // (IOException, InvalidOperationException, ArgumentException
            // from the OpenXml renderer) get marked Failed without retry.
            var pdfBytes = await _docxToPdfConverter.ConvertAsync(docxBytes);

            using var ms = new MemoryStream(pdfBytes);
            await _packetsContainer.SaveAsync(blobName, ms, overrideExisting: true);

            await _packetManager.MarkGeneratedAsync(packet.Id, blobName);

            // Phase 4 (Category 4, 2026-05-10): notify email handlers
            // that the packet is ready for fan-out. One event per kind so
            // PatientPacketEmailHandler + AttyCEPacketEmailHandler can
            // subscribe independently. Doctor kind fires too but has no
            // subscriber -- mirrors OLD's "Doctor packet is generated
            // and stored, but NOT emailed" asymmetry at
            // AppointmentDocumentDomain.cs:561-634.
            //
            // 2026-05-11: Defer publish until the surrounding UoW commits.
            // ILocalEventBus inside an active UoW already buffers handler
            // invocations until commit, but the handler in turn enqueues a
            // Hangfire job whose worker opens a FRESH DB transaction and
            // queries AppAppointmentPackets. Without OnCompleted, the
            // worker can dequeue before EnsureGeneratingAsync /
            // MarkGeneratedAsync writes are committed, see the Status row
            // as still NotStarted, and log "is not Generated; skipping"
            // (the Cat 4 P0 we hit while smoke-testing). OnCompleted runs
            // strictly after SaveChanges, eliminating the race.
            var eto = new PacketGeneratedEto
            {
                AppointmentId = appointmentId,
                TenantId = _currentTenant.Id,
                PacketId = packet.Id,
                Kind = kind,
                OccurredAt = DateTime.UtcNow,
            };
            var currentUow = _unitOfWorkManager.Current;
            if (currentUow != null)
            {
                currentUow.OnCompleted(async () => await _localEventBus.PublishAsync(eto));
            }
            else
            {
                await _localEventBus.PublishAsync(eto);
            }

            _logger.LogInformation(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} generated ({DocxBytes} bytes DOCX -> {PdfBytes} bytes PDF); PacketGeneratedEto published.",
                appointmentId, kind, docxBytes.Length, pdfBytes.Length);
        }
        // BUG-036 sub-bug 3 (defense-in-depth): include AbpDbConcurrencyException.
        // EF Core 8+ misclassifies SqlException 2601 on batched INSERT as
        // DbUpdateConcurrencyException (efcore #20649, #35043 won't-fix),
        // which ABP rewraps as AbpDbConcurrencyException. Without this in
        // the filter, future concurrency races (cross-tenant SQL ops,
        // manual janitor scripts, contention on Update paths) would escape
        // the catch, roll back the [UnitOfWork], and let Hangfire retry-
        // storm the job. The filtered unique index (sub-bug 1) closes the
        // primary trigger; this widening keeps the policy intact for
        // future edge cases.
        catch (Exception ex) when (ex is IOException
                                   || ex is InvalidOperationException
                                   || ex is ArgumentException
                                   || ex is AbpDbConcurrencyException)
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
