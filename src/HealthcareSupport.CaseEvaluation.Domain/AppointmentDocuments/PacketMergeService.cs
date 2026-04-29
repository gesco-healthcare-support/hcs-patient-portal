using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: builds the final merged PDF packet.
///
/// Input: a cover-page byte stream (always PDF, from <see cref="CoverPageGenerator"/>)
/// + an ordered list of approved documents (each with a content type +
/// stream). Approved documents are merged in declaration order.
///
/// Per-content-type handling:
///   - PDF (.pdf, application/pdf, magic 25 50 44 46): pass through; copy
///     each page from the source PDF onto the output document via
///     PdfReader + AddPage.
///   - JPG / JPEG / PNG: render onto a fresh A4 page using XImage; scale to
///     fit the page bounds preserving aspect ratio.
///   - Anything else: skipped with a warning. The upload path's magic-byte
///     guard rejects non-{PDF,JPG,PNG} so this branch is defensive.
///
/// Returns the raw bytes of the merged PDF; caller writes them to a blob.
/// </summary>
public class PacketMergeService : ITransientDependency
{
    private readonly ILogger<PacketMergeService> _logger;

    public PacketMergeService(ILogger<PacketMergeService> logger)
    {
        _logger = logger;
    }

    public byte[] Merge(byte[] coverPagePdfBytes, IEnumerable<MergeInput> approvedDocuments)
    {
        if (coverPagePdfBytes == null || coverPagePdfBytes.Length == 0)
        {
            throw new InvalidOperationException("Cover page is empty.");
        }

        using var output = new PdfDocument();
        AppendPdf(output, coverPagePdfBytes, "cover-page");

        foreach (var doc in approvedDocuments ?? Enumerable.Empty<MergeInput>())
        {
            try
            {
                AppendOne(output, doc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PacketMergeService: failed to merge document {FileName} ({ContentType}); skipped.",
                    doc.FileName, doc.ContentType);
            }
        }

        using var ms = new MemoryStream();
        output.Save(ms, false);
        return ms.ToArray();
    }

    private void AppendOne(PdfDocument output, MergeInput doc)
    {
        if (doc.Bytes == null || doc.Bytes.Length == 0)
        {
            return;
        }
        var lowerCt = (doc.ContentType ?? string.Empty).ToLowerInvariant();
        var lowerName = (doc.FileName ?? string.Empty).ToLowerInvariant();

        bool isPdf = lowerCt.Contains("pdf") || lowerName.EndsWith(".pdf");
        bool isJpg = lowerCt.Contains("jpeg") || lowerCt.Contains("jpg") || lowerName.EndsWith(".jpg") || lowerName.EndsWith(".jpeg");
        bool isPng = lowerCt.Contains("png") || lowerName.EndsWith(".png");

        var label = string.IsNullOrEmpty(doc.FileName) ? "(unnamed)" : doc.FileName;
        if (isPdf)
        {
            AppendPdf(output, doc.Bytes, label);
        }
        else if (isJpg || isPng)
        {
            AppendImage(output, doc.Bytes, label);
        }
        else
        {
            _logger.LogInformation(
                "PacketMergeService: unsupported content type {ContentType} for {FileName}; skipping.",
                doc.ContentType, label);
        }
    }

    private static void AppendPdf(PdfDocument output, byte[] pdfBytes, string label)
    {
        using var ms = new MemoryStream(pdfBytes);
        // ImportedObjectTable: open the input PDF in import mode so we can copy
        // pages into the output. PdfReader.Open with PdfDocumentOpenMode.Import
        // is the canonical PdfSharp 6.x path.
        using var input = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        for (int i = 0; i < input.PageCount; i++)
        {
            output.AddPage(input.Pages[i]);
        }
    }

    private static void AppendImage(PdfDocument output, byte[] imageBytes, string label)
    {
        var page = output.AddPage();
        // A4 default; XImage will be scaled to fit.
        using var gfx = XGraphics.FromPdfPage(page);
        using var img = XImage.FromStream(new MemoryStream(imageBytes));
        var pageW = page.Width.Point - 36; // 0.5" margins
        var pageH = page.Height.Point - 36;
        var scale = Math.Min(pageW / img.PixelWidth, pageH / img.PixelHeight);
        var w = img.PixelWidth * scale;
        var h = img.PixelHeight * scale;
        var x = (page.Width.Point - w) / 2;
        var y = (page.Height.Point - h) / 2;
        gfx.DrawImage(img, x, y, w, h);
    }

    public class MergeInput
    {
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }
}
