namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// A required document (a master <c>Document</c> the active package template
/// expects) that is still outstanding for an appointment, with its current
/// state. <see cref="DocumentId"/> is internal; the UI shows <see cref="Name"/>.
/// </summary>
public sealed record MissingRequiredDocument(Guid DocumentId, string Name, RequiredDocumentState State);

/// <summary>
/// Pure projection of "which required documents are still outstanding" for one
/// appointment. A requirement (a master Document id) is <b>satisfied</b> only
/// when an uploaded document for the appointment references it
/// (<c>AppointmentDocument.SourceDocumentId</c>) AND is
/// <see cref="DocumentStatus.Accepted"/>. Otherwise it is reported in the
/// most-actionable state among its rows: <see cref="RequiredDocumentState.AwaitingReview"/>
/// (an Uploaded row) before <see cref="RequiredDocumentState.Rejected"/> before
/// <see cref="RequiredDocumentState.NotUploaded"/> (only Pending rows, or none).
///
/// Kept dependency-free (primitive inputs, no entities) so it is unit-testable
/// without a database, and so the AppService can feed it a union of the active
/// package templates' documents. Required ids are de-duplicated (first name
/// wins) and input order is preserved.
/// </summary>
public static class RequiredDocumentEvaluator
{
    public static IReadOnlyList<MissingRequiredDocument> Evaluate(
        IEnumerable<(Guid DocumentId, string Name)> required,
        IEnumerable<(Guid? SourceDocumentId, DocumentStatus Status)> existing)
    {
        var statusesBySource = existing
            .Where(e => e.SourceDocumentId.HasValue)
            .GroupBy(e => e.SourceDocumentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Status).ToList());

        var result = new List<MissingRequiredDocument>();
        var seen = new HashSet<Guid>();

        foreach (var (documentId, name) in required)
        {
            if (!seen.Add(documentId))
            {
                continue; // union of multiple active packages: first occurrence wins
            }

            var statuses = statusesBySource.GetValueOrDefault(documentId);
            if (statuses != null && statuses.Contains(DocumentStatus.Accepted))
            {
                continue; // satisfied
            }

            var state =
                statuses?.Contains(DocumentStatus.Uploaded) == true ? RequiredDocumentState.AwaitingReview
                : statuses?.Contains(DocumentStatus.Rejected) == true ? RequiredDocumentState.Rejected
                : RequiredDocumentState.NotUploaded;

            result.Add(new MissingRequiredDocument(documentId, name, state));
        }

        return result;
    }
}
