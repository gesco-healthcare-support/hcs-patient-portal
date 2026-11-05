using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Sends one templated email with SEVERAL packets attached (2026-11-03: the office inbox's packet copy
/// carries the attorney / claim examiner packet and the patient packet together). A separate narrow
/// interface rather than another parameter on <see cref="INotificationDispatcher"/>: every other send
/// attaches at most one packet, and widening the main interface would touch every implementer of it.
/// <see cref="NotificationDispatcher"/> implements both, so the render and the outbox write are shared.
/// </summary>
public interface IPacketCopyDispatcher
{
    /// <summary>
    /// Renders <paramref name="templateCode"/> once and writes one outbox row per recipient, each
    /// attaching every entry of <paramref name="packetRefs"/> in order (the first is the primary
    /// reference, the rest ride beside it). Empty recipients or an empty reference list is a no-op.
    /// </summary>
    Task DispatchWithPacketsAsync(
        NotificationCase notificationCase,
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        IReadOnlyList<PacketAttachmentRef> packetRefs,
        CancellationToken cancellationToken = default);
}
