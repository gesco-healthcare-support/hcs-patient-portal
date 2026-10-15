using System;
using System.Net.Mail;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments.Jobs;

/// <summary>
/// Unit tests for <see cref="SendAppointmentEmailJob"/> -- T4: the plain (non-
/// attachment) email path must NOT swallow SMTP failures, else every plain
/// notification is silently dropped on a transient relay outage.
/// </summary>
public class SendAppointmentEmailJobTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPlainEmailSendFails_PropagatesForHangfireRetry()
    {
        var emailSender = Substitute.For<IEmailSender>();
        emailSender
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .ThrowsAsync(new SmtpException("transient relay failure"));

        var attachmentProvider = Substitute.For<IPacketAttachmentProvider>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());
        var configuration = Substitute.For<IConfiguration>();

        var job = new SendAppointmentEmailJob(
            emailSender, attachmentProvider, currentTenant, configuration);

        var args = new SendAppointmentEmailArgs
        {
            To = "pat@x.com",
            Subject = "s",
            Body = "b",
            Context = "Transition/Approved/appt1",
            // no PacketRef -> plain path (SendPlainAsync)
        };

        var ex = await Record.ExceptionAsync(() => job.ExecuteAsync(args));

        // T4: the swallow is removed -- a transient SMTP failure MUST propagate so
        // Hangfire's AutomaticRetry engages and the send dead-letters, instead of
        // the job reporting Succeeded and silently dropping the notification.
        ex.ShouldNotBeNull();
        ex.ShouldBeOfType<SmtpException>();
    }
}
