using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Where an appointment's Responsible User mail goes (2026-10-26): to that person while their account is
/// switched on. Once it is switched off -- typically an operator who was unassigned or left -- the mail goes
/// to the office's notification inbox instead, and when the office has none it goes nowhere. It never goes
/// to a switched-off account, because that person no longer works the office's cases.
///
/// <para>Shared by the approval email's responsible-user leg and the user-query routing, so the rule is in
/// one place. Runs in the office's tenant scope, which is where both callers read the inbox setting.</para>
/// </summary>
public class ResponsibleUserRecipientResolver : ITransientDependency
{
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<ResponsibleUserRecipientResolver> _logger;

    public ResponsibleUserRecipientResolver(
        IRepository<IdentityUser, Guid> userRepository,
        ISettingProvider settingProvider,
        ILogger<ResponsibleUserRecipientResolver> logger)
    {
        _userRepository = userRepository;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    /// <summary>
    /// The address for <paramref name="responsibleUserId"/>, or null when there is none: no id, no such
    /// user, no address, or a switched-off account in an office without an inbox.
    /// </summary>
    public virtual async Task<ResponsibleUserRecipient?> ResolveAsync(Guid? responsibleUserId)
    {
        if (!responsibleUserId.HasValue || responsibleUserId.Value == Guid.Empty)
        {
            return null;
        }

        var user = await _userRepository.FindAsync(responsibleUserId.Value);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        if (user.IsActive)
        {
            return new ResponsibleUserRecipient(user.Email!, user);
        }

        var officeInbox = (await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.OfficeEmail))?.Trim();
        if (!string.IsNullOrWhiteSpace(officeInbox))
        {
            return new ResponsibleUserRecipient(officeInbox, User: null);
        }

        // Ids only: no name or address reaches the log.
        _logger.LogWarning(
            "ResponsibleUserRecipientResolver: responsible user {UserId} is switched off and the office has no notification inbox; nothing is sent for them.",
            user.Id);
        return null;
    }
}

/// <summary>
/// A resolved Responsible User address. <see cref="User"/> is the person when the mail goes to them, and
/// null when it goes to the office inbox in their place.
/// </summary>
public sealed record ResponsibleUserRecipient(string Email, IdentityUser? User);
