using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// Synthetic uploaded files for the controllers' multipart actions. The content is a few fixed
/// bytes; no real document is involved.
/// </summary>
internal static class FormFiles
{
    public const string FileName = "synthetic-upload.pdf";
    public const string ContentType = "application/pdf";

    /// <summary>A file of the given length whose stream is <paramref name="stream"/>.</summary>
    public static IFormFile WithContent(out Stream stream, long? length = null)
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-synthetic");
        stream = new MemoryStream(bytes);
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(FileName);
        file.ContentType.Returns(ContentType);
        file.Length.Returns(length ?? bytes.Length);
        file.OpenReadStream().Returns(stream);
        return file;
    }

    /// <summary>A file that arrived with no bytes.</summary>
    public static IFormFile Empty() => WithContent(out _, length: 0);
}
