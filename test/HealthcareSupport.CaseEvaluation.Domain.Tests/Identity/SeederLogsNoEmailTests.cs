using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// The identity seed contributors must not pass an email address (or a user name, which these seeders
/// set to the email) to a logger. The seeded host database holds real mailboxes, and
/// <c>ExternalUsersDataSeedContributor</c> is not demo-gated. Logs identify a user by the generated
/// <c>UserId</c> instead. This scans each <c>_logger.Log*(...)</c> statement as source text, because a
/// logger call's arguments cannot be observed from a unit test without running the seeders against a
/// real identity store.
/// </summary>
public class SeederLogsNoEmailTests
{
    private static readonly Regex LogCall =
        new(@"_logger\.Log\w+\((?<body>.*?)\);", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Identifier =
        new(@"\{Email\}|\{UserName\}|\bemail\b|\buserName\b|\.Email\b|\.UserName\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void No_identity_seeder_logger_call_names_an_email_or_user_name()
    {
        var dir = Path.Combine(RepoRoot(), "src", "HealthcareSupport.CaseEvaluation.Domain", "Identity");
        var files = Directory.EnumerateFiles(dir, "*SeedContributor.cs").ToList();
        files.ShouldNotBeEmpty();

        var offenders = new List<string>();
        var calls = 0;
        foreach (var file in files)
        {
            foreach (Match m in LogCall.Matches(File.ReadAllText(file)))
            {
                calls++;
                if (Identifier.IsMatch(m.Groups["body"].Value))
                {
                    offenders.Add(Path.GetFileName(file) + ": " + Regex.Replace(m.Value, @"\s+", " "));
                }
            }
        }

        calls.ShouldBeGreaterThan(10); // the scan must actually be seeing the log calls
        offenders.ShouldBeEmpty();
    }

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
