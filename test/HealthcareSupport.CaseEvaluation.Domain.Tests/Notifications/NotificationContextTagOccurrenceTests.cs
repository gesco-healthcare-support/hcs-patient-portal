using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Every email context tag either carries an occurrence part or belongs to an email that happens once
/// per subject.
///
/// <para><b>Why.</b> The notification outbox collapses a send onto any existing row with the same
/// office, recipient and context tag, whatever that row's state, so a replayed unit of work cannot
/// email the same thing twice. A tag that names a KIND of email -- "password reset for user X", "the
/// daily digest for office Y" -- therefore sends the first time and then never again, silently: the
/// screen says the email was sent. Measured on the test server on 2026-10-23: the daily pending digest
/// had gone out once, on its first day, and every person's second password reset had sent nothing.
/// The fix gives each such tag an occurrence part through <c>NotificationOccurrence</c>.</para>
///
/// <para><b>How.</b> This reads every <c>contextTag:</c> argument and every activation send in the
/// source, because which emails can repeat is a property of the code that calls the dispatcher and is
/// invisible from a unit test of the dispatcher itself. A tag passes when it is built by
/// <c>NotificationOccurrence</c>, when its literal kind is on <see cref="OncePerSubject"/> (each entry
/// says why it can only happen once), or when it is one of the reviewed forwarders and builders in
/// <see cref="ReviewedNonLiteral"/>. Anything else -- in particular a NEW sender -- fails here, and the
/// fix is to wrap its tag, or to add it to the list with the reason it cannot repeat.</para>
/// </summary>
public class NotificationContextTagOccurrenceTests
{
    /// <summary>
    /// Literal tag kinds (the text before the first interpolation hole) for emails that happen at most
    /// once for their subject, so the outbox collapse is exactly what is wanted.
    /// </summary>
    private static readonly Dictionary<string, string> OncePerSubject = new(StringComparer.Ordinal)
    {
        ["StatusChange/Approved/Stakeholders/"] = "an appointment is approved once",
        ["StatusChange/Approved/Responsible/"] = "an appointment is approved once",
        ["StatusChange/Rejected/Stakeholders/"] = "rejection is terminal",
        ["StatusChange/NoShow/InternalStaff/"] = "no-show is terminal",
        ["StatusChange/CancelledNoBill/Stakeholders/"] = "cancellation is terminal",
        ["PatientPacket/"] = "one packet email per appointment and recipient; a re-render mails only new addresses",
        ["AttyCEPacket/"] = "one packet email per appointment and recipient; a re-render mails only new addresses",
        ["JdfOverdue/"] = "the daily job re-raises an overdue appointment; the collapse keeps it to one email",
        ["AppointmentReminder/T-"] = "one reminder per offset per appointment",
        ["AppointmentRequested/"] = "a booking is submitted once (resubmitting after Send Back raises no new booking event)",
        ["AppointmentRequested/Office/"] = "a booking is submitted once",
        ["BookingSubmitted/ApproveReject/"] = "a booking is submitted once",
        ["ChangeRequestSubmitted/"] = "per change request",
        ["ChangeRequestRejected/"] = "per change request",
        ["ChangeRequestApproved/"] = "per change request",
        ["ClinicalStaffCancellation/"] = "per change request",
        ["UserQuery/Submitted/"] = "per query",
        ["Invite/"] = "per invitation; resending is a separate, per-request tag",
        ["AccessorInvited/"] = "per new account, created once",
        ["InternalUserCreated/"] = "per new account, created once",
    };

    /// <summary>Tags passed as something other than a literal, each reviewed once.</summary>
    private static readonly Dictionary<string, string> ReviewedNonLiteral = new(StringComparer.Ordinal)
    {
        ["contextTag"] = "a forwarder: its callers' tags are checked where they are written",
        ["ContextTagFor(eventData)"] = "missing-intakes digest, keyed by its run date",
        ["BuildContextTag(eventData)"] = "consent request, keyed by round and send attempt since phase 4c",
    };

