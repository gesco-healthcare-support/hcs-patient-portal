using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-23: the signed-in caller's email may be used only once confirmed. The appointment
/// access pathways, the change-request side that gets its consent granted automatically, and the
/// claim-examiner address a booking is sent to all match on an email, so an address nobody proved
/// must never count. Code reads it through <c>CurrentUser.GetVerifiedEmail()</c>, which answers null
/// for an unconfirmed address.
///
/// <para>This scans the source for any other read of <c>CurrentUser.Email</c>. A new one fails
/// here until it is switched to <c>GetVerifiedEmail()</c> or added below with the reason it is
/// safe.</para>
/// </summary>
public class RawCurrentUserEmailGuardTests
{
    /// <summary>Files allowed to read the raw address, each with the reason.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["HealthcareSupport.CaseEvaluation.Application/Appointments/CurrentUserVerifiedEmailExtensions.cs"] =
            "the helper itself: it returns the address only when the email_verified claim is true",
        ["HealthcareSupport.CaseEvaluation.Application/NotificationTemplates/NotificationTemplatesAppService.cs"] =
            "'Send test' mails a template to the staff caller's own inbox; no access or party decision",
        ["HealthcareSupport.CaseEvaluation.AuthServer/OpenIddict/HostIntakeImpersonationExtensionGrant.cs"] =
            "a host STAFF operator entering an office; the address only names their own internal shadow user "
            + "there and is never matched against an appointment's party emails",
    };

    private static readonly Regex RawRead =
        new(@"\b[Cc]urrentUser\s*\.\s*Email\b(?!Verified)", RegexOptions.Compiled);

    [Fact]
    public void The_callers_email_is_read_only_through_GetVerifiedEmail()
    {
        var offenders = RawReads()
            .Where(r => !Allowed.ContainsKey(r.File))
            .Select(r => $"{r.File}:{r.Line}")
            .ToList();

        offenders.ShouldBeEmpty(
            "These read CurrentUser.Email directly. An unconfirmed address must not decide access, a "
            + "change-request side or where a party's email goes: use CurrentUser.GetVerifiedEmail(), or "
            + "add the file to Allowed with the reason the raw address is safe there.");
    }

    [Fact]
    public void The_scan_sees_the_helper_it_allows()
    {
        // A known positive, so a scan that matched nothing could not pass the test above.
        RawReads().ShouldContain(r => r.File.EndsWith("CurrentUserVerifiedEmailExtensions.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_allowed_file_still_reads_the_raw_address()
    {
        // An entry whose file no longer reads it is a stale permission waiting to be reused.
        var files = RawReads().Select(r => r.File).ToHashSet(StringComparer.Ordinal);
        Allowed.Keys.Where(k => !files.Contains(k)).ShouldBeEmpty();
    }

    private static IEnumerable<(string File, int Line)> RawReads()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match m in RawRead.Matches(text))
            {
                if (!IsInComment(text, m.Index))
                {
                    yield return (relative, LineOf(text, m.Index));
                }
            }
        }
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
