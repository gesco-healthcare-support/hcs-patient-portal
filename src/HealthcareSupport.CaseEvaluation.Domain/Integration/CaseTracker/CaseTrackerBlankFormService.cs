using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.Branding;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The blank patient and doctor packets the Case Tracker downloads once per office and fills from an iPad intake.
///
/// <para>A DOMAIN service for the reason <see cref="CaseTrackerFeedService"/> gives: an application service would be
/// auto-exposed at a second route with no token check. The token-gated controller is the only way in.</para>
///
/// <para><b>The ETag</b> is a hash of the template's name, the renderer's version of that template (a hash of the
/// baked HTML and its post-processing, read from the sidecar), and the office's letterhead tokens. It changes
/// exactly when a render of this form could change, and it is known BEFORE rendering, so a caller that already
/// holds the current version costs one small request to the sidecar and no render. The PDF's own bytes are not
/// hashed: they carry timestamps and would differ on every render.</para>
///
/// <para>Rendered inside the office's own scope, with only the office tokens filled. Every other token is sent as
/// an empty string, because the renderer leaves an unsent token as literal text.</para>
///
/// <para>The same gate as the feed: an unknown office and one not switched on for the Case Tracker collapse to
/// <see cref="BlankPacketFormOutcome.NotEnabled"/>, so the answer cannot be used to discover offices.</para>
/// </summary>
public class CaseTrackerBlankFormService : ITransientDependency
{
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;
    private readonly CaseTrackerDeliveryModeReader _deliveryMode;
    private readonly OfficeLetterheadResolver _letterheadResolver;
    private readonly IHtmlPacketRenderer _renderer;
    private readonly BlankPacketFormCache _cache;
    private readonly ILogger<CaseTrackerBlankFormService> _logger;

    public CaseTrackerBlankFormService(
        ITenantStore tenantStore,
        ICurrentTenant currentTenant,
        CaseTrackerDeliveryModeReader deliveryMode,
        OfficeLetterheadResolver letterheadResolver,
        IHtmlPacketRenderer renderer,
        BlankPacketFormCache cache,
        ILogger<CaseTrackerBlankFormService> logger)
    {
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
        _deliveryMode = deliveryMode;
        _letterheadResolver = letterheadResolver;
        _renderer = renderer;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>The renderer's template for a kind of blank form.</summary>
    public static string TemplateFor(BlankPacketKind kind) => kind switch
    {
        BlankPacketKind.PatientPacket => PacketTemplateNames.PatientBlank,
        BlankPacketKind.DoctorPacket => PacketTemplateNames.DoctorBlank,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blank packet kind."),
    };

    /// <summary>
    /// The blank form for <paramref name="officeId"/>, or the reason there is none. <paramref name="ifNoneMatch"/> is
    /// the raw If-None-Match header: when it names the current version the result carries no PDF.
    /// </summary>
    public virtual async Task<BlankPacketFormResult> GetAsync(
        Guid officeId,
        BlankPacketKind kind,
        string? ifNoneMatch,
        CancellationToken cancellationToken = default)
    {
        var template = TemplateFor(kind);
        if (!await IsEnabledAsync(officeId))
        {
            return BlankPacketFormResult.NotEnabled;
        }

        using (_currentTenant.Change(officeId))
        {
            var tokens = await BuildOfficeTokensAsync(cancellationToken);
            var version = await _renderer.GetTemplateVersionAsync(template, cancellationToken);
            var eTag = ComputeETag(officeId, template, version, tokens);

            if (IfNoneMatchContains(ifNoneMatch, eTag))
            {
                return new BlankPacketFormResult(BlankPacketFormOutcome.NotModified, eTag, null);
            }

            var pdf = _cache.Find(eTag);
            if (pdf == null)
            {
                pdf = await _renderer.RenderAsync(template, tokens, cancellationToken);
                _cache.Store(eTag, pdf);
            }

            return new BlankPacketFormResult(BlankPacketFormOutcome.Pdf, eTag, pdf);
        }
    }

    /// <summary>
    /// The gate, with the feed's answer for EVERY failure: the office must exist (checked in the tenant store before
    /// any scope is entered) and its Case Tracker switch must be on. An unknown office, a failed lookup and an
    /// unreadable switch all read as "not enabled", so the caller can neither tell which offices exist nor cause a 500.
    /// </summary>
    private async Task<bool> IsEnabledAsync(Guid officeId)
    {
        try
        {
            if (await _tenantStore.FindAsync(officeId) == null)
            {
                return false;
            }

            using (_currentTenant.Change(officeId))
            {
                return await _deliveryMode.IsPushSwitchOnAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ids only in the log.
            _logger.LogWarning(ex, "CaseTrackerBlankFormService: could not check office {OfficeId}; answering not enabled.", officeId);
            return false;
        }
    }

    /// <summary>
    /// Every token the templates know, empty, except the office's own. Nothing about any case is read: there is no
    /// appointment here, and the blank form must never carry one.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> BuildOfficeTokensAsync(CancellationToken cancellationToken)
    {
        var letterhead = await _letterheadResolver.ResolveAsync(cancellationToken);
        return PacketTokenMap.Build(new PacketTokenContext { Office = letterhead with { SignatureImage = string.Empty } });
    }

    /// <summary>A strong ETag (quoted) over the office, the template, the renderer's version of it, and the tokens sent.</summary>
    public static string ComputeETag(Guid officeId, string template, string rendererVersion, IReadOnlyDictionary<string, string> tokens)
    {
        var text = new StringBuilder()
            .Append(officeId.ToString("N")).Append('\n').Append(template).Append('\n').Append(rendererVersion);
        foreach (var token in tokens.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            text.Append('\n').Append(token.Key).Append('=').Append(token.Value);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return "\"" + Convert.ToHexString(hash, 0, 16).ToLowerInvariant() + "\"";
    }

    /// <summary>True when an If-None-Match header lists <paramref name="eTag"/> (weak or strong) or is "*".</summary>
    public static bool IfNoneMatchContains(string? header, string eTag)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        return header.Split(',')
            .Select(part => part.Trim())
            .Any(part => part == "*" || (part.StartsWith("W/", StringComparison.Ordinal) ? part[2..] : part) == eTag);
    }
}
