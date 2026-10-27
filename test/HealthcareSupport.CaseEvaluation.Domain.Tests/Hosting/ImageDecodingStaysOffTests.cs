using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// 2026-10-27: Directory.Build.props suppresses the SixLabors.ImageSharp decoder advisories instead
/// of pinning the fixed 3.2.0, on two grounds this test keeps true.
///
/// <para>Unreachable: ImageSharp arrives only through ABP. Its one caller, ABP Account Pro's profile
/// picture upload, decodes the uploaded bytes (any format, TIFF included) only when
/// <c>AbpProfilePictureOptions.EnableImageCompression</c> is on, which it is not by default. So src/
/// must not switch it on, call an image API, or load ABP's imaging module.</para>
///
/// <para>Transitive: a direct ImageSharp PackageReference is a "Direct Package Dependency" under the
/// Six Labors Split License, which needs a paid licence for this company. Pinning it is a business
/// decision, so it fails here until someone makes that decision and removes this check.</para>
/// </summary>
public class ImageDecodingStaysOffTests
{
    private static readonly Regex ImageApi = new(
        @"\bAbpProfilePictureOptions\b|\bEnableImageCompression\b|\bIImage(Compressor|Resizer)\b"
        + @"|\bImage\s*\.\s*(Load|Identify|DetectFormat)|\bSixLabors\b|\bAbpImaging\w*Module\b"
        + @"|\[\s*(CompressImage|ResizeImage)\b",
        RegexOptions.Compiled);

    private static readonly Regex DirectImageSharpReference = new(
        @"<PackageReference\s+(Include|Update)\s*=\s*""SixLabors\.ImageSharp""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void No_source_file_turns_on_or_calls_image_decoding()
    {
        var offenders = SourceFiles()
            .SelectMany(f => Matches(f.Relative, File.ReadAllText(f.Path)))
            .ToList();

        offenders.ShouldBeEmpty(
            "These make ImageSharp decode images, so its decoder advisories become reachable and the "
            + "NuGetAuditSuppress entries in Directory.Build.props stop being true. Remove the call, or "
            + "move to a patched ImageSharp first (see the licence note there).");
    }

    [Fact]
    public void ImageSharp_is_never_referenced_directly()
    {
        var root = RepoRoot();
        var offenders = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "Directory.*.props", SearchOption.TopDirectoryOnly))
            .Where(p => !IsBuildOutput(p) && DirectImageSharpReference.IsMatch(File.ReadAllText(p)))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .ToList();

        offenders.ShouldBeEmpty(
            "A direct SixLabors.ImageSharp reference makes it a Direct Package Dependency under the "
            + "Six Labors Split License, which needs a paid commercial licence for this company.");
    }

    [Theory]
    [InlineData("Configure<AbpProfilePictureOptions>(o => o.EnableImageCompression = true);")]
    [InlineData("private readonly IImageCompressor _compressor;")]
    [InlineData("using var image = await Image.LoadAsync(stream);")]
    [InlineData("[DependsOn(typeof(AbpImagingImageSharpModule))]")]
    [InlineData("[CompressImage]")]
    public void The_scan_recognises_each_way_in(string line)
    {
        // Known positives, so a pattern that matched nothing could not pass the scan above.
        Matches("Sample.cs", line).ShouldNotBeEmpty();
    }

    [Fact]
    public void The_scan_reads_the_real_source_tree()
    {
        // An empty or wrong directory would pass the scan vacuously.
        SourceFiles().Count().ShouldBeGreaterThan(500);
    }

    private static IEnumerable<string> Matches(string relative, string text)
    {
        foreach (Match m in ImageApi.Matches(text))
        {
            if (!IsInComment(text, m.Index))
            {
                yield return $"{relative}:{LineOf(text, m.Index)} {m.Value}";
            }
        }
    }

    private static IEnumerable<(string Path, string Relative)> SourceFiles()
    {
        var src = Path.Combine(RepoRoot(), "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .Select(p => (p, Path.GetRelativePath(src, p).Replace('\\', '/')));
    }

    private static bool IsBuildOutput(string path)
    {
        var p = path.Replace('\\', '/');
        return p.Contains("/obj/", StringComparison.Ordinal) || p.Contains("/bin/", StringComparison.Ordinal)
            || p.Contains("/node_modules/", StringComparison.Ordinal);
    }

    private static bool IsInComment(string text, int index)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var head = text.AsSpan(lineStart, index - lineStart).TrimStart();
        return head.StartsWith("//", StringComparison.Ordinal) || head.StartsWith("*", StringComparison.Ordinal);
    }

    private static int LineOf(string text, int index) =>
        text.AsSpan(0, index).Count('\n') + 1;

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !d.GetFiles("*.slnx").Any())
        {
            d = d.Parent;
        }
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
