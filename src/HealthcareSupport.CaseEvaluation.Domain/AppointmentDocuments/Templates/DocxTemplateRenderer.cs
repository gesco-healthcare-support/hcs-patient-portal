using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Volo.Abp.DependencyInjection;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Default OpenXml-based renderer. Three-pass strategy:
///
/// <list type="number">
///   <item>Reflect over <see cref="PacketTokenContext"/> string properties
///   to build a <c>##Group.Field## -&gt; value</c> map. The XML doc-comments
///   hold the OLD token name on each property; the map mirrors that.</item>
///   <item>For every <c>&lt;w:p&gt;</c> in main body + headers + footers,
///   reconstruct the paragraph's flat text from its descendant Text
///   elements and replace tokens at run granularity. Replacements are
///   applied right-to-left so earlier offsets stay valid.</item>
///   <item>If <see cref="PacketTokenContext.ResponsibleUserSignature"/> is
///   non-null, find the <c>##Appointments.Signature##</c> placeholder
///   (which the token-replace pass intentionally leaves intact) and
///   stamp an inline image at OLD's exact 880000 x 880000 EMU
///   (~0.96 inch square). When the signature is null, the placeholder
///   text is silently cleared (OLD parity).</item>
/// </list>
/// </summary>
public class DocxTemplateRenderer : IDocxTemplateRenderer, ITransientDependency
{
    /// <summary>OLD's exact image dimensions: 880000 EMU == 0.962 inches.</summary>
    private const long SignatureWidthEmu = 880000L;
    private const long SignatureHeightEmu = 880000L;

    /// <summary>The signature placeholder is excluded from the string-token
    /// map and handled separately by <see cref="StampSignature"/>.</summary>
    private const string SignaturePlaceholder = "##Appointments.Signature##";

    private static readonly Regex TokenRegex = new(
        @"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##",
        RegexOptions.Compiled);

    /// <summary>
    /// 2026-05-12 (Path 2 of packet-pagination fix): conservative
    /// margin-tightening threshold and target. Any per-section page
    /// margin currently >= 0.6 inch (~548640 EMU) gets clamped to
    /// 0.5 inch (~457200 EMU). Smaller margins are left alone so we
    /// don't expand intentionally-tight sections. Empirically this
    /// drops residual overflow pages by ~50% on top of Option A's
    /// font-substitution fix (see PR-91 measurements):
    ///
    ///   AttyCE  : 7 -> 6 pages (0 near-empty)
    ///   Doctor  : 9 -> 9 pages (1 trailing blank page from a
    ///             section break, NOT a margin issue)
    ///   Patient : 23 -> 21 pages (2 near-empty, down from 4)
    ///
    /// Implementation: pre-pass before token replacement. Walks every
    /// w:sectPr in the main body (and any nested section-properties in
    /// final paragraphs) and tightens w:top, w:bottom, w:left, w:right,
    /// and w:gutter when present.
    /// </summary>
    private const long MarginThresholdEmu = 548640L;  // 0.6"
    private const long MarginTargetEmu = 457200L;     // 0.5"

    /// <summary>OpenXml stores w:top etc. as twentieths of a point
    /// (twips). Convert to EMU at 914400 EMU per inch == 20 twips per
    /// point * 72 points per inch = 1440 twips per inch. So 1 twip =
    /// 914400/1440 = 635 EMU. We compare in EMU for self-documentation
    /// but the raw value on disk is twips.</summary>
    private const long EmuPerTwip = 635L;

