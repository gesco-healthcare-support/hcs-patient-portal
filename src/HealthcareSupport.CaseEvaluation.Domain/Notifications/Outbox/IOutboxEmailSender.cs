using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// Seam over the actual email transport used by the outbox drain. The default
/// implementation delegates to the existing <c>SendAppointmentEmailJob</c> so
/// the tested plain + attachment + tenant-scope send logic is reused verbatim;
/// the interface exists so the drain is unit-testable without SMTP.
/// </summary>
public interface IOutboxEmailSender
{
    /// <summary>
    /// Sends one email. Throws on transport failure so the drain marks the row
    /// Failed (and reschedules) rather than Sent.
    /// </summary>
    Task SendAsync(SendAppointmentEmailArgs args);
}
