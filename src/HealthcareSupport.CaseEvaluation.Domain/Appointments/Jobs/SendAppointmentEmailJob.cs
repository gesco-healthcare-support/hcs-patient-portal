using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace HealthcareSupport.CaseEvaluation.Appointments.Jobs;

/// <summary>
/// Hangfire-backed worker for appointment notification emails. Fired by the
/// <c>SubmissionEmailHandler</c> and <c>StatusChangeEmailHandler</c> via
/// <see cref="IBackgroundJobManager"/>.EnqueueAsync; runs out-of-band so the
/// originating HTTP request returns immediately regardless of SMTP latency.
///
/// One-shot execution at MVP: SMTP exceptions are caught and logged as
/// warnings inside <see cref="ExecuteAsync"/>. Because no exception leaks
/// out, Hangfire records the job as "Succeeded" and never retries. This is
/// intentional while ACS placeholder credentials are in <c>appsettings.secrets.json</c>:
/// the appointment request still completes, with a log breadcrumb explaining why
/// delivery did not happen.
///
/// When real ACS credentials land and email completion should gate behavior
/// (per Adrian 2026-04-28), remove the try/catch so failures propagate up
/// and let Hangfire's default retry policy (10 attempts with exponential
/// backoff) handle transient SMTP failures. The handlers can also switch
/// from <c>EnqueueAsync</c> to a synchronous <c>SendAsync</c> if you want
/// the appointment request itself to fail when delivery fails.
/// Logged in <c>docs/plans/deferred-from-mvp.md</c>.
/// </summary>
public class SendAppointmentEmailJob :
    AsyncBackgroundJob<SendAppointmentEmailArgs>,
    ITransientDependency
{
    private readonly IEmailSender _emailSender;

    public SendAppointmentEmailJob(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public override async Task ExecuteAsync(SendAppointmentEmailArgs args)
    {
        try
        {
            await _emailSender.SendAsync(args.To, args.Subject, args.Body, isBodyHtml: args.IsBodyHtml);
            Logger.LogInformation(
                "SendAppointmentEmailJob: delivered ({Context}) to {To}.",
                args.Context,
                args.To);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "SendAppointmentEmailJob: SMTP delivery failed ({Context}) to {To}. Configure ACS credentials to deliver. Job will not retry until Attempts policy is raised.",
                args.Context,
                args.To);
        }
    }
}
