using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;

/// <summary>
/// Phase 1 (2026-06-05) -- HTTP client to the packet-renderer sidecar
/// (<c>docker/packet-renderer</c>) that owns the HTML templates. POSTs a JSON body
/// <c>{ "template": "&lt;name&gt;", "tokens": { "##Group.Field##": "value", ... } }</c> to
/// <c>/render</c>; the sidecar substitutes the tokens, renders via WeasyPrint <c>--pdf-forms</c> +
/// the shared post-processing, and returns the PDF bytes.
///
/// <para>Why a sidecar: WeasyPrint + pikepdf (Python, native Pango / HarfBuzz / fontconfig deps)
/// would bloat the api image; the sidecar also keeps the HTML templates in one place (its
/// generators), so this app embeds no HTML. Mirrors the Gotenberg sidecar pattern.</para>
///
/// <para>The typed HttpClient is registered in <c>CaseEvaluationDomainModule.ConfigureServices</c>
/// with the base URL from configuration (<c>PacketRenderer:Url</c>, env var
/// <c>PacketRenderer__Url</c>); the 60-second timeout matches the worst-case render of the 15-page
/// patient packet.</para>
///
/// <para>HIPAA: the token map carries PHI (SSN / DOB). This client MUST NOT log the tokens or the
/// PDF body -- only the template name and sizes. The sidecar's error responses are its own JSON
/// status messages (never an echo of the request), so logging those is safe.</para>
/// </summary>
public class WeasyPrintPacketRenderer : IHtmlPacketRenderer
{
    /// <summary>The packet-renderer render endpoint: JSON request {template, tokens}, PDF response.</summary>
    private const string RenderRoute = "/render";

    private readonly HttpClient _httpClient;
    private readonly ILogger<WeasyPrintPacketRenderer> _logger;

    public WeasyPrintPacketRenderer(
        HttpClient httpClient,
        ILogger<WeasyPrintPacketRenderer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public virtual async Task<byte[]> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> tokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("templateName must be non-empty.", nameof(templateName));
        }
        ArgumentNullException.ThrowIfNull(tokens);

        var json = JsonSerializer.Serialize(new { template = templateName, tokens });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(RenderRoute, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = string.Empty;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                // Body capture is best-effort; swallow secondary failures so the original HTTP
                // error stays the actionable signal.
            }
            // The sidecar's error body is its own JSON status message, never the request tokens.
            _logger.LogError(
                "Packet render failed for template {Template}: HTTP {Status} {Reason}; body: {Body}",
                templateName, (int)response.StatusCode, response.ReasonPhrase, body);
            response.EnsureSuccessStatusCode();
        }

        var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        // Template name + sizes only -- never the tokens or PDF content (PHI).
        _logger.LogDebug(
            "Packet render: template {Template}, {TokenCount} tokens -> {OutputBytes} bytes PDF.",
            templateName, tokens.Count, pdfBytes.Length);
        return pdfBytes;
    }
}
