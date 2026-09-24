using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using Volo.Abp;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// One dispatch the handler under test asked for. <see cref="To"/> is null for a plain
/// <c>DispatchAsync</c> fan-out; <see cref="Recipients"/> is the To (if any) followed by every
/// CC or fan-out address, so a test can ask "who was emailed" either way.
/// </summary>
public sealed record RecordedDispatch(
    string TemplateCode,
    string? To,
    IReadOnlyList<string> Recipients,
    IReadOnlyDictionary<string, object?> Variables);

/// <summary>
/// Stands in for <see cref="INotificationDispatcher"/> so a handler test can assert exactly what
/// would have been emailed without rendering a template or enqueueing a send job. It is the only
/// send path these handlers have, so no email sender is ever resolved while it is installed.
/// </summary>
public sealed class RecordingNotificationDispatcher : INotificationDispatcher
{
    private readonly List<RecordedDispatch> _dispatches = new();

    public IReadOnlyList<RecordedDispatch> Dispatches => _dispatches;

    /// <summary>When set, every dispatch throws the "template not found" business error instead.</summary>
    public bool ThrowTemplateNotFound { get; set; }

    /// <summary>
    /// Forgets everything recorded so far. The rig seeds its data AFTER this fake is installed, so a
    /// test clears it before acting to count only what the handler under test sent.
    /// </summary>
    public void Clear() => _dispatches.Clear();

    public Task DispatchAsync(
        string templateCode,
        IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables,
        string contextTag,
        PacketAttachmentRef? packetRef = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured(templateCode);
        _dispatches.Add(new RecordedDispatch(
            templateCode, To: null, recipients.Select(r => r.Email).ToList(), variables));
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
        ThrowIfConfigured(templateCode);
        var all = new List<string> { to.Email };
        all.AddRange(cc.Select(r => r.Email));
        _dispatches.Add(new RecordedDispatch(templateCode, to.Email, all, variables));
        return Task.CompletedTask;
    }

    private void ThrowIfConfigured(string templateCode)
    {
        if (ThrowTemplateNotFound)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
                .WithData("templateCode", templateCode);
        }
    }
}

/// <summary>
/// Stands in for <see cref="IAppointmentRecipientResolver"/> so the stakeholder set a status email
/// fans out to is chosen by the test rather than by whatever the seeded parties happen to resolve.
/// </summary>
public sealed class StubAppointmentRecipientResolver : IAppointmentRecipientResolver
{
    public List<SendAppointmentEmailArgs> Recipients { get; } = new();

    public Task<List<SendAppointmentEmailArgs>> ResolveAsync(Guid appointmentId, NotificationKind kind) =>
        Task.FromResult(Recipients.Select(r => new SendAppointmentEmailArgs
        {
            To = r.To,
            Role = r.Role,
            IsRegistered = r.IsRegistered,
        }).ToList());
}

/// <summary>Creates users in named roles inside whatever tenant scope the caller has entered.</summary>
public static class RoleUserSeeder
{
    /// <summary>Creates the role if missing, then a user holding it, and returns the user's email.</summary>
    public static async Task<string> CreateUserInRoleAsync(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        Guid? tenantId,
        string roleName,
        string label)
    {
        if (await roleManager.FindByNameAsync(roleName) == null)
        {
            (await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, tenantId)))
                .Succeeded.ShouldBeTrueOrThrow($"create role {roleName}");
        }

        var email = $"TEST-{label}-{Guid.NewGuid():N}@test.local";
        var user = new IdentityUser(Guid.NewGuid(), $"TEST-{label}-{Guid.NewGuid():N}", email, tenantId);
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrueOrThrow($"create user {label}");
        (await userManager.AddToRoleAsync(user, roleName)).Succeeded.ShouldBeTrueOrThrow($"add {label} to {roleName}");
        return email;
    }

    private static void ShouldBeTrueOrThrow(this bool succeeded, string what)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException($"Test setup failed: {what}.");
        }
    }
}
