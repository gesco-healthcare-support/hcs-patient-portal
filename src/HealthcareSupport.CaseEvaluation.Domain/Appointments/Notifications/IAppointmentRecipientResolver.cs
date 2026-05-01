using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;

namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// W2-10: shared "all parties to the case" recipient resolver. Walks the
/// appointment + every linked party entity (booker, applicant attorney,
/// defense attorney, claim examiner, primary insurance contact, employer,
/// office mailbox) and returns one <see cref="SendAppointmentEmailArgs"/>
/// per addressable recipient. Used by both the per-event handlers
/// (Submission / StatusChange) and the W2-10 recurring jobs.
///
/// Skips recipients with empty / null email and logs a warning per skip.
/// Tenant scope: caller must be inside the tenant context (the per-event
/// handlers run inside a UoW; the recurring jobs wrap each tenant-pass in
/// <c>_currentTenant.Change(tenantId)</c>).
/// </summary>
public interface IAppointmentRecipientResolver
{
    Task<List<SendAppointmentEmailArgs>> ResolveAsync(Guid appointmentId, NotificationKind kind);
}