    private static readonly Regex NamedArgument =
        new(@"\bcontextTag:\s*(?<expr>.+?)(?=\)\s*;|,\s*\r?\n|\r?\n\s*\))", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ActivationSend =
        new(@"(?:_activationSender\.|await\s+)SendAsync\(\s*\w+\s*,\s*(?<expr>[^\r\n]+)", RegexOptions.Compiled);

    // Greedy, so a quoted string inside an interpolation hole stays part of the text.
    private static readonly Regex Literal = new("^\\$?\"(?<text>.*)\"$", RegexOptions.Singleline | RegexOptions.Compiled);

    [Fact]
    public void Every_context_tag_has_an_occurrence_or_happens_once_per_subject()
    {
        var sites = TagSites().ToList();
        sites.Count.ShouldBeGreaterThan(40, "the scan must actually be finding the dispatch calls");

        var offenders = sites
            .Where(s => !IsAccepted(s.Expression))
            .Select(s => $"{s.File}:{s.Line}: {s.Expression}")
            .ToList();

        offenders.ShouldBeEmpty(
            "These emails can be sent more than once for the same person or office, but their context tag "
            + "has no occurrence part, so every repeat collapses onto the first outbox row and is never sent. "
            + "Build the tag with NotificationOccurrence.PerRequest / At / PerPacificDay, or, if the email "
            + "really happens once per subject, add its kind to OncePerSubject with the reason.");
    }

    [Theory]
    [InlineData("NotificationOccurrence.PerRequest($\"PasswordReset/RequestLink/{user.Id}\")", true)]
    [InlineData("$\"StatusChange/Approved/Stakeholders/{eventData.AppointmentId}\"", true)]
    [InlineData("contextTag", true)]
    [InlineData("$\"PasswordReset/RequestLink/{user.Id}\"", false)]
    [InlineData("$\"PendingDailyDigest/{eventData.TenantId}\"", false)]
    [InlineData("$\"{templateCode}/{eventData.TenantId}\"", false)]
    [InlineData("SomeNewHelper(eventData)", false)]
    public void The_classifier_accepts_and_refuses_what_it_should(string expression, bool accepted)
    {
        IsAccepted(expression).ShouldBe(accepted);
    }

    [Fact]
    public void The_scan_sees_the_tags_that_used_to_collapse()
    {
        // Known positives, so a scan that silently matched nothing could not pass the main fact.
        var expressions = TagSites().Select(s => s.Expression).ToList();
        expressions.ShouldContain(e => e.Contains("PasswordReset/RequestLink/", StringComparison.Ordinal));
        expressions.ShouldContain(e => e.Contains("PendingDailyDigest/", StringComparison.Ordinal));
        expressions.ShouldContain(e => e.Contains("AccountActivation/", StringComparison.Ordinal));
        expressions.ShouldContain(e => e.Contains("CaseTrackerFeedAlert", StringComparison.Ordinal));
    }

    private static bool IsAccepted(string expression)
    {
        if (expression.StartsWith("NotificationOccurrence.", StringComparison.Ordinal))
        {
            return true;
        }

        if (ReviewedNonLiteral.ContainsKey(expression))
        {
            return true;
        }

        var literal = Literal.Match(expression);
        if (!literal.Success)
        {
            return false;
        }

        var text = literal.Groups["text"].Value;
        var hole = text.IndexOf('{');
        var kind = hole < 0 ? text : text[..hole];
        return kind.Length > 0 && OncePerSubject.Keys.Any(k => string.Equals(kind, k, StringComparison.Ordinal));
    }

    private static IEnumerable<(string File, int Line, string Expression)> TagSites()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (relative.Contains("/Migrations/", StringComparison.Ordinal)
                || relative.Contains("/TenantMigrations/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (!text.Contains("contextTag:", StringComparison.Ordinal)
                && !text.Contains("SendAsync(", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match m in NamedArgument.Matches(text))
            {
                if (IsInComment(text, m.Index))
                {
                    continue;
                }
                yield return (relative, LineOf(text, m.Index), Normalize(m.Groups["expr"].Value));
            }

            if (relative.EndsWith("AccountActivationSender.cs", StringComparison.Ordinal)
                || text.Contains("_activationSender.SendAsync(", StringComparison.Ordinal))
            {
                foreach (Match m in ActivationSend.Matches(text))
                {
                    if (IsInComment(text, m.Index))
                    {
                        continue;
                    }
                    yield return (relative, LineOf(text, m.Index), ActivationTagOf(m.Groups["expr"].Value));
                }
            }
        }
    }

    /// <summary>The tag argument of an activation send: everything up to the argument list's end.</summary>
    private static string ActivationTagOf(string rest)
    {
        var depth = 0;
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case '(':
                    depth++;
                    break;
                case ')' when depth == 0:
                case ',' when depth == 0:
                    return Normalize(rest[..i]);
                case ')':
                    depth--;
                    break;
            }
        }
        return Normalize(rest);
    }

    private static string Normalize(string expression) =>
        Regex.Replace(expression, @"\s+", " ").Trim();

    private static bool IsInComment(string text, int index)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var head = text.AsSpan(lineStart, index - lineStart).TrimStart();
        return head.StartsWith("//", StringComparison.Ordinal);
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
