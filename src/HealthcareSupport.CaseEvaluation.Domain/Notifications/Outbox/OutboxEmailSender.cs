using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Jobs;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Default <see cref="IOutboxEmailSender"/>: delegates to the existing
/// <see cref="SendAppointmentEmailJob"/> so the outbox drain reuses the exact
/// plain + packet-attachment + tenant-scope send path (T4 already makes SMTP
/// failures propagate). Only the trigger changes -- a drained outbox row instead
/// of a direct Hangfire enqueue.
/// </summary>
public class OutboxEmailSender : IOutboxEmailSender, ITransientDependency
{
    private readonly SendAppointmentEmailJob _sendJob;

    public OutboxEmailSender(SendAppointmentEmailJob sendJob)
    {
        _sendJob = sendJob;
    }

    public Task SendAsync(SendAppointmentEmailArgs args) => _sendJob.ExecuteAsync(args);
}
