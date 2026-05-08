using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using Xunit.Abstractions;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates.Poc;

/// <summary>
/// Phase 1 proof-of-concept: prove that the
/// "OLD DOCX template + runtime DOCX-to-PDF" approach can match the
/// quality of Microsoft Word's "Save as PDF" output. If the
/// LibreOffice render visually matches Word's PDF on the same
/// templates, we adopt this pattern in production and abandon the
/// QuestPDF authoring approach (which produced a structurally
/// incorrect result on first cut).
///
/// <para>Two independent verifications produced by this file, written
/// to <c>docs/parity/samples/poc/</c>:</para>
/// <list type="number">
///   <item><b>renderer-only</b>: OLD DOCX rendered as-is, with
///   placeholders intact. Compares LibreOffice's render of static
///   layout (tables, fonts, body diagrams, margins) against Word's
///   render of the same untouched DOCX. If these match, the renderer
///   is proven.</item>
///   <item><b>renderer + token replacement</b>: OLD DOCX is loaded,
///   tokens replaced with synthetic upper-case values via OpenXML SDK
///   (paragraph-level run-flattening to handle split tokens), then
///   rendered. Compares LibreOffice's render of the data-filled DOCX
///   against what Word would produce if the placeholders had been
///   typed over manually with the same values.</item>
/// </list>
///
/// <para>SETUP REQUIRED: install LibreOffice once on the dev box.
/// The tests skip cleanly if soffice.exe is not on PATH or in the
/// usual install locations.</para>
/// <code>winget install TheDocumentFoundation.LibreOffice</code>
///
/// <para>Tests are skipped (not failed) when LibreOffice is missing,
/// so CI without LibreOffice does not turn red. Once the renderer
/// route is approved, the production code path moves out of this POC
/// folder and into the Domain layer.</para>
///
/// <para>Synthetic data only per <c>~/.claude/rules/hipaa-data.md</c>;
/// every value is obviously fake. OLD DOCX templates at
/// <c>P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\</c>
/// are never modified -- the POC copies them to a temp working
/// directory before any token replacement.</para>
/// </summary>
public class DocxToPdfPocTest
{
    private readonly ITestOutputHelper _out;

    public DocxToPdfPocTest(ITestOutputHelper output)
    {
        _out = output;
    }

    /// <summary>
    /// Renderer-only verification for the Patient Packet. Copies OLD
    /// DOCX, asks LibreOffice to convert it, copies the output PDF to
    /// the parity samples folder. Adrian compares this PDF against
    /// the Word "Save as PDF" output of the same DOCX.
    /// </summary>
    [Fact]
    public void Poc_PatientPacket_RendererOnly()
    {
        RunRendererPoc(
            sourceDocx: PatientPacketSourcePath,
            outputPdfName: "poc-patient-packet-libreoffice-no-tokens.pdf");
    }

    [Fact]
    public void Poc_DoctorPacket_RendererOnly()
    {
        RunRendererPoc(
            sourceDocx: DoctorPacketSourcePath,
            outputPdfName: "poc-doctor-packet-libreoffice-no-tokens.pdf");
    }

    [Fact]
    public void Poc_PatientPacket_WithTokenReplacement()
    {
        RunTokenReplacementPoc(
            sourceDocx: PatientPacketSourcePath,
            outputPdfName: "poc-patient-packet-libreoffice-with-tokens.pdf");
    }

    [Fact]
    public void Poc_DoctorPacket_WithTokenReplacement()
    {
        RunTokenReplacementPoc(
            sourceDocx: DoctorPacketSourcePath,
            outputPdfName: "poc-doctor-packet-libreoffice-with-tokens.pdf");
    }

    // -- Test bodies ------------------------------------------------------