    public virtual byte[] Render(byte[] templateBytes, PacketTokenContext context)
    {
        // Copy the template into a writable MemoryStream so we don't mutate
        // the caller's array (templates are embedded resources -- single
        // shared instance).
        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            // 2026-05-12: tighten page margins before token replacement so
            // LibreOffice's font-metric drift has more usable area per
            // page to absorb. Reduces overflow pages from ~15% to ~8%
            // total after Option A fonts. See class-level comment on
            // MarginThresholdEmu for the rationale.
            TightenLargeMargins(doc.MainDocumentPart!.Document);

            var tokenMap = BuildTokenMap(context);

            ReplaceTokensInPart(doc.MainDocumentPart!.Document, tokenMap);
            foreach (var headerPart in doc.MainDocumentPart!.HeaderParts)
            {
                ReplaceTokensInPart(headerPart.Header, tokenMap);
            }
            foreach (var footerPart in doc.MainDocumentPart!.FooterParts)
            {
                ReplaceTokensInPart(footerPart.Footer, tokenMap);
            }

            StampSignature(doc, context.ResponsibleUserSignature);

            doc.MainDocumentPart!.Document.Save();
            foreach (var headerPart in doc.MainDocumentPart!.HeaderParts)
            {
                headerPart.Header.Save();
            }
            foreach (var footerPart in doc.MainDocumentPart!.FooterParts)
            {
                footerPart.Footer.Save();
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Walk every w:sectPr in the document and reduce any margin
    /// currently >= MarginThresholdEmu (0.6") down to MarginTargetEmu
    /// (0.5"). Section properties that already have tighter margins
    /// stay unchanged. Internal so the unit tests can drive it
    /// directly.
    /// </summary>
    internal static void TightenLargeMargins(OpenXmlElement root)
    {
        var thresholdTwips = MarginThresholdEmu / EmuPerTwip;
        var targetTwips = MarginTargetEmu / EmuPerTwip;

        foreach (var pageMargin in root.Descendants<PageMargin>().ToList())
        {
            if (pageMargin.Top?.Value is int topValue && topValue >= thresholdTwips)
            {
                pageMargin.Top = (int)targetTwips;
            }
            if (pageMargin.Bottom?.Value is int bottomValue && bottomValue >= thresholdTwips)
            {
                pageMargin.Bottom = (int)targetTwips;
            }
            // Left / Right / Gutter / Header / Footer use UInt32Value.
            // Top + Bottom are Int32Value because Word allows negative
            // values (content extending into the margin); the spec
            // distinguishes them.
            if (pageMargin.Left?.Value is uint leftValue && leftValue >= (uint)thresholdTwips)
            {
                pageMargin.Left = (uint)targetTwips;
            }
            if (pageMargin.Right?.Value is uint rightValue && rightValue >= (uint)thresholdTwips)
            {
                pageMargin.Right = (uint)targetTwips;
            }
        }
    }

    // -- Token replacement --------------------------------------------------

    private static void ReplaceTokensInPart(OpenXmlElement root, IReadOnlyDictionary<string, string> tokenMap)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            ReplaceTokensInParagraph(paragraph, tokenMap);
        }
    }

    private static void ReplaceTokensInParagraph(Paragraph paragraph, IReadOnlyDictionary<string, string> tokenMap)
    {
        var texts = paragraph.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            return;
        }

        var combined = string.Concat(texts.Select(t => t.Text ?? string.Empty));
        var matches = TokenRegex.Matches(combined);
        if (matches.Count == 0)
        {
            return;
        }

        // Map every Text element to its [start, end) offset in the combined
        // string. We mutate texts in place so the offsets stay valid only
        // while we apply matches right-to-left.
        var offsets = new List<(Text element, int start, int end)>(texts.Count);
        var cursor = 0;
        foreach (var t in texts)
        {
            var len = t.Text?.Length ?? 0;
            offsets.Add((t, cursor, cursor + len));
            cursor += len;
        }

