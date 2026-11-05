using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- the staff email stops are enforced in ONE place (<see cref="EmailStopPolicy"/>), and these
/// guards keep every email path going through it.
///
/// <list type="bullet">
///   <item>Every dispatch call names its case: <c>NotificationCase.Of(...)</c>, or the reviewed pass-through
///     in the booker CC helper. A call that passes <c>NotificationCase.None</c> must come from a file on the
///     list below, with the reason it is not about a single case -- so a NEW case email cannot slip past a
///     "this case only" stop. (An all-cases stop still applies to every <c>None</c> email.)</item>
///   <item>The self-service account templates that are never stopped are exactly the reviewed eight.</item>
///   <item>Nothing else enqueues the send job directly: the only files that do are the account emailer
///     (account mail is never stopped) and the two reminder jobs, which apply the stops themselves.</item>
/// </list>
/// </summary>
public class EmailStopGuardTests
{
    /// <summary>Files allowed to dispatch with <c>NotificationCase.None</c>, each with the reason.</summary>
    private static readonly Dictionary<string, string> NoneAllowed = new(StringComparer.Ordinal)
    {
        ["HealthcareSupport.CaseEvaluation.Application/ExternalAccount/AccountActivationSender.cs"] = "account activation (never stopped)",
        ["HealthcareSupport.CaseEvaluation.Application/ExternalAccount/CaseEvaluationProfileAppService.cs"] = "password changed (never stopped)",
        ["HealthcareSupport.CaseEvaluation.Application/ExternalAccount/ExternalAccountAppService.cs"] = "password reset / changed (never stopped)",
        ["HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs"] = "staff invitation + portal link (never stopped)",
        ["HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs"] = "staff account mail",
        ["HealthcareSupport.CaseEvaluation.Application/Users/SignInEmailChangeNotifier.cs"] = "sign-in email changed (never stopped)",
        ["HealthcareSupport.CaseEvaluation.Application/Integration/CaseTracker/Handlers/CaseTrackerFeedAlertEmailHandler.cs"] = "technical alert list, not a case",
        ["HealthcareSupport.CaseEvaluation.Application/Integration/CaseTracker/Handlers/CaseTrackerMissingIntakesEmailHandler.cs"] = "technical alert list, not a case",
        ["HealthcareSupport.CaseEvaluation.Application/Integration/CaseTracker/Handlers/CaseTrackerPushFailedEmailHandler.cs"] = "technical alert list",
        ["HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/OfficeSummaryEmailHandler.cs"] = "daily staff summary over many cases",
        ["HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/SupervisorSummaryEmailHandler.cs"] = "daily supervisor summary over many offices",
        ["HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/UserQuerySubmittedEmailHandler.cs"] = "a question to staff (office inbox, Intake, IT Admins)",
        ["HealthcareSupport.CaseEvaluation.Application/NotificationTemplates/NotificationTemplatesAppService.cs"] = "'Send test' to the staff caller's own inbox",
    };

    /// <summary>Files that enqueue the send job directly, each with why the stops still hold.</summary>
    private static readonly Dictionary<string, string> DirectSendAllowed = new(StringComparer.Ordinal)
    {
        ["HealthcareSupport.CaseEvaluation.Application/Emailing/CaseEvaluationAccountEmailer.cs"] = "account mail only (never stopped)",
        ["HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs"] = "enqueues the outbox drain, which re-checks the stops",
        ["HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs"] = "the send job itself",
        ["HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/Jobs/CancellationRescheduleReminderJob.cs"] = "applies IEmailStopPolicy itself",
        ["HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/Jobs/RequestSchedulingReminderJob.cs"] = "applies IEmailStopPolicy itself",
    };

    private static readonly Regex DispatchCall = new(
        @"\.(DispatchAsync|DispatchToWithCcAsync|DispatchWithPacketsAsync|DispatchToBookerWithCcAsync)\(\s*(?:notificationCase:\s*)?(?<first>[^,]+),",
        RegexOptions.Compiled);

    [Fact]
    public void Every_dispatch_names_its_case_or_is_a_reviewed_non_case_email()
    {
        var offenders = DispatchCalls()
            .Where(c => !IsCaseArgument(c.First) && !(IsNone(c.First) && NoneAllowed.ContainsKey(c.File)))
            .Select(c => $"{c.File}:{c.Line} -> {c.First}")
            .ToList();

        offenders.ShouldBeEmpty(
            "A dispatch must pass NotificationCase.Of(appointmentId) when the email is about a case, so a staff "
            + "\"stop emails for this case\" holds. NotificationCase.None is allowed only from the files listed in "
            + "NoneAllowed, each with the reason the email is not about a single case.");
    }