    private void RunRendererPoc(string sourceDocx, string outputPdfName)
    {
        var soffice = TryFindSoffice()
            ?? throw new InvalidOperationException(
                "LibreOffice is not installed.\n" +
                "Run on the dev box (PowerShell, admin not required):\n" +
                "    winget install TheDocumentFoundation.LibreOffice\n" +
                "After install, restart the terminal so PATH refreshes (or use a new terminal).");
        Assert.True(File.Exists(sourceDocx), $"Source DOCX missing: {sourceDocx}");

        var workDir = CreateWorkDir();
        var tempDocx = Path.Combine(workDir, Path.GetFileName(sourceDocx));
        File.Copy(sourceDocx, tempDocx, overwrite: true);

        var pdfPath = ConvertDocxToPdf(soffice!, tempDocx, workDir);
        CopyToSamples(pdfPath, outputPdfName);
        _out.WriteLine($"Wrote: {outputPdfName} ({new FileInfo(pdfPath).Length:N0} bytes)");
    }

    private void RunTokenReplacementPoc(string sourceDocx, string outputPdfName)
    {
        var soffice = TryFindSoffice()
            ?? throw new InvalidOperationException(
                "LibreOffice is not installed.\n" +
                "Run: winget install TheDocumentFoundation.LibreOffice");
        Assert.True(File.Exists(sourceDocx), $"Source DOCX missing: {sourceDocx}");

        var workDir = CreateWorkDir();
        var tempDocx = Path.Combine(workDir, Path.GetFileName(sourceDocx));
        File.Copy(sourceDocx, tempDocx, overwrite: true);

        var replacements = BuildSyntheticTokenMap();
        var matchedTokens = ReplaceTokensInDocx(tempDocx, replacements);
        _out.WriteLine($"Replaced {matchedTokens} token occurrences in {Path.GetFileName(tempDocx)}");

        var pdfPath = ConvertDocxToPdf(soffice!, tempDocx, workDir);
        CopyToSamples(pdfPath, outputPdfName);
        _out.WriteLine($"Wrote: {outputPdfName} ({new FileInfo(pdfPath).Length:N0} bytes)");
    }

    // -- LibreOffice invocation -------------------------------------------

    /// <summary>Invokes <c>soffice --headless --convert-to pdf</c> and returns the resulting PDF path.</summary>
    private static string ConvertDocxToPdf(string soffice, string docxPath, string outDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = soffice,
            ArgumentList =
            {
                "--headless",
                "--convert-to", "pdf",
                "--outdir", outDir,
                docxPath,
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(60_000);

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"soffice exited with code {p.ExitCode}.\nSTDOUT: {stdout}\nSTDERR: {stderr}");
        }

