using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Issue (Path 2 of 2026-05-12 packet-pagination fix): the renderer's
/// pre-pass tightens any page margin >= 0.6" to 0.5". These tests use
/// in-memory DOCX fixtures (no embedded resources, no IO beyond the
/// in-memory stream) so the rule logic is exercised in isolation.
///
/// Twip math reminder: 1 inch = 1440 twips. 0.5" = 720 twips,
/// 0.6" = 864 twips, 1.0" = 1440 twips.
/// </summary>
public class DocxTemplateRendererMarginsUnitTests
{
    private const int OneInchTwips = 1440;
    private const int HalfInchTwips = 720;
    private const int QuarterInchTwips = 360;

    private static WordprocessingDocument CreateMinimalDoc(MemoryStream stream, PageMargin margin)
    {
        var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        var body = new Body();
        var sectPr = new SectionProperties(margin);
        body.AppendChild(sectPr);
        mainPart.Document = new Document(body);
        mainPart.Document.Save();
        return doc;
    }

    [Fact]
    public void TightenLargeMargins_OneInchMargins_ClampedToHalfInch()
    {
        using var ms = new MemoryStream();
        using (var doc = CreateMinimalDoc(ms, new PageMargin
        {
            Top = OneInchTwips,
            Bottom = OneInchTwips,
            Left = (uint)OneInchTwips,
            Right = (uint)OneInchTwips,
        }))
        {
            DocxTemplateRenderer.TightenLargeMargins(doc.MainDocumentPart!.Document!);
            var margin = doc.MainDocumentPart!.Document!.Descendants<PageMargin>().Single();
            margin.Top!.Value.ShouldBe(HalfInchTwips);
            margin.Bottom!.Value.ShouldBe(HalfInchTwips);
            margin.Left!.Value.ShouldBe((uint)HalfInchTwips);
            margin.Right!.Value.ShouldBe((uint)HalfInchTwips);
        }
    }

    [Fact]
    public void TightenLargeMargins_QuarterInchMargins_LeftUnchanged()
    {
        // 0.25" = 360 twips. Below the 0.6" threshold. Must not be expanded.
        using var ms = new MemoryStream();
        using (var doc = CreateMinimalDoc(ms, new PageMargin
        {
            Top = QuarterInchTwips,
            Bottom = QuarterInchTwips,
            Left = (uint)QuarterInchTwips,
            Right = (uint)QuarterInchTwips,
        }))
        {
            DocxTemplateRenderer.TightenLargeMargins(doc.MainDocumentPart!.Document!);
            var margin = doc.MainDocumentPart!.Document!.Descendants<PageMargin>().Single();
            margin.Top!.Value.ShouldBe(QuarterInchTwips);
            margin.Bottom!.Value.ShouldBe(QuarterInchTwips);
            margin.Left!.Value.ShouldBe((uint)QuarterInchTwips);
            margin.Right!.Value.ShouldBe((uint)QuarterInchTwips);
        }
    }

    [Fact]
    public void TightenLargeMargins_MixedMargins_OnlyOverthresholdChange()
    {
        using var ms = new MemoryStream();
        using (var doc = CreateMinimalDoc(ms, new PageMargin
        {
            Top = OneInchTwips,       // 1440 — > threshold — gets clamped
            Bottom = QuarterInchTwips, // 360 — left alone
            Left = (uint)OneInchTwips, // 1440 — gets clamped
            Right = (uint)QuarterInchTwips, // 360 — left alone
        }))
        {
            DocxTemplateRenderer.TightenLargeMargins(doc.MainDocumentPart!.Document!);
            var margin = doc.MainDocumentPart!.Document!.Descendants<PageMargin>().Single();
            margin.Top!.Value.ShouldBe(HalfInchTwips);
            margin.Bottom!.Value.ShouldBe(QuarterInchTwips);    // unchanged
            margin.Left!.Value.ShouldBe((uint)HalfInchTwips);
            margin.Right!.Value.ShouldBe((uint)QuarterInchTwips); // unchanged
        }
    }

    [Fact]
    public void TightenLargeMargins_ExactlyThreshold_GetsClamped()
    {
        // 864 twips = 0.6" — exactly on the threshold. Per the >= check
        // in the implementation, this should clamp down.
        using var ms = new MemoryStream();
        using (var doc = CreateMinimalDoc(ms, new PageMargin
        {
            Top = 864,
            Bottom = 864,
            Left = 864u,
            Right = 864u,
        }))
        {
            DocxTemplateRenderer.TightenLargeMargins(doc.MainDocumentPart!.Document!);
            var margin = doc.MainDocumentPart!.Document!.Descendants<PageMargin>().Single();
            margin.Top!.Value.ShouldBe(HalfInchTwips);
            margin.Bottom!.Value.ShouldBe(HalfInchTwips);
            margin.Left!.Value.ShouldBe((uint)HalfInchTwips);
            margin.Right!.Value.ShouldBe((uint)HalfInchTwips);
        }
    }

    [Fact]
    public void TightenLargeMargins_JustBelowThreshold_LeftUnchanged()
    {
        // 863 twips = 0.5993" — just below the 0.6" threshold.
        using var ms = new MemoryStream();
        using (var doc = CreateMinimalDoc(ms, new PageMargin
        {
            Top = 863,
            Bottom = 863,
            Left = 863u,
            Right = 863u,
        }))
        {
            DocxTemplateRenderer.TightenLargeMargins(doc.MainDocumentPart!.Document!);
            var margin = doc.MainDocumentPart!.Document!.Descendants<PageMargin>().Single();
            margin.Top!.Value.ShouldBe(863);
            margin.Bottom!.Value.ShouldBe(863);
            margin.Left!.Value.ShouldBe(863u);
            margin.Right!.Value.ShouldBe(863u);
        }
    }
}
