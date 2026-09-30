using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Jobs;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Unit coverage for <see cref="OutboxEmailSender"/>, the seam the outbox drain sends through. It
/// hands each row to <see cref="SendAppointmentEmailJob"/> synchronously.
///
/// <para><b>NO MAIL CAN LEAVE.</b> The job itself is substituted -- its constructor only stores its
/// four arguments and <c>ExecuteAsync</c> is virtual -- so no email sender is ever resolved or
/// called. The result asserted is what the job was handed, and that a send failure reaches the
/// caller (the drain records a failure only if it sees one).</para>
/// </summary>
public class OutboxEmailSenderTests
{
    private static SendAppointmentEmailArgs Args() => new()
    {
        To = "TEST-recipient@test.local",
        Subject = "TEST-subject",
        Body = "<p>TEST-body</p>",
        Context = "TEST-context",
    };

    [Fact]
    public async Task SendAsync_HandsTheSameRowToTheSendJob()
    {
        var job = Substitute.For<SendAppointmentEmailJob>(null, null, null, null);
        var args = Args();

        await new OutboxEmailSender(job).SendAsync(args);

        await job.Received(1).ExecuteAsync(Arg.Is<SendAppointmentEmailArgs>(a => ReferenceEquals(a, args)));
    }

    /// <summary>
    /// A send failure must reach the drain unchanged: it is how the row gets marked failed and retried,
    /// instead of being recorded as sent.
    /// </summary>
    [Fact]
    public async Task SendAsync_ASendFailure_ReachesTheCaller()
    {
        var job = Substitute.For<SendAppointmentEmailJob>(null, null, null, null);
        job.ExecuteAsync(Arg.Any<SendAppointmentEmailArgs>())
            .Returns(Task.FromException(new InvalidOperationException("TEST-transport down")));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => new OutboxEmailSender(job).SendAsync(Args()));

        thrown.Message.ShouldBe("TEST-transport down");
    }
}