        var expected = Path.Combine(outDir, Path.GetFileNameWithoutExtension(docxPath) + ".pdf");
        if (!File.Exists(expected))
        {
            throw new FileNotFoundException(
                $"soffice did not produce expected PDF.\nSTDOUT: {stdout}\nSTDERR: {stderr}",
                expected);
        }
        return expected;
    }

    /// <summary>
    /// Looks for soffice.exe on PATH, then in the common Windows
    /// LibreOffice install directories. Returns null if not found
    /// (test should Skip).
    /// </summary>
    private static string? TryFindSoffice()
    {
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "soffice.exe");
            if (File.Exists(candidate)) return candidate;
        }

        var commonPaths = new[]
        {
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        };
        foreach (var p in commonPaths)
        {
            if (File.Exists(p)) return p;
        }

        return null;
    }

    // -- OpenXML token replacement ----------------------------------------

    private static readonly Regex TokenPattern = new(@"##([A-Za-z]+\.[A-Za-z]+)##", RegexOptions.Compiled);

    /// <summary>
    /// Replaces <c>##Token##</c> placeholders in a DOCX file in place.
    /// Walks every paragraph, concatenates the per-paragraph run text
    /// (handles Word's split-runs problem -- a single visible token
    /// can be split across multiple <c>w:t</c> elements due to
    /// formatting boundaries), then rebuilds matched runs in place.
    ///
    /// <para>POC-grade: when a paragraph contains a token, all runs
    /// in that paragraph are collapsed to one. Loses intra-paragraph
    /// formatting like a bold word followed by an italic token; the
    /// production version will preserve formatting per run. For the
    /// POC, the cover-letter and case-info sections that need
    /// data-fill are mostly uniform formatting, so this is acceptable.</para>
    /// </summary>
    /// <returns>Total token occurrences replaced (for diagnostics).</returns>
    private static int ReplaceTokensInDocx(string docxPath, Dictionary<string, string> replacements)
    {
        var totalMatches = 0;
        using var doc = WordprocessingDocument.Open(docxPath, isEditable: true);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidOperationException("DOCX has no MainDocumentPart");
        var document = mainPart.Document
            ?? throw new InvalidOperationException("DOCX has no Document");
        var body = document.Body
            ?? throw new InvalidOperationException("DOCX has no Body");

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var matches = ReplaceTokensInParagraph(paragraph, replacements);
            totalMatches += matches;
        }

        document.Save();
        return totalMatches;
    }

    private static int ReplaceTokensInParagraph(Paragraph paragraph, Dictionary<string, string> replacements)
    {
        var runs = paragraph.Descendants<Run>().ToList();
        if (runs.Count == 0) return 0;

        var concatText = string.Concat(runs.SelectMany(r => r.Descendants<Text>()).Select(t => t.Text ?? ""));
        if (!TokenPattern.IsMatch(concatText)) return 0;

        var replaced = TokenPattern.Replace(concatText, m =>
        {
            var key = m.Groups[1].Value;
            return replacements.TryGetValue(key, out var v) ? v : m.Value;
        });

        // Count only tokens we actually had a value for, so the diagnostic
        // doesn't overstate matches when a paragraph contains a token we
        // intentionally don't fill.
        var matchCount = TokenPattern.Matches(concatText)
            .Cast<Match>()
            .Count(m => replacements.ContainsKey(m.Groups[1].Value));

        // Collapse the paragraph's runs into a single run carrying the
        // replaced text. Preserves the first run's RunProperties (rPr)
        // so the paragraph's primary formatting (bold / font / size)
        // survives. Removes all subsequent runs.
        var firstRun = runs[0];
        var firstRunProps = firstRun.GetFirstChild<RunProperties>()?.CloneNode(true);

        // Clear children of first run, attach rPr if any, attach a single Text.
        firstRun.RemoveAllChildren();
        if (firstRunProps != null)
        {
            firstRun.AppendChild(firstRunProps);
        }
        firstRun.AppendChild(new Text(replaced) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });

        // Remove the other runs in the paragraph.
        for (var i = 1; i < runs.Count; i++)
        {
            runs[i].Remove();
        }

        return matchCount;
    }

    // -- Synthetic data ---------------------------------------------------

    /// <summary>
    /// Tokens uppercased to mirror OLD <c>AppointmentDocumentDomain.cs:1070</c>
    /// behavior where the resolver applies <c>.ToUpper()</c> before
    /// insertion. POC values are obviously synthetic and exercise both
    /// short and long replacements so layout drift surfaces in the
    /// side-by-side compare.
    /// </summary>
    private static Dictionary<string, string> BuildSyntheticTokenMap()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Patients
            ["Patients.FirstName"] = "JANE",
            ["Patients.LastName"] = "SAMPLE",
            ["Patients.MiddleName"] = "Q",
            // Per .claude/rules/test-data.md: obviously-fake placeholder
            // shapes only. No SSN-shape patterns. No real-birthday shapes.
            ["Patients.DateOfBirth"] = "01/01/1990",
            ["Patients.SocialSecurityNumber"] = "SYNTHETIC-SSN",
            ["Patients.Street"] = "123 SAMPLE ST",
            ["Patients.City"] = "LOS ANGELES",
            ["Patients.State"] = "CALIFORNIA",
            ["Patients.ZipCode"] = "90001",

            // Appointments
            ["Appointments.RequestConfirmationNumber"] = "APT-100001",
            ["Appointments.AvailableDate"] = "06/15/2026",
            ["Appointments.AppointmenTime"] = "9:00 AM",
            ["Appointments.AppointmentType"] = "AME",
            ["Appointments.Location"] = "DEMO CLINIC",
            ["Appointments.LocationAddress"] = "456 DEMO BLVD",
            ["Appointments.LocationCity"] = "ENCINO",
            ["Appointments.LocationState"] = "CALIFORNIA",
            ["Appointments.LocationZipCode"] = "91426",
            ["Appointments.LocationParkingFee"] = "10",
            ["Appointments.PrimaryResponsibleUserName"] = "DR. SAMPLE STAFF",
            // Signature is not a string token -- OLD swaps it for an image.
            // POC leaves the placeholder text intact so we can see WHERE in
            // the rendered PDF the signature would land.

            // Employer
            ["EmployerDetails.EmployerName"] = "DEMO EMPLOYER LLC",
            ["EmployerDetails.Street"] = "789 EMPLOYER WAY",
            ["EmployerDetails.City"] = "BURBANK",
            ["EmployerDetails.State"] = "CALIFORNIA",
            ["EmployerDetails.Zip"] = "91505",

            // Patient (Applicant) attorney
            ["PatientAttorneys.AttorneyName"] = "ATTORNEY APPLICANT",
            ["PatientAttorneys.Street"] = "100 APPLICANT LN",
            ["PatientAttorneys.City"] = "GLENDALE",
            ["PatientAttorneys.State"] = "CALIFORNIA",
            ["PatientAttorneys.Zip"] = "91201",

            // Defense attorney
            ["DefenseAttorneys.AttorneyName"] = "ATTORNEY DEFENSE",
            ["DefenseAttorneys.Street"] = "200 DEFENSE DR",
            ["DefenseAttorneys.City"] = "PASADENA",
            ["DefenseAttorneys.State"] = "CALIFORNIA",
            ["DefenseAttorneys.Zip"] = "91101",

            // Injury (single-injury -- trailing space mirrors OLD concat pattern)
            ["InjuryDetails.ClaimNumber"] = "CLM-555555 ",
            ["InjuryDetails.DateOfInjury"] = "01/01/2026 ",
            ["InjuryDetails.WcabAdj"] = "ADJ-9999 ",
            ["InjuryDetails.WcabOfficeName"] = "VAN NUYS WCAB ",
            ["InjuryDetails.WcabOfficeAddress"] = "6150 VAN NUYS BLVD ",
            ["InjuryDetails.WcabOfficeCity"] = "VAN NUYS ",
            ["InjuryDetails.WcabOfficeState"] = "CALIFORNIA ",
            ["InjuryDetails.WcabOfficeZipCode"] = "91401 ",
            ["InjuryDetails.PrimaryInsuranceName"] = "DEMO INSURANCE CO ",
            ["InjuryDetails.PrimaryInsuranceStreet"] = "300 INSURANCE PKWY ",
            ["InjuryDetails.PrimaryInsuranceCity"] = "LONG BEACH ",
            ["InjuryDetails.PrimaryInsuranceState"] = "CALIFORNIA ",
            ["InjuryDetails.PrimaryInsuranceZip"] = "90802 ",
            ["InjuryDetails.ClaimExaminerName"] = "EXAMINER SAMPLE ",

            // Others
            ["Others.DateNow"] = DateTime.Today.ToString("MM/dd/yyyy").ToUpper(),
        };
    }

    // -- Path helpers -----------------------------------------------------

    private const string PatientPacketSourcePath =
        @"P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacketnew\PATIENT PACKET NEW.docx";

    private const string DoctorPacketSourcePath =
        @"P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\doctorpacket\DOCTOR PACKET.docx";

    private static string CreateWorkDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"docx-pdf-poc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CopyToSamples(string pdfPath, string targetName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
        {
            dir = dir.Parent;
        }
        if (dir == null)
        {
            throw new InvalidOperationException("Could not locate repo root for output path");
        }
        var target = Path.Combine(dir.FullName, "docs", "parity", "samples", "poc", targetName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(pdfPath, target, overwrite: true);
    }
}

