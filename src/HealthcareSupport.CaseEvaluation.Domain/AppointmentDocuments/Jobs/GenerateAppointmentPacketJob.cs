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

    /// <summary>
    /// T2: when set, generate ONLY this kind. Lets the reconciliation sweep /
    /// Regenerate fix a single missing or Failed kind WITHOUT re-rendering and
    /// re-emailing the others. Null = generate all three (the on-approval default).
    /// </summary>
    public PacketKind? Kind { get; set; }

    /// <summary>
    /// Render <see cref="Kind"/> again even though it is already Generated (2026-10-21): staff changed
    /// the parties the packet names. The new file is stored under a new key, the row is pointed at it,
    /// and the previous file is kept and recorded as an <see cref="AppointmentPacketVersion"/>. The
    /// <see cref="PacketGeneratedEto"/> that follows mails only addresses that were never sent this
    /// packet, so the parties already mailed are not mailed again. Ignored without
    /// <see cref="Kind"/>, so it can never re-render and re-mail a whole set.
    /// </summary>
    public bool ReRender { get; set; }

    /// <summary>The staff member whose change asked for the re-render; recorded on the version row.</summary>
    public Guid? RequestedByUserId { get; set; }
}

/// <summary>
/// Hangfire-backed packet generation. Triggered by
/// <see cref="PacketGenerationOnApprovedHandler"/> on
/// <c>AppointmentStatusChangedEto</c> with
/// <c>ToStatus = Approved</c>, or by the office's Regenerate button.
///
/// <para>Generates all three packet kinds for every appointment, each
/// rendered to a fillable PDF by the WeasyPrint packet-renderer sidecar
/// (the DOCX -&gt; Gotenberg path was removed 2026-06-10):</para>
/// <list type="bullet">
///   <item>Patient packet -- always generated, emailed + saved.</item>
///   <item>Doctor packet -- always generated, storage-only (not emailed).</item>
///   <item>AttorneyClaimExaminer packet -- always generated; the notice
///   variant (Panel QME vs AME/IME) is chosen by appointment type.</item>
/// </list>
///
/// <para>Each kind generates in its own try/catch so a failure on one
/// kind does not block the others. Each kind gets its own
/// <see cref="AppointmentPacket"/> row keyed by the (TenantId,
/// AppointmentId, Kind) composite unique index.</para>
/// </summary>
public class GenerateAppointmentPacketJob :
    AsyncBackgroundJob<GenerateAppointmentPacketArgs>,
    ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly AppointmentPacketManager _packetManager;
    private readonly IBlobContainer<AppointmentPacketsContainer> _packetsContainer;
    private readonly IPacketTokenResolver _tokenResolver;
    private readonly IHtmlPacketRenderer _htmlPacketRenderer;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<GenerateAppointmentPacketJob> _logger;

    public GenerateAppointmentPacketJob(
        IRepository<Appointment, Guid> appointmentRepository,
        AppointmentPacketManager packetManager,
        IBlobContainer<AppointmentPacketsContainer> packetsContainer,
        IPacketTokenResolver tokenResolver,
        IHtmlPacketRenderer htmlPacketRenderer,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<GenerateAppointmentPacketJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _packetManager = packetManager;
        _packetsContainer = packetsContainer;
        _tokenResolver = tokenResolver;
        _htmlPacketRenderer = htmlPacketRenderer;
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
        //
        // T2: a specific args.Kind targets ONE kind (surgical sweep / Regenerate
        // re-drive); null generates all three (the on-approval default).
        var kindsToGenerate = args.Kind.HasValue
            ? new List<PacketKind> { args.Kind.Value }
            : new List<PacketKind>
            {
                PacketKind.Patient,
                PacketKind.Doctor,
                PacketKind.AttorneyClaimExaminer,
            };

        var reRender = args.ReRender && args.Kind.HasValue;

        var anyFailed = false;
        foreach (var kind in kindsToGenerate)
        {
            var ok = await GenerateKindAsync(args.AppointmentId, kind, context, reRender, args.RequestedByUserId);
            if (!ok)
            {
                anyFailed = true;
            }
        }

        if (anyFailed)
        {
            // 2026-10-27: a kind that failed permanently (a rendering error, not a transport one) is
            // recorded Failed and the job COMMITS. It used to throw here so Hangfire would retry, but the
            // job runs in one unit of work, so the throw rolled back the Failed row together with the
            // kinds that had just been Generated: staff saw "no packets yet" with no error, the retry
            // could not skip the kinds already done, and nothing ever recorded the failure, so the Case
            // Tracker stalled-set release (which looks for a non-Generated row) never fired. Now the
            // Failed row carries the error, staff can Regenerate it, the release sees it, and the kinds
            // that worked are kept and mailed. Transport and timeout failures still propagate from the
            // render call and get Hangfire's retry.
            _logger.LogWarning(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} finished with a Failed packet kind; "
                + "the failure is recorded on its packet row for staff to regenerate.",
                args.AppointmentId);
        }
    }

    private async Task<bool> GenerateKindAsync(
        Guid appointmentId,
        PacketKind kind,
        PacketTokenContext context,
        bool reRender,
        Guid? requestedByUserId)
    {
        var tenantSegment = _currentTenant.Id?.ToString() ?? "host";
        // The WeasyPrint sidecar returns a final PDF; the blob extension is
        // .pdf and PacketAttachmentProvider.PdfContentType matches.
        var blobName = $"{tenantSegment}/{appointmentId}/packet/{kind.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}.pdf";

        AppointmentPacket packet;
        try
        {
            packet = await _packetManager.EnsureGeneratingAsync(_currentTenant.Id, appointmentId, kind, blobName);
        }
        catch (AbpDbConcurrencyException ex)
        {
            // A concurrent worker (sweep vs live job, two sweeps, or Regenerate +
            // sweep) claimed this kind first; the filtered unique index rejects our
            // duplicate insert. Skip -- the winner finishes it. Do NOT mark Failed
            // (would clobber the winner's row) and do NOT propagate (whole-job retry
            // storm, the BUG-033/036 mechanic).
            _logger.LogInformation(ex,
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} claimed by a concurrent worker; skipping.",
                appointmentId, kind);
            return true; // not a failure -- the winner finishes this kind.
        }

        if (packet.Status == PacketGenerationStatus.Generated && reRender)
        {
            return await ReRenderGeneratedAsync(appointmentId, packet, kind, context, blobName, requestedByUserId);
        }

        if (packet.Status == PacketGenerationStatus.Generated)
        {
            // Idempotency (T1): a prior attempt already generated this kind. Skip
            // render + PacketGeneratedEto so a Hangfire retry / crash re-fetch cannot
            // re-send the packet email (duplicate PHI).
            _logger.LogInformation(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} already Generated; skipping.",
                appointmentId, kind);
            return true; // already done -- not a failure.
        }

        try
        {
            // Render via the WeasyPrint packet-renderer sidecar. Transport /
            // timeout failures intentionally propagate -- they are NOT in the
            // per-kind catch filter below, so Hangfire's retry policy re-runs
            // the whole job; only permanent rendering failures (IOException /
            // InvalidOperationException / ArgumentException) get marked Failed
            // without retry.
            var pdfBytes = await RenderHtmlPacketAsync(kind, context);

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
            await PublishAfterCommitAsync(appointmentId, packet.Id, kind);

            _logger.LogInformation(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} generated ({PdfBytes} bytes PDF); PacketGeneratedEto published.",
                appointmentId, kind, pdfBytes.Length);
            return true;
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
            // Return false: the job records it and commits (2026-10-27; see the end of
            // GenerateInsideTenantAsync). Do NOT rethrow the raw render exception here --
            // that would abort the sibling kinds mid-loop and roll back this Failed row.
            return false;
        }
    }

    /// <summary>
    /// Renders an already Generated kind again (<see cref="GenerateAppointmentPacketArgs.ReRender"/>).
    /// The row stays Generated on its previous file until the new one is stored, so the packet can be
    /// downloaded throughout and a failure leaves it exactly as it was. The new file goes under a new
    /// key and the previous one is recorded by <see cref="AppointmentPacketManager.ReplaceGeneratedFileAsync"/>;
    /// nothing is overwritten or deleted.
    /// </summary>
    private async Task<bool> ReRenderGeneratedAsync(
        Guid appointmentId,
        AppointmentPacket packet,
        PacketKind kind,
        PacketTokenContext context,
        string blobName,
        Guid? requestedByUserId)
    {
        try
        {
            // Transport / timeout failures propagate, as on first generation: Hangfire retries the
            // job, and the email waits for the new file.
            var pdfBytes = await RenderHtmlPacketAsync(kind, context);

            using var ms = new MemoryStream(pdfBytes);
            await _packetsContainer.SaveAsync(blobName, ms, overrideExisting: false);

            await _packetManager.ReplaceGeneratedFileAsync(packet.Id, blobName, requestedByUserId);

            await PublishAfterCommitAsync(appointmentId, packet.Id, kind);

            _logger.LogInformation(
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} re-rendered ({PdfBytes} bytes PDF); previous file kept; PacketGeneratedEto published.",
                appointmentId, kind, pdfBytes.Length);
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                   || ex is InvalidOperationException
                                   || ex is ArgumentException
                                   || ex is AbpDbConcurrencyException)
        {
            // The previous file is still on the row and still good, so the packet is NOT marked
            // Failed. A party added since approval is sent the packet as it stands rather than
            // nothing; the email goes once per address, so nobody already mailed is mailed again.
            // Not reported as a failure: these errors repeat on a retry, and only a job that
            // completes runs the after-commit publish that sends that email.
            _logger.LogError(ex,
                "GenerateAppointmentPacketJob: appointment {AppointmentId} kind {Kind} re-render failed; the previous file stays and is sent to any new party.",
                appointmentId, kind);
            await PublishAfterCommitAsync(appointmentId, packet.Id, kind);
            return true;
        }
    }

    /// <summary>
    /// Announces a rendered packet once the surrounding unit of work commits (see the 2026-05-11 note
    /// in <see cref="GenerateKindAsync"/> for why it must wait for the commit).
    /// </summary>
    private async Task PublishAfterCommitAsync(Guid appointmentId, Guid packetId, PacketKind kind)
    {
        var eto = new PacketGeneratedEto
        {
            AppointmentId = appointmentId,
            TenantId = _currentTenant.Id,
            PacketId = packetId,
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
    }

    /// <summary>
    /// Build the shared token map and POST {template, tokens} to the packet-renderer
    /// sidecar (which owns the templates). The AttorneyClaimExaminer kind selects its notice variant
    /// by appointment type (see <see cref="HtmlTemplateName"/>).
    /// </summary>
    private async Task<byte[]> RenderHtmlPacketAsync(PacketKind kind, PacketTokenContext context)
    {
        var templateName = HtmlTemplateName(kind, context);
        var tokens = PacketTokenMap.Build(context);
        return await _htmlPacketRenderer.RenderAsync(templateName, tokens);
    }

    /// <summary>
    /// Maps a kind (and, for AttorneyClaimExaminer, the appointment type) to the sidecar template
    /// name. Panel QME selects the DWC QME notice; AME, single QME, and any other type use the
    /// shared AME/IME notice (see <see cref="IsPanelQme"/>).
    /// </summary>
    private static string HtmlTemplateName(PacketKind kind, PacketTokenContext context) => kind switch
    {
        PacketKind.Patient => PacketTemplateNames.Patient,
        PacketKind.Doctor => PacketTemplateNames.Doctor,
        PacketKind.AttorneyClaimExaminer => IsPanelQme(context.AppointmentType)
            ? PacketTemplateNames.AttorneyPqme
            : PacketTemplateNames.AttorneyAme,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No HTML template for this PacketKind."),
    };

    /// <summary>
    /// The AttorneyClaimExaminer packet has two notice variants: the DWC Panel QME Appointment
    /// Notification Form (pqme) vs the shared AME / IME notice (ame_ime). "Panel QME" is the only
    /// panel-based appointment type (seed name "Panel QME"); AME, single QME, and any other type
    /// use the AME / IME notice. Matched case-insensitively on "PANEL" -- the resolver uppercases
    /// the type name, and OLD's Contains("PQME") gate famously missed the space in "Panel QME".
    /// </summary>
    private static bool IsPanelQme(string? appointmentTypeName) =>
        !string.IsNullOrEmpty(appointmentTypeName)
        && appointmentTypeName.Contains("PANEL", StringComparison.OrdinalIgnoreCase);
}
