using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments.Jobs;

/// <summary>
/// The <see cref="SendAppointmentEmailJob"/> paths that <c>SendAppointmentEmailJobTests</c> does not
/// reach: the CC'd plain send, link logging, and the packet-attachment send with its skip, failure
/// and callback outcomes.
/// </summary>
/// <remarks>
/// The sender is a hand-written <see cref="RecordingEmailSender"/>, never one resolved from DI, so
/// no test can reach a real mail relay. It snapshots each <see cref="MailMessage"/> inside
/// <c>SendAsync</c> because the job disposes the message (and the attachment stream) on return.
/// Every "not sent" fact seeds a message the job would otherwise have sent. Values are synthetic.
/// </remarks>
public class SendAppointmentEmailJobPathTests
{
    private static readonly Guid TenantId = new("7e57c000-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentId = new("7e57c000-0000-4000-9000-000000000002");
    private static readonly Guid PacketId = new("7e57c000-0000-4000-9000-000000000003");

    /// <summary>What the job handed to the sender, captured before the job disposed it.</summary>
    private sealed record SentMail(
        string To,
        IReadOnlyList<string> Cc,
        string? Subject,
        string? Body,
        bool IsBodyHtml,
        IReadOnlyList<SentAttachment> Attachments);

    private sealed record SentAttachment(string? Name, string MediaType, string? Disposition, string? DispositionFileName, byte[] Bytes);

    /// <summary>Hand-written <see cref="IEmailSender"/> that records sends and can be told to fail.</summary>
    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<SentMail> Sent { get; } = new();

        public Exception? FailWith { get; set; }

        public Task SendAsync(string to, string? subject, string? body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        {
            ThrowIfFailing();
            Sent.Add(new SentMail(to, Array.Empty<string>(), subject, body, isBodyHtml, Array.Empty<SentAttachment>()));
            return Task.CompletedTask;
        }

        public Task SendAsync(string from, string to, string? subject, string? body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null) =>
            throw new NotSupportedException("The job never sets a from address.");

        public Task SendAsync(MailMessage mail, bool normalize = true)
        {
            ThrowIfFailing();
            var attachments = mail.Attachments.Select(a =>
            {
                using var copy = new MemoryStream();
                a.ContentStream.CopyTo(copy);
                return new SentAttachment(a.Name, a.ContentType.MediaType, a.ContentDisposition?.DispositionType, a.ContentDisposition?.FileName, copy.ToArray());
            }).ToList();
            Sent.Add(new SentMail(
                mail.To.Single().Address,
                mail.CC.Select(c => c.Address).ToList(),
                mail.Subject,
                mail.Body,
                mail.IsBodyHtml,
                attachments));
            return Task.CompletedTask;
        }

        public Task QueueAsync(string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null) =>
            throw new NotSupportedException("The job sends directly; it never queues.");

        public Task QueueAsync(string from, string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null) =>
            throw new NotSupportedException("The job sends directly; it never queues.");

        private void ThrowIfFailing()
        {
            if (FailWith != null)
            {
                throw FailWith;
            }
        }
    }

    /// <summary>Hand-written logger that keeps each rendered message with its level.</summary>
    private sealed class RecordingLogger : ILogger<AsyncBackgroundJob<SendAppointmentEmailArgs>>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class World
    {
        public RecordingEmailSender Sender { get; } = new();
        public IPacketAttachmentProvider Packets { get; } = Substitute.For<IPacketAttachmentProvider>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();
        public IConfiguration Configuration { get; } = Substitute.For<IConfiguration>();
        public RecordingLogger Log { get; } = new();
        public SendAppointmentEmailJob Job { get; }

        public World()
        {
            CurrentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());
            Job = new SendAppointmentEmailJob(Sender, Packets, CurrentTenant, Configuration) { Logger = Log };
        }

        public IEnumerable<string> Messages(LogLevel level) => Log.Entries.Where(e => e.Level == level).Select(e => e.Message);
    }

    private static SendAppointmentEmailArgs Args(PacketAttachmentRef? packet = null, params string[] cc) => new()
    {
        To = "TEST-patient@test.local",
        Cc = cc.ToList(),
        Subject = "TEST Appointment approved",
        Body = "<p>TEST body</p>",
        IsBodyHtml = false,
        Context = "TEST/Approved",
        TenantId = TenantId,
        PacketRef = packet,
    };

    private static PacketAttachmentRef PatientPacket() => new()
    {
        AppointmentId = AppointmentId,
        PacketId = PacketId,
        Kind = PacketKind.Patient,
    };

    // ------------------------------------------------------------------ plain sends

    [Fact]
    public async Task A_plain_send_with_cc_goes_as_one_message_carrying_every_non_blank_cc()
    {
        var world = new World();

        await world.Job.ExecuteAsync(Args(null, "TEST-cc1@test.local", "  ", "TEST-cc2@test.local"));

        var sent = world.Sender.Sent.ShouldHaveSingleItem();
        sent.To.ShouldBe("TEST-patient@test.local");
        sent.Cc.ShouldBe(new[] { "TEST-cc1@test.local", "TEST-cc2@test.local" });
        sent.Subject.ShouldBe("TEST Appointment approved");
        sent.Body.ShouldBe("<p>TEST body</p>");
        sent.IsBodyHtml.ShouldBeFalse();
        world.CurrentTenant.Received(1).Change(TenantId, Arg.Any<string?>());
        world.Messages(LogLevel.Information).ShouldContain(m => m.Contains("delivered") && m.Contains("cc=3"));
    }