        foreach (Match match in matches.Cast<Match>().OrderByDescending(m => m.Index))
        {
            var key = match.Value;

            // The signature placeholder is intentionally untouched here --
            // StampSignature replaces it with an inline image after this
            // pass. If the signature is null at runtime, the placeholder
            // is cleared by StampSignature instead.
            if (key == SignaturePlaceholder)
            {
                continue;
            }

            if (!tokenMap.TryGetValue(key, out var replacement))
            {
                // Unknown tokens left as-is. OLD's reflection lookup at
                // GetColumnValues:1066-1071 produces "" for unknown columns;
                // we surface them as the literal placeholder so visual
                // diff catches the mapping gap rather than swallowing it.
                continue;
            }

            var matchStart = match.Index;
            var matchEnd = match.Index + match.Length;
            var firstWritten = false;

            foreach (var (element, start, end) in offsets)
            {
                if (end <= matchStart || start >= matchEnd)
                {
                    continue;
                }

                var oldText = element.Text ?? string.Empty;
                var localStart = System.Math.Max(0, matchStart - start);
                var localEnd = System.Math.Min(oldText.Length, matchEnd - start);
                var before = oldText.Substring(0, localStart);
                var after = oldText.Substring(localEnd);

                if (!firstWritten)
                {
                    element.Text = before + replacement + after;
                    firstWritten = true;
                }
                else
                {
                    element.Text = before + after;
                }
                // Preserve leading/trailing spaces in the rendered value
                // (OLD's space-concat injects trailing spaces on injury
                // tokens). Without xml:space="preserve" Word collapses
                // them on save.
                element.Space = SpaceProcessingModeValues.Preserve;
            }
        }
    }

    // -- Signature stamping -------------------------------------------------

    private static void StampSignature(WordprocessingDocument doc, byte[]? signatureBytes)
    {
        var mainPart = doc.MainDocumentPart!;

        // Find the placeholder Text element. Run-aware matching: the
        // placeholder may have been split across multiple Text elements,
        // but at this point token replacement has already merged
        // run-fragments at the paragraph level for matched tokens. The
        // signature placeholder was deliberately skipped, so it remains
        // wherever Word originally split it. We locate it by scanning
        // text-flat per paragraph.
        Paragraph? targetParagraph = null;
        Text? firstPlaceholderText = null;
        var placeholderTexts = new List<Text>();

        foreach (var paragraph in mainPart.Document.Body!.Descendants<Paragraph>())
        {
            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count == 0) continue;

            var combined = string.Concat(texts.Select(t => t.Text ?? string.Empty));
            var idx = combined.IndexOf(SignaturePlaceholder, System.StringComparison.Ordinal);
            if (idx < 0) continue;

            // Collect contributing Text elements
            targetParagraph = paragraph;
            firstPlaceholderText = null;
            placeholderTexts.Clear();

            var cursor = 0;
            var placeholderEnd = idx + SignaturePlaceholder.Length;
            foreach (var t in texts)
            {
                var len = t.Text?.Length ?? 0;
                if (cursor + len > idx && cursor < placeholderEnd)
                {
                    placeholderTexts.Add(t);
                    if (firstPlaceholderText == null) firstPlaceholderText = t;
                }
                cursor += len;
            }
            break;
        }

        if (targetParagraph == null || firstPlaceholderText == null)
        {
            return;
        }

        // Strip the placeholder string from the contributing Text elements
        // by collapsing all the placeholder's host Texts into the first one,
        // minus the placeholder substring itself.
        var combinedAll = string.Concat(placeholderTexts.Select(t => t.Text ?? string.Empty));
        var phIdx = combinedAll.IndexOf(SignaturePlaceholder, System.StringComparison.Ordinal);
        var before = combinedAll.Substring(0, phIdx);
        var after = combinedAll.Substring(phIdx + SignaturePlaceholder.Length);

        firstPlaceholderText.Text = before + after;
        firstPlaceholderText.Space = SpaceProcessingModeValues.Preserve;
        for (var i = 1; i < placeholderTexts.Count; i++)
        {
            placeholderTexts[i].Text = string.Empty;
            placeholderTexts[i].Space = SpaceProcessingModeValues.Preserve;
        }

        if (signatureBytes == null || signatureBytes.Length == 0)
        {
            // OLD silent-skip: placeholder text removed, no image inserted.
            return;
        }

        // Detect PNG vs JPEG from magic bytes; OLD hardcodes PNG even for
        // JPEG which works only because the OpenXml SDK is forgiving.
        // Doing it right is no harder.
        var imagePartType = ImagePartType.Png;
        if (signatureBytes.Length >= 3
            && signatureBytes[0] == 0xFF
            && signatureBytes[1] == 0xD8
            && signatureBytes[2] == 0xFF)
        {
            imagePartType = ImagePartType.Jpeg;
        }

        var imagePart = mainPart.AddImagePart(imagePartType);
        using (var imgStream = new MemoryStream(signatureBytes))
        {
            imagePart.FeedData(imgStream);
        }
        var imageId = mainPart.GetIdOfPart(imagePart);

        var drawing = BuildSignatureDrawing(imageId, SignatureWidthEmu, SignatureHeightEmu);

        // Insert the Drawing wrapped in a Run in the parent of the first
        // placeholder Text -- typically a Run that we keep around to
        // preserve formatting (font, size, etc.). We append the Drawing
        // to that run; if the run already has visible text from "before"
        // or "after", the image renders inline alongside.
        var hostingRun = firstPlaceholderText.Parent as Run;
        if (hostingRun == null)
        {
            // Fallback: append a fresh run to the paragraph.
            targetParagraph.AppendChild(new Run(drawing));
            return;
        }
        hostingRun.AppendChild(drawing);
    }

    private static Drawing BuildSignatureDrawing(string imageId, long widthEmu, long heightEmu)
    {
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = (UInt32Value)1U, Name = "Signature" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)0U, Name = "Signature" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = imageId, CompressionState = A.BlipCompressionValues.Print },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                    })
            )
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });
    }

    // -- Token map construction ---------------------------------------------

    /// <summary>
    /// Reflects over PacketTokenContext's string properties and builds the
    /// <c>##Group.Field##</c> -&gt; value map. The mapping below mirrors
    /// the OLD token names to PacketTokenContext property names recorded
    /// in the audit doc Section 5.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildTokenMap(PacketTokenContext c)
    {
        return new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // Patients group
            ["##Patients.FirstName##"] = c.PatientFirstName,
            ["##Patients.LastName##"] = c.PatientLastName,
            ["##Patients.MiddleName##"] = c.PatientMiddleName,
            ["##Patients.DateOfBirth##"] = c.PatientDateOfBirth,
            ["##Patients.SocialSecurityNumber##"] = c.PatientSocialSecurityNumber,
            ["##Patients.Street##"] = c.PatientStreet,
            ["##Patients.City##"] = c.PatientCity,
            ["##Patients.State##"] = c.PatientState,
            ["##Patients.ZipCode##"] = c.PatientZipCode,
            ["##Patients.PhoneNumber##"] = c.PatientPhoneNumber,

            // Appointments group (Signature is intentionally omitted -- handled by StampSignature)
            ["##Appointments.RequestConfirmationNumber##"] = c.RequestConfirmationNumber,
            ["##Appointments.AvailableDate##"] = c.AvailableDate,
            ["##Appointments.AppointmenTime##"] = c.AppointmentTime,    // typo preserved verbatim from OLD
            ["##Appointments.AppointmentType##"] = c.AppointmentType,
            ["##Appointments.Location##"] = c.LocationName,
            ["##Appointments.LocationAddress##"] = c.LocationAddress,
            ["##Appointments.LocationCity##"] = c.LocationCity,
            ["##Appointments.LocationState##"] = c.LocationState,
            ["##Appointments.LocationZipCode##"] = c.LocationZipCode,
            ["##Appointments.LocationParkingFee##"] = c.LocationParkingFee,
            ["##Appointments.PrimaryResponsibleUserName##"] = c.PrimaryResponsibleUserName,
            ["##Appointments.AppointmentCreatedDate##"] = c.AppointmentCreatedDate,
            ["##Appointments.PanelNumber##"] = c.PanelNumber,

            // EmployerDetails group
            ["##EmployerDetails.EmployerName##"] = c.EmployerName,
            ["##EmployerDetails.Street##"] = c.EmployerStreet,
            ["##EmployerDetails.City##"] = c.EmployerCity,
            ["##EmployerDetails.State##"] = c.EmployerState,
            ["##EmployerDetails.Zip##"] = c.EmployerZip,

            // PatientAttorneys group
            ["##PatientAttorneys.AttorneyName##"] = c.PatientAttorneyName,
            ["##PatientAttorneys.Street##"] = c.PatientAttorneyStreet,
            ["##PatientAttorneys.City##"] = c.PatientAttorneyCity,
            ["##PatientAttorneys.State##"] = c.PatientAttorneyState,
            ["##PatientAttorneys.Zip##"] = c.PatientAttorneyZip,

            // DefenseAttorneys group
            ["##DefenseAttorneys.AttorneyName##"] = c.DefenseAttorneyName,
            ["##DefenseAttorneys.Street##"] = c.DefenseAttorneyStreet,
            ["##DefenseAttorneys.City##"] = c.DefenseAttorneyCity,
            ["##DefenseAttorneys.State##"] = c.DefenseAttorneyState,
            ["##DefenseAttorneys.Zip##"] = c.DefenseAttorneyZip,

            // InjuryDetails group (multi-row space-concatenated by the resolver)
            ["##InjuryDetails.ClaimNumber##"] = c.InjuryClaimNumber,
            ["##InjuryDetails.DateOfInjury##"] = c.InjuryDateOfInjury,
            ["##InjuryDetails.WcabAdj##"] = c.InjuryWcabAdj,
            ["##InjuryDetails.WcabOfficeName##"] = c.InjuryWcabOfficeName,
            ["##InjuryDetails.WcabOfficeAddress##"] = c.InjuryWcabOfficeAddress,
            ["##InjuryDetails.WcabOfficeCity##"] = c.InjuryWcabOfficeCity,
            ["##InjuryDetails.WcabOfficeState##"] = c.InjuryWcabOfficeState,
            ["##InjuryDetails.WcabOfficeZipCode##"] = c.InjuryWcabOfficeZipCode,
            ["##InjuryDetails.PrimaryInsuranceName##"] = c.InjuryPrimaryInsuranceName,
            ["##InjuryDetails.PrimaryInsuranceStreet##"] = c.InjuryPrimaryInsuranceStreet,
            ["##InjuryDetails.PrimaryInsuranceCity##"] = c.InjuryPrimaryInsuranceCity,
            ["##InjuryDetails.PrimaryInsuranceState##"] = c.InjuryPrimaryInsuranceState,
            ["##InjuryDetails.PrimaryInsuranceZip##"] = c.InjuryPrimaryInsuranceZip,
            ["##InjuryDetails.PrimaryInsurancePhoneNumber##"] = c.InjuryPrimaryInsurancePhoneNumber,
            ["##InjuryDetails.ClaimExaminerName##"] = c.InjuryClaimExaminerName,
            ["##InjuryDetails.ClaimExaminerStreet##"] = c.InjuryClaimExaminerStreet,
            ["##InjuryDetails.ClaimExaminerCity##"] = c.InjuryClaimExaminerCity,
            ["##InjuryDetails.ClaimExaminerState##"] = c.InjuryClaimExaminerState,
            ["##InjuryDetails.ClaimExaminerZip##"] = c.InjuryClaimExaminerZip,
            ["##InjuryDetails.ClaimExaminerPhoneNumber##"] = c.InjuryClaimExaminerPhoneNumber,

            // Others group
            ["##Others.DateNow##"] = c.DateNow,
        };
    }
}