    [Fact]
    public void The_scan_sees_the_dispatch_calls()
    {
        // A known positive, so a scan that matched nothing could not pass the test above.
        var calls = DispatchCalls().ToList();
        calls.Count.ShouldBeGreaterThan(30);
        calls.ShouldContain(c => c.File.EndsWith("AttyCEPacketEmailHandler.cs", StringComparison.Ordinal) && IsCaseArgument(c.First));
        calls.ShouldContain(c => c.File.EndsWith("AccountActivationSender.cs", StringComparison.Ordinal) && IsNone(c.First));
    }

    [Fact]
    public void Every_none_allowed_file_still_sends_a_non_case_email()
    {
        // An entry whose file no longer passes None is a stale permission waiting to be reused.
        var files = DispatchCalls().Where(c => IsNone(c.First)).Select(c => c.File).ToHashSet(StringComparer.Ordinal);
        NoneAllowed.Keys.Where(k => !files.Contains(k)).ShouldBeEmpty();
    }

    [Fact]
    public void The_never_stopped_templates_are_exactly_the_self_service_account_mail()
    {
        EmailStopPolicy.NeverStoppedTemplateCodes.OrderBy(c => c, StringComparer.Ordinal).ShouldBe(new[]
        {
            NotificationTemplateConsts.Codes.AccountActivation,
            NotificationTemplateConsts.Codes.AccountEmailChanged,
            NotificationTemplateConsts.Codes.ExternalUserPortalLink,
            NotificationTemplateConsts.Codes.InternalUserCreated,
            NotificationTemplateConsts.Codes.InviteExternalUser,
            NotificationTemplateConsts.Codes.PasswordChange,
            NotificationTemplateConsts.Codes.ResetPassword,
            NotificationTemplateConsts.Codes.UserRegistered,
        }.OrderBy(c => c, StringComparer.Ordinal));

        // The case templates an Authorized User receives ARE stopped.
        EmailStopPolicy.IsNeverStopped(NotificationTemplateConsts.Codes.AccessorAppointmentBooked).ShouldBeFalse();
        EmailStopPolicy.IsNeverStopped(NotificationTemplateConsts.Codes.AccessorAppointmentAdded).ShouldBeFalse();
        EmailStopPolicy.IsNeverStopped(NotificationTemplateConsts.Codes.ChangeRequestConsentRequest).ShouldBeFalse();
    }

    [Fact]
    public void Only_reviewed_files_enqueue_email_sends_directly()
    {
        var offenders = SourceFiles()
            .Where(f => f.Text.Contains("IBackgroundJobManager", StringComparison.Ordinal)
                && (f.Text.Contains("SendAppointmentEmailArgs", StringComparison.Ordinal)
                    || f.Text.Contains("IAppointmentRecipientResolver", StringComparison.Ordinal)))
            .Select(f => f.File)
            .Where(f => !DirectSendAllowed.ContainsKey(f))
            .ToList();

        offenders.ShouldBeEmpty(
            "These enqueue an email outside the notification dispatcher, so the staff email stops would not apply. "
            + "Send through INotificationDispatcher, or apply IEmailStopPolicy and add the file to DirectSendAllowed.");
    }

    [Fact]
    public void The_reminder_jobs_that_send_directly_apply_the_stops()
    {
        foreach (var file in DirectSendAllowed.Keys.Where(k => k.Contains("/Jobs/", StringComparison.Ordinal)
            && k.EndsWith("ReminderJob.cs", StringComparison.Ordinal)))
        {
            var text = SourceFiles().Single(f => f.File == file).Text;
            text.ShouldContain("_emailStopPolicy.LookupAsync", customMessage: file);
            text.ShouldContain("stops.IsStopped(", customMessage: file);
        }
    }

    private static bool IsCaseArgument(string first)
    {
        var arg = first.Trim();
        return arg.StartsWith("NotificationCase.Of(", StringComparison.Ordinal) || arg == "notificationCase";
    }

    private static bool IsNone(string first) => first.Trim() == "NotificationCase.None";

    private static IEnumerable<(string File, int Line, string First)> DispatchCalls()
    {
        foreach (var (file, text) in SourceFiles())
        {
            if (file.EndsWith("/NotificationDispatcher.cs", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (Match m in DispatchCall.Matches(text))
            {
                var lineStart = text.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1;
                var head = text.Substring(lineStart, m.Index - lineStart);
                if (head.Contains("Task ", StringComparison.Ordinal) || head.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                yield return (file, text.AsSpan(0, m.Index).Count('\n') + 1, m.Groups["first"].Value);
            }
        }
    }

    private static IEnumerable<(string File, string Text)> SourceFiles()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }
            yield return (relative, File.ReadAllText(file));
        }
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
