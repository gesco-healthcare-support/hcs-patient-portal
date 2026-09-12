using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Resolves publishable document entries. An interface so the trigger handlers can be tested against
/// a fake instead of three repositories.
/// </summary>
public interface IDocumentListResolver
{
    /// <summary>Every fetchable document and packet for the appointment, oldest first.</summary>
    Task<List<IntakeDocumentEntry>> ResolveAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One uploaded document, or null when it has no bytes in MinIO (a queued placeholder). Null is
    /// the caller's signal to publish nothing rather than an object key that would 404.
    /// </summary>
    Task<IntakeDocumentEntry?> ResolveDocumentAsync(
        Guid documentId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>The appointment's generated packets, excluding any still rendering or failed.</summary>
    Task<List<IntakeDocumentEntry>> ResolvePacketsAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default);
}