    [Fact]
    public async Task Link_logging_when_switched_on_lists_each_distinct_link_once_and_is_silent_when_off()
    {
        var on = new World();
        on.Configuration["Notifications:LogLinks"].Returns("TRUE");
        var withLinks = Args(null, "TEST-cc@test.local");
        withLinks.Body = "Open https://portal.test.local/a then HTTPS://PORTAL.test.local/A and 'http://portal.test.local/b?x=1'.";

        var noLinks = new World();
        noLinks.Configuration["Notifications:LogLinks"].Returns("true");

        var off = new World();
        off.Configuration["Notifications:LogLinks"].Returns((string?)null);

        await on.Job.ExecuteAsync(withLinks);
        await noLinks.Job.ExecuteAsync(Args());
        await off.Job.ExecuteAsync(withLinks);

        var line = on.Messages(LogLevel.Information).Single(m => m.StartsWith("EMAIL-LINKS", StringComparison.Ordinal));
        line.ShouldContain("cc=TEST-cc@test.local");
        line.ShouldContain("links=https://portal.test.local/a | http://portal.test.local/b?x=1");
        noLinks.Messages(LogLevel.Information).Single(m => m.StartsWith("EMAIL-LINKS", StringComparison.Ordinal))
            .ShouldContain("cc=(none)");
        noLinks.Messages(LogLevel.Information).Single(m => m.StartsWith("EMAIL-LINKS", StringComparison.Ordinal))
            .ShouldContain("links=(none)");
        off.Messages(LogLevel.Information).ShouldNotContain(m => m.StartsWith("EMAIL-LINKS", StringComparison.Ordinal));
        off.Sender.Sent.ShouldHaveSingleItem();
    }

    // ------------------------------------------------------------------ packet sends

    [Fact]
    public async Task A_packet_send_attaches_the_pdf_under_its_file_name_and_reports_success()
    {
        var world = new World();
        world.Packets.GetAttachmentAsync(AppointmentId, PacketKind.Patient, Arg.Any<CancellationToken>())
            .Returns(new PacketAttachment(new byte[] { 7, 8, 9 }, "TEST-00042_Patient Packet_06052031_110000.pdf", "application/pdf"));

        await world.Job.ExecuteAsync(Args(PatientPacket(), "TEST-cc@test.local", " "));

        var sent = world.Sender.Sent.ShouldHaveSingleItem();
        sent.To.ShouldBe("TEST-patient@test.local");
        sent.Cc.ShouldBe(new[] { "TEST-cc@test.local" });
        var attachment = sent.Attachments.ShouldHaveSingleItem();
        attachment.Name.ShouldBe("TEST-00042_Patient Packet_06052031_110000.pdf");
        attachment.MediaType.ShouldBe("application/pdf");
        attachment.Disposition.ShouldBe(DispositionTypeNames.Attachment);
        attachment.DispositionFileName.ShouldBe("TEST-00042_Patient Packet_06052031_110000.pdf");
        attachment.Bytes.ShouldBe(new byte[] { 7, 8, 9 });
        await world.Packets.Received(1).NotifySendCompletedAsync(PacketId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_packet_that_is_not_generated_skips_the_email_and_reports_no_send()
    {
        var world = new World();
        world.Packets.GetAttachmentAsync(AppointmentId, PacketKind.Patient, Arg.Any<CancellationToken>())
            .Returns((PacketAttachment?)null);

        await world.Job.ExecuteAsync(Args(PatientPacket(), "TEST-cc@test.local"));

        world.Sender.Sent.ShouldBeEmpty();
        await world.Packets.DidNotReceiveWithAnyArgs().NotifySendCompletedAsync(default, default, default);
        world.Messages(LogLevel.Warning).ShouldContain(m => m.Contains("is not Generated; skipping"));
    }

    [Fact]
    public async Task A_failed_packet_send_propagates_for_retry_and_still_reports_the_failure()
    {
        var world = new World();
        world.Sender.FailWith = new SmtpException("TEST relay down");
        world.Packets.GetAttachmentAsync(AppointmentId, PacketKind.Patient, Arg.Any<CancellationToken>())
            .Returns(new PacketAttachment(new byte[] { 1 }, "TEST.pdf", "application/pdf"));

        var ex = await Should.ThrowAsync<SmtpException>(() => world.Job.ExecuteAsync(Args(PatientPacket())));

        ex.Message.ShouldBe("TEST relay down");
        world.Sender.Sent.ShouldBeEmpty();
        await world.Packets.Received(1).NotifySendCompletedAsync(PacketId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_throwing_send_completed_callback_does_not_undo_a_delivered_packet_email()
    {
        var world = new World();
        world.Packets.GetAttachmentAsync(AppointmentId, PacketKind.Patient, Arg.Any<CancellationToken>())
            .Returns(new PacketAttachment(new byte[] { 1 }, "TEST.pdf", "application/pdf"));
        world.Packets.NotifySendCompletedAsync(PacketId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("TEST callback failure"));

        await world.Job.ExecuteAsync(Args(PatientPacket()));

        world.Sender.Sent.ShouldHaveSingleItem().Attachments.ShouldHaveSingleItem();
        world.Messages(LogLevel.Warning).ShouldContain(m => m.Contains("NotifySendCompletedAsync threw"));
    }
}
