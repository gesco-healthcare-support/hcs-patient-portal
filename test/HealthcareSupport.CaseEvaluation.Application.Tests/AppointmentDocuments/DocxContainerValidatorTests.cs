using System.IO;
using System.IO.Compression;
using System.Text;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Item G (2026-08-22) -- the container check that lets Word uploads in safely.
///
/// <para>Adrian's stated worry was "injection scripts hidden", which in Word means macros. The
/// extension allow-list alone cannot address that, because renaming a <c>.docm</c> to <c>.docx</c>
/// takes one keystroke and OOXML's magic bytes are just <c>PK\x03\x04</c>, identical to any zip.
/// These tests build REAL zip containers in memory so the macro case is exercised as an attacker
/// would actually present it -- a macro-bearing package wearing the allowed extension.</para>
/// </summary>
public class DocxContainerValidatorTests
{
    [Fact]
    public void Inspect_MinimalValidDocx_IsValid()
    {
        using var stream = BuildZip(
            ("[Content_Types].xml", "<Types/>"),
            ("word/document.xml", "<w:document/>"),
            ("_rels/.rels", "<Relationships/>"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.Valid);
    }

    /// <summary>
    /// THE case this whole check exists for: a macro-bearing package renamed to .docx. The extension
    /// is allowed and the magic bytes are a valid zip header, so nothing before this point can
    /// refuse it.
    /// </summary>
    [Fact]
    public void Inspect_MacroBearingPackageRenamedToDocx_IsMacroEnabled()
    {
        using var stream = BuildZip(
            ("[Content_Types].xml", "<Types/>"),
            ("word/document.xml", "<w:document/>"),
            ("word/vbaProject.bin", "MZ-not-really-but-the-name-is-what-matters"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.MacroEnabled);
    }

    [Fact]
    public void Inspect_VbaProjectAtAnUnexpectedPath_IsStillMacroEnabled()
    {
        // Checked on the entry name wherever it sits, not only at word/.
        using var stream = BuildZip(
            ("[Content_Types].xml", "<Types/>"),
            ("word/document.xml", "<w:document/>"),
            ("customUI/nested/vbaProject.bin", "x"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.MacroEnabled);
    }

    [Fact]
    public void Inspect_VbaProjectInDifferentCase_IsStillMacroEnabled()
    {
        using var stream = BuildZip(
            ("[Content_Types].xml", "<Types/>"),
            ("word/document.xml", "<w:document/>"),
            ("word/VBAProject.BIN", "x"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.MacroEnabled);
    }

    [Fact]
    public void Inspect_PlainZipWithoutWordParts_IsNotWordDocument()
    {
        using var stream = BuildZip(("notes.txt", "just a zip of something"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.NotWordDocument);
    }

    [Fact]
    public void Inspect_SpreadsheetRenamedToDocx_IsNotWordDocument()
    {
        // A real .xlsx has [Content_Types].xml but no word/document.xml, so requiring BOTH parts is
        // what separates the two.
        using var stream = BuildZip(
            ("[Content_Types].xml", "<Types/>"),
            ("xl/workbook.xml", "<workbook/>"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.NotWordDocument);
    }

    [Fact]
    public void Inspect_MissingContentTypes_IsNotWordDocument()
    {
        using var stream = BuildZip(("word/document.xml", "<w:document/>"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.NotWordDocument);
    }

    [Fact]
    public void Inspect_NotAZipAtAll_IsNotAZip()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("PK but then garbage"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.NotAZip);
    }

    [Fact]
    public void Inspect_EmptyStream_IsNotAZip()
    {
        using var stream = new MemoryStream();

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.NotAZip);
    }

    [Fact]
    public void Inspect_MoreEntriesThanAllowed_IsTooManyEntries()
    {
        var entries = new (string Name, string Body)[DocxContainerValidator.MaxEntries + 1];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ($"word/media/image{i}.png", "x");
        }

        using var stream = BuildZip(entries);

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.TooManyEntries);
    }

    /// <summary>
    /// The zip-bomb shape: a small archive whose entries declare an enormous expansion. It is
    /// rejected from the central directory alone -- nothing is decompressed, which is why a real
    /// bomb costs us nothing.
    /// </summary>
    [Fact]
    public void Inspect_DeclaredSizeBeyondCeiling_IsTooLarge()
    {
        // Highly compressible content: ~2 MB of zeroes per entry stores as almost nothing, so 60 of
        // them push the DECLARED total past the 100 MB ceiling while the archive stays tiny.
        var body = new string('\0', 2 * 1024 * 1024);
        var entries = new (string Name, string Body)[60];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ($"word/media/blob{i}.bin", body);
        }

        using var stream = BuildZip(entries);

        stream.Length.ShouldBeLessThan(1024 * 1024, "the archive itself must stay small -- that is the bomb shape");
        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.TooLarge);
    }

    [Fact]
    public void Inspect_RewindsTheStream_SoTheCallerCanStillStoreTheFile()
    {
        using var stream = BuildZip(
            ("[Content_Types].xml", "<Types/>"),
            ("word/document.xml", "<w:document/>"));

        DocxContainerValidator.Inspect(stream).ShouldBe(DocxContainerVerdict.Valid);

        stream.Position.ShouldBe(0);
    }

    private static MemoryStream BuildZip(params (string Name, string Body)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, body) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(body);
            }
        }
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}
