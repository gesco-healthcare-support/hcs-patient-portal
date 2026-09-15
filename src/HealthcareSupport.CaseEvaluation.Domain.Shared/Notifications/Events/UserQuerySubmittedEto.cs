namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Raised when an external user submits a free-text question through the
/// "Help / Need Question?" modal. Carries the persisted query id plus the
/// transient routing inputs (the optional confirmation number the user
/// supplied) so the email handler can route to the appointment's
/// primary-responsible internal user or fall back to the IT-Admin pool --
/// the OLD UserQueryDomain.Add fan-out.
///
/// <para>The confirmation number is intentionally NOT persisted on the
/// UserQuery row (OLD kept it [NotMapped]); it travels on the event only.</para>
/// </summary>
public class UserQuerySubmittedEto
{
    public Guid UserQueryId { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional appointment confirmation number supplied at submit time.
    /// When present and it resolves to an Approved appointment, the email
    /// routes to that appointment's responsible internal user; otherwise it
    /// broadcasts to all IT-Admins.
    /// </summary>
    public string? RequestConfirmationNumber { get; set; }

    /// <summary>Identity user who submitted the query.</summary>
    public Guid? SubmitterUserId { get; set; }

    public Guid? TenantId { get; set; }

    public DateTime OccurredAt { get; set; }
}
