using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// A hand-set <see cref="OfficeNotificationRecipients"/> for handler unit tests: the office inbox and the
/// Intake Staff are whatever the test lists, so no identity store or setting provider is needed. The
/// real lookups (active-only, per-role) are pinned in the EF tests. Synthetic addresses only.
/// </summary>
internal sealed class FakeOfficeRecipients : OfficeNotificationRecipients
{
    public List<string> Intake { get; } = new();

    public string? Inbox { get; set; }

    public FakeOfficeRecipients()
        : base(null!, null!, null!, null!)
    {
    }

    public override Task<List<NotificationRecipient>> GetOfficeInboxAsync(string contextTag) =>
        Task.FromResult(Inbox == null
            ? new List<NotificationRecipient>()
            : new List<NotificationRecipient> { new(email: Inbox, role: RecipientRole.OfficeAdmin) });

    public override Task<List<NotificationRecipient>> GetIntakeAsync() =>
        Task.FromResult(Intake
            .Select(e => new NotificationRecipient(email: e, role: RecipientRole.OfficeAdmin, isRegistered: true))
            .ToList());
}
