using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- loads the staff email stops for a send (see <see cref="EmailStopPolicy"/>, the one
/// implementation). An interface so the sending paths can be unit-tested without a database.
/// </summary>
public interface IEmailStopPolicy
{
    /// <summary>The stops for one send: every address at once, plus the case stops of one appointment.</summary>
    Task<EmailStopLookup> LookupAsync(IEnumerable<string?> addresses, Guid? appointmentId);

    /// <summary>The stops for many (case, address) pairs: one host query and one office query in all.</summary>
    Task<EmailStopLookup> LookupBatchAsync(IEnumerable<(Guid? AppointmentId, string? Address)> pairs);
}
