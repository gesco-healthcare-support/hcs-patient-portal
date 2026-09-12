using System;
using System.IO;
using System.IO.Compression;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Item G (2026-08-22) -- what a <c>.docx</c> upload turned out to be.
/// </summary>
public enum DocxContainerVerdict
{
    /// <summary>A structurally valid WordprocessingML package with no VBA project.</summary>
    Valid = 0,

    /// <summary>Not a readable zip at all, whatever the extension or the PK header claimed.</summary>
    NotAZip = 1,

    /// <summary>Carries a <c>vbaProject.bin</c>. A macro-enabled file renamed to .docx lands here.</summary>
    MacroEnabled = 2,

    /// <summary>More entries than any real document has.</summary>
    TooManyEntries = 3,

    /// <summary>Declared uncompressed size beyond the ceiling.</summary>
    TooLarge = 4,

    /// <summary>A readable zip that is missing the parts every Word document must have.</summary>
    NotWordDocument = 5,
}

/// <summary>
/// Item G (2026-08-22) -- decides whether an uploaded <c>.docx</c> really is a Word document that
/// carries no macro.
///
/// <para>Extracted from <c>AppointmentDocumentsAppService</c> so the RULE is testable without
/// standing up ABP DI, which is the same shape items C and D settled on. The AppService maps the
/// verdict to a user-facing message.</para>
///
/// <para><b>Why a magic-byte check is not enough.</b> OOXML is a ZIP container, so its first four
/// bytes are <c>PK\x03\x04</c> -- identical to every other archive. Establishing "this is really a
/// Word document" means looking at the parts inside, not sniffing a header.</para>
///
/// <para><b>Why this cannot be a zip bomb.</b> Only the central directory is read: entry names and
/// their DECLARED uncompressed sizes. Nothing is ever decompressed, so a hostile archive costs a
/// directory read and nothing more, and a lying size header costs nothing either. The size and count
/// ceilings exist to reject absurd archives cheaply, not to defend a decompression we never do.</para>
///
/// <para><b>What this does NOT establish.</b> That the file is safe. There is no antivirus anywhere
/// in this stack. What makes format restriction the effective control here is that we never open or
/// render these files -- we store them and forward them -- so the risk is acting as a delivery
/// channel, and a .docx that cannot carry a VBA project is a poor delivery channel for a macro.</para>
/// </summary>
public static class DocxContainerValidator
{
    /// <summary>Beyond any real document; a Word file is typically well under 100 entries.</summary>
    public const int MaxEntries = 512;

    /// <summary>Ten times the 10 MB upload cap, so legitimate compressible documents pass.</summary>
    public const long MaxDeclaredBytes = 100L * 1024 * 1024;

    private const string VbaProjectEntryName = "vbaProject.bin";
    private const string ContentTypesEntryName = "[Content_Types].xml";
    private const string MainDocumentEntryName = "word/document.xml";

    /// <summary>
    /// Inspects the container and returns a verdict. Leaves <paramref name="stream"/> rewound to the
    /// start so the caller can go on to store the file.
    /// </summary>
    public static DocxContainerVerdict Inspect(Stream stream)
    {
        if (stream == null)
        {
            return DocxContainerVerdict.NotAZip;
        }

        try
        {
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            if (archive.Entries.Count > MaxEntries)
            {
                return DocxContainerVerdict.TooManyEntries;
            }

            return InspectEntries(archive);
        }
        catch (InvalidDataException)
        {
            return DocxContainerVerdict.NotAZip;
        }
        finally
        {
            // No null check: a null stream returned above, before the try.
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
        }
    }

    /// <summary>
    /// The per-entry pass, split out of <see cref="Inspect"/> so neither method exceeds the
    /// project's cognitive-complexity ceiling. Returns on the first disqualifying entry.
    /// </summary>
    private static DocxContainerVerdict InspectEntries(ZipArchive archive)
    {
        long declaredTotal = 0;
        var hasContentTypes = false;
        var hasMainDocumentPart = false;

        foreach (var entry in archive.Entries)
        {
            // Zip paths are '/'-separated by spec, but hostile archives do as they please.
            var name = entry.FullName.Replace('\\', '/');

            // Checked on the ENTRY NAME, wherever it sits in the package. A macro-enabled file
            // renamed to .docx is caught here, which is the whole reason for opening it.
            if (name.EndsWith(VbaProjectEntryName, StringComparison.OrdinalIgnoreCase))
            {
                return DocxContainerVerdict.MacroEnabled;
            }

            // entry.Length is the DECLARED uncompressed size from the central directory. Reading it
            // decompresses nothing.
            declaredTotal += entry.Length;
            if (declaredTotal > MaxDeclaredBytes)
            {
                return DocxContainerVerdict.TooLarge;
            }

            if (string.Equals(name, ContentTypesEntryName, StringComparison.OrdinalIgnoreCase))
            {
                hasContentTypes = true;
            }
            else if (string.Equals(name, MainDocumentEntryName, StringComparison.OrdinalIgnoreCase))
            {
                hasMainDocumentPart = true;
            }
        }

        // Both are mandatory in a WordprocessingML package. Missing either means this is some other
        // archive wearing a .docx name -- a renamed .xlsx, or a plain zip of anything.
        return hasContentTypes && hasMainDocumentPart
            ? DocxContainerVerdict.Valid
            : DocxContainerVerdict.NotWordDocument;
    }
}
