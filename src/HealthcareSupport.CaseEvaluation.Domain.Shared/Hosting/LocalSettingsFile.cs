using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// #595: loads the gitignored <c>appsettings.Local.json</c> override, skipping an EMPTY or whitespace-only
/// file so it cannot stop the host starting.
///
/// <para>NOT quite "the same as absent": a skipped file is never registered, so <c>reloadOnChange</c> does not
/// watch it, and filling one in later needs a restart. An absent file IS watched and picks up content when it
/// appears. The difference only matters to a developer who creates the file blank and then edits it.</para>
///
/// <para><c>AddJsonFile(..., optional: true)</c> covers a file that is absent, not one that is present and empty.
/// A zero-byte file -- what <c>touch</c> produces -- is still parsed, empty is not JSON, and the host stopped at
/// configuration loading with <c>InvalidDataException</c>. An empty file says nothing, so it is skipped. A file
/// with content is parsed as before, so malformed JSON still fails loudly.</para>
///
/// <para>Shared by every process that reads the file (both web hosts through <c>CaseEvaluationHost</c>, and the
/// DbMigrator), which is why it lives here: this is the one project all of them reference.</para>
/// </summary>
public static class LocalSettingsFile
{
    public const string FileName = "appsettings.Local.json";

    public static IConfigurationBuilder AddLocalSettingsJson(this IConfigurationBuilder builder)
    {
        // Resolved through the builder's own file provider, as AddJsonFile resolves it, so the two agree on which
        // file is meant whatever the content root is.
        var file = builder.GetFileProvider().GetFileInfo(FileName);
        if (file.Exists && IsBlank(file))
        {
            return builder;
        }

        return builder.AddJsonFile(FileName, optional: true, reloadOnChange: true);
    }

    private static bool IsBlank(IFileInfo file)
    {
        // StreamReader strips a byte-order mark, so a file holding only a BOM counts as blank too.
        using var reader = new StreamReader(file.CreateReadStream());
        return string.IsNullOrWhiteSpace(reader.ReadToEnd());
    }
}
