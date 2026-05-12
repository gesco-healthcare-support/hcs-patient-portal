using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;

/// <summary>
/// Phase 2 (2026-05-11) -- HTTP client to a Gotenberg sidecar
/// (https://gotenberg.dev) for DOCX -> PDF conversion. Multipart POST
/// against <c>/forms/libreoffice/convert</c>; response body is the
/// rendered PDF bytes.
///
/// <para>Why a sidecar instead of in-process: Gotenberg wraps headless
/// LibreOffice, which is too large to bake into the API image
/// (adds ~700 MB) and is not safely concurrent within a single soffice
/// process. Running it as a dedicated service gives crash isolation,
/// horizontal scalability, and keeps the api image lean so dotnet
/// watch rebuilds stay fast.</para>
///
/// <para>The typed HttpClient is registered in
/// <c>CaseEvaluationDomainModule.ConfigureServices</c> with the base
/// URL pulled from configuration (<c>Gotenberg:Url</c>, env var
/// <c>Gotenberg__Url</c>); the 60-second timeout matches LibreOffice's
/// worst-case rendering time on complex documents.</para>
/// </summary>
public class GotenbergDocxToPdfConverter : IDocxToPdfConverter
{
    /// <summary>DOCX MIME type per RFC 4288. Gotenberg uses the
    /// filename's extension to detect the input format -- the
    /// Content-Type header is informational only -- but we set both
    /// for correctness.</summary>
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Gotenberg's LibreOffice conversion endpoint. The route
    /// accepts one or more "files" form fields; for single-file
    /// conversion the response is the PDF binary. See
    /// https://gotenberg.dev/docs/routes#convert-with-libreoffice</summary>
    private const string ConvertRoute = "/forms/libreoffice/convert";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GotenbergDocxToPdfConverter> _logger;

    public GotenbergDocxToPdfConverter(
        HttpClient httpClient,
        ILogger<GotenbergDocxToPdfConverter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public virtual async Task<byte[]> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default)
    {
        if (docxBytes == null || docxBytes.Length == 0)
        {
            throw new ArgumentException("docxBytes must be non-empty.", nameof(docxBytes));
        }

        using var content = new MultipartFormDataContent();
        var docxPart = new ByteArrayContent(docxBytes);
        docxPart.Headers.ContentType = new MediaTypeHeaderValue(DocxContentType);
        // Gotenberg requires "files" as the field name and uses the
        // filename's .docx extension to pick the LibreOffice path.
        content.Add(docxPart, "files", "input.docx");

        using var response = await _httpClient.PostAsync(ConvertRoute, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = string.Empty;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                // Body capture is best-effort; swallow secondary failures so
                // the original HTTP error stays the actionable signal.
            }
            _logger.LogError(
                "Gotenberg conversion failed: HTTP {Status} {Reason}; body: {Body}",
                (int)response.StatusCode, response.ReasonPhrase, body);
            response.EnsureSuccessStatusCode();
        }

        var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        _logger.LogDebug(
            "Gotenberg conversion: {InputBytes} bytes DOCX -> {OutputBytes} bytes PDF.",
            docxBytes.Length, pdfBytes.Length);
        return pdfBytes;
    }
}
