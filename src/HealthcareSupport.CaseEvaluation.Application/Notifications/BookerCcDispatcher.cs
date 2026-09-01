using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Group F (2026-06-09): shared ex-parte addressing helper, extracted from
/// <c>StatusChangeEmailHandler</c>. Sends ONE message addressed To the booker
/// with the other parties + the per-tenant office CC list CC'd, via
/// <see cref="INotificationDispatcher.DispatchToWithCcAsync"/>.
///
/// <para>Used by the status-change emails (Group C) and the consolidated
/// reminder (Group F) so the rule -- "one notice To the booker, everyone else
/// CC'd; never separate per-party copies of the same notice" -- lives in one
/// place instead of being duplicated per handler.</para>
/// </summary>
public class BookerCcDispatcher : ITransientDependency
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly CcRecipientAppender _ccAppender;
    private readonly ILogger<BookerCcDispatcher> _logger;

    public BookerCcDispatcher(
        INotificationDispatcher dispatcher,
        CcRecipientAppender ccAppender,
        ILogger<BookerCcDispatcher> logger)
    {
        _dispatcher = dispatcher;
        _ccAppender = ccAppender;
        _logger = logger;
    }

    /// <summary>
    /// Partitions <paramref name="stakeholders"/> into To = the booker and
    /// CC = everyone else, appends the per-tenant office CC list
    /// (<c>SystemParameter.CcEmailIds</c>), and dispatches one email. No-op
    /// (logs) when no To recipient can be resolved.
    /// </summary>
    public virtual async Task DispatchToBookerWithCcAsync(
        string templateCode,
        string? bookerEmail,
        IReadOnlyCollection<NotificationRecipient> stakeholders,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null)
    {
        var (to, cc) = PartitionToBookerCc(stakeholders, bookerEmail);
        if (to == null)
        {
            _logger.LogInformation(
                "BookerCcDispatcher: no To recipient resolved for {Context}; skipping send.",
                contextTag);
            return;
        }

        // The dispatcher dedups a CC address equal to the To address.
        await _ccAppender.AppendAsync(cc, contextTagForLogging: contextTag);

        await _dispatcher.DispatchToWithCcAsync(
            templateCode: templateCode,
            to: to,
            cc: cc,
            variables: variables,
            contextTag: contextTag,
            packetRef: packetRef);
    }

    /// <summary>
    /// Splits recipients into To = the booker and CC = everyone else. To = the
    /// party whose email matches <paramref name="bookerEmail"/>; if the booker
    /// is not a party, a To recipient is built from the booker email directly.
    /// Falls back to the patient party, then the first party, when no booker
    /// email is available.
    /// </summary>
    public static (NotificationRecipient? To, List<NotificationRecipient> Cc) PartitionToBookerCc(
        IReadOnlyCollection<NotificationRecipient> recipients,
        string? bookerEmail)
    {
        NotificationRecipient? to = null;
        if (!string.IsNullOrWhiteSpace(bookerEmail))
        {
            to = recipients.FirstOrDefault(r =>
                    string.Equals(r.Email, bookerEmail, StringComparison.OrdinalIgnoreCase))
                ?? new NotificationRecipient(
                    email: bookerEmail!,
                    role: RecipientRole.Patient,
                    isRegistered: true);
        }

        to ??= recipients.FirstOrDefault(r =>
                (r.Role ?? RecipientRole.Patient) == RecipientRole.Patient)
            ?? recipients.FirstOrDefault();

        if (to == null)
        {
            return (null, new List<NotificationRecipient>());
        }

        var cc = recipients
            .Where(r => !string.Equals(r.Email, to.Email, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return (to, cc);
    }
}
