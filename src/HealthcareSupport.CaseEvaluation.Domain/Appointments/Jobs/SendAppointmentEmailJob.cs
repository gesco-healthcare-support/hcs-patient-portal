using System;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Appointments.Jobs;

/// <summary>
/// Hangfire-backed worker for appointment notification emails. Fired by
/// every email handler (Booking / StatusChange / Document / Packet etc.)
/// via <see cref="IBackgroundJobManager"/>.EnqueueAsync; runs out-of-band
/// so the originating HTTP request returns immediately regardless of SMTP
/// latency.
///
/// <para><b>One-shot at MVP:</b> SMTP exceptions are caught and logged as
/// warnings. Because no exception leaks, Hangfire records the job as
/// "Succeeded" and never retries. Intentional while ACS placeholder
/// credentials are in <c>appsettings.secrets.json</c>: the appointment
/// request still completes, with a log breadcrumb explaining why
/// delivery did not happen. When real ACS credentials land, remove the
/// try/catch so failures propagate and Hangfire's default retry policy
/// kicks in.</para>
///
/// <para>Phase 4 (Category 4, 2026-05-10) adds packet-attachment
/// support. When <see cref="SendAppointmentEmailArgs.PacketRef"/> is set,
/// the job fetches the rendered DOCX via
/// <see cref="IPacketAttachmentProvider"/>, builds a
/// <see cref="MailMessage"/> with the attachment, sends via the
/// <c>MailMessage</c> overload of <see cref="IEmailSender"/>, and then
/// calls <see cref="IPacketAttachmentProvider.NotifySendCompletedAsync"/>
/// so AttyCE-kind packets get pruned on success per Adrian's
/// AttyCE-on-failure-only retention rule.</para>
/// </summary>
public class SendAppointmentEmailJob :
    AsyncBackgroundJob<SendAppointmentEmailArgs>,
    ITransientDependency
{
    private readonly IEmailSender _emailSender;
    private readonly IPacketAttachmentProvider _packetAttachmentProvider;

    public SendAppointmentEmailJob(
        IEmailSender emailSender,
        IPacketAttachmentProvider packetAttachmentProvider)
    {
        _emailSender = emailSender;
        _packetAttachmentProvider = packetAttachmentProvider;
    }

    // [UnitOfWork] mirrors AppointmentDayReminderJob + GenerateAppointmentPacketJob.
    // Without it, IPacketAttachmentProvider.GetAttachmentAsync (which uses
    // IRepository under the hood) calls FirstOrDefault on a DbContext that
    // auto-disposes when the per-call UoW returns; the next access throws
    // ObjectDisposedException. The non-attachment SendPlainAsync path does
    // not hit a DbContext so it survived without -- but the safer pattern is
    // one UoW for the whole job.
    [UnitOfWork]
    public override async Task ExecuteAsync(SendAppointmentEmailArgs args)
    {
        if (args.PacketRef == null)
        {
            await SendPlainAsync(args);
            return;
        }
        await SendWithAttachmentAsync(args, args.PacketRef);
    }

    private async Task SendPlainAsync(SendAppointmentEmailArgs args)
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

    private async Task SendWithAttachmentAsync(SendAppointmentEmailArgs args, PacketAttachmentRef packetRef)
    {
        var attachment = await _packetAttachmentProvider.GetAttachmentAsync(
            packetRef.AppointmentId, packetRef.Kind);

        if (attachment == null)
        {
            // Per Adrian Decision (2026-05-10): if the packet is not yet
            // Generated (Failed or Generating), skip the email + log.
            // Office can Regenerate; on success, the email fires.
            Logger.LogWarning(
                "SendAppointmentEmailJob: packet {PacketId} (kind={Kind}) is not Generated; skipping packet email ({Context}) to {To}.",
                packetRef.PacketId, packetRef.Kind, args.Context, args.To);
            return;
        }

        var success = false;
        try
        {
            using var mail = new MailMessage
            {
                Subject = args.Subject,
                Body = args.Body,
                IsBodyHtml = args.IsBodyHtml,
            };
            mail.To.Add(args.To);

            using var ms = new System.IO.MemoryStream(attachment.Bytes);
            var mailAttachment = new Attachment(ms, attachment.FileName, attachment.ContentType);
            if (mailAttachment.ContentDisposition != null)
            {
                mailAttachment.ContentDisposition.DispositionType = DispositionTypeNames.Attachment;
                mailAttachment.ContentDisposition.FileName = attachment.FileName;
            }
            mail.Attachments.Add(mailAttachment);

            await _emailSender.SendAsync(mail);
            success = true;
            Logger.LogInformation(
                "SendAppointmentEmailJob: delivered ({Context}) to {To} with attachment {FileName} ({Bytes} bytes).",
                args.Context, args.To, attachment.FileName, attachment.Bytes.Length);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "SendAppointmentEmailJob: SMTP delivery failed ({Context}) to {To} with attachment. Configure ACS credentials to deliver.",
                args.Context, args.To);
        }
        finally
        {
            // Always callback so AttyCE retention rule sees both success
            // and failure paths -- the provider's NoOp on Patient/Doctor
            // makes the call safe regardless of kind.
            try
            {
                await _packetAttachmentProvider.NotifySendCompletedAsync(packetRef.PacketId, success);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    ex,
                    "SendAppointmentEmailJob: NotifySendCompletedAsync threw for packet {PacketId} (kind={Kind}); attachment lifecycle may need manual cleanup.",
                    packetRef.PacketId, packetRef.Kind);
            }
        }
    }
}
