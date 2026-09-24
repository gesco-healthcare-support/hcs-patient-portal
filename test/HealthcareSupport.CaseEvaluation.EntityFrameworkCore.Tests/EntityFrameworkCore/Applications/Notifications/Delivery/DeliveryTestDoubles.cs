using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// One dispatch a handler asked for, including the packet it would attach. <see cref="Recipients"/>
/// is every address the message would reach.
/// </summary>
public sealed record SentNotification(
    string TemplateCode,
    IReadOnlyList<string> Recipients,
    IReadOnlyDictionary<string, object?> Variables,
    PacketAttachmentRef? Packet);

/// <summary>
/// Records what would have been emailed in place of <see cref="INotificationDispatcher"/>, which is
/// the only send path the handlers under test have, so no email sender is ever resolved.
///
/// <para>A sibling of the recorder in the parent folder's test doubles (added in a parallel pull
/// request). It is kept separate so each PR stands alone; merging the two is a backlog item.</para>
/// </summary>
public sealed class NotificationRecorder : INotificationDispatcher
{
    private readonly List<SentNotification> _sent = new();

    public IReadOnlyList<SentNotification> Sent => _sent;

    /// <summary>The rig seeds after this is installed; clear before acting to count only the test's sends.</summary>
    public void Clear() => _sent.Clear();

    public Task DispatchAsync(
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null,
        CancellationToken cancellationToken = default)
    {
        _sent.Add(new SentNotification(templateCode, recipients.Select(r => r.Email).ToList(), variables, packetRef));
        return Task.CompletedTask;
    }

    public Task DispatchToWithCcAsync(
        string templateCode,
        NotificationRecipient to,
        IReadOnlyCollection<NotificationRecipient> cc,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null,
        CancellationToken cancellationToken = default)
    {
        var all = new List<string> { to.Email };
        all.AddRange(cc.Select(r => r.Email));
        _sent.Add(new SentNotification(templateCode, all, variables, packetRef));
        return Task.CompletedTask;
    }
}

/// <summary>Returns the parties each test chooses, instead of whatever the seeded rows resolve to.</summary>
public sealed class ChosenRecipients : IAppointmentRecipientResolver
{
    public List<SendAppointmentEmailArgs> Parties { get; } = new();

    public Task<List<SendAppointmentEmailArgs>> ResolveAsync(Guid appointmentId, NotificationKind kind) =>
        Task.FromResult(Parties.Select(p => new SendAppointmentEmailArgs
        {
            To = p.To,
            Role = p.Role,
            IsRegistered = p.IsRegistered,
        }).ToList());
}

/// <summary>Creates a user in a named role inside the tenant scope the caller has entered.</summary>
public static class StaffSeeder
{
    public static async Task<(string Email, string UserName)> CreateAsync(
        IdentityUserManager users, IdentityRoleManager roles, Guid? tenantId, string roleName, string label)
    {
        if (await roles.FindByNameAsync(roleName) == null)
        {
            Require((await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, tenantId))).Succeeded, $"role {roleName}");
        }

        var email = $"TEST-{label}-{Guid.NewGuid():N}@test.local";
        var userName = $"TEST-{label}-{Guid.NewGuid():N}";
        var user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId);
        Require((await users.CreateAsync(user)).Succeeded, $"user {label}");
        Require((await users.AddToRoleAsync(user, roleName)).Succeeded, $"{label} into {roleName}");
        return (email, userName);
    }

    private static void Require(bool succeeded, string what)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException($"Test setup failed: {what}.");
        }
    }
}
