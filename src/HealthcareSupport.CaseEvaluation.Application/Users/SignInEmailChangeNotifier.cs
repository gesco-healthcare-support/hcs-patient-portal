using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Emailing;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Volo.Abp.Account.Emailing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Users;

/// <summary>
/// 2026-10-27 (Levon) -- what an administrator's change to someone's sign-in email sends. The edit
/// leaves the new address unconfirmed (<see cref="UserExtendedAppService.ConfirmedAfterEdit"/>), so
/// without a message the person could not confirm it and would not know it had changed.
///
/// <list type="bullet">
///   <item>An account that was activated gets ONE confirm-your-email link at the new address. It opens
///         the email confirmation page, which only confirms: the password and everything else stay as
///         they were.</item>
///   <item>An account never activated gets a fresh activation link at the new address, because the
///         one it was sent died with the change. Activating it is how such an account chooses its
///         first password.</item>
///   <item>The previous address gets a short notice with no link, so a change the person did not
///         expect does not go unnoticed. The new address appears masked in it.</item>
/// </list>
///
/// <para>Each change is its own occurrence (<see cref="NotificationOccurrence.PerRequest"/>), so a
/// second change sends again. Called after the change is saved and after every other change to the
/// user: a later change to the security stamp would kill the link.</para>
/// </summary>
public class SignInEmailChangeNotifier : ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IAccountEmailer _accountEmailer;
    private readonly IAccountActivationSender _activationSender;
    private readonly INotificationDispatcher _notificationDispatcher;

    public SignInEmailChangeNotifier(
        IdentityUserManager userManager,
        IAccountEmailer accountEmailer,
        IAccountActivationSender activationSender,
        INotificationDispatcher notificationDispatcher)
    {
        _userManager = userManager;
        _accountEmailer = accountEmailer;
        _activationSender = activationSender;
        _notificationDispatcher = notificationDispatcher;
    }

    /// <summary>Whether an edit changed the sign-in address, compared as the edit compares it.</summary>
    public static bool AddressChanged(string? previousEmail, string? newEmail) =>
        !string.Equals(previousEmail?.Trim(), newEmail?.Trim(), StringComparison.OrdinalIgnoreCase);

    public virtual async Task NotifyAsync(IdentityUser user, string? previousEmail, bool wasActivated)
    {
        if (wasActivated)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _accountEmailer.SendEmailConfirmationLinkAsync(user, token, "MVC");
        }
        else
        {
            await _activationSender.SendAsync(user, NotificationOccurrence.PerRequest($"AccountActivation/EmailChanged/{user.Id}"));
        }

        if (string.IsNullOrWhiteSpace(previousEmail))
        {
            return;
        }

        await _notificationDispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.AccountEmailChanged,
            recipients: new[]
            {
                new NotificationRecipient(email: previousEmail.Trim(), role: RecipientRole.Patient, isRegistered: true),
            },
            variables: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["PatientFirstName"] = user.Name ?? string.Empty,
                ["PatientLastName"] = user.Surname ?? string.Empty,
                ["NewEmailMasked"] = EmailAddressVisibility.Mask(user.Email) ?? string.Empty,
            },
            contextTag: NotificationOccurrence.PerRequest($"AccountEmailChanged/{user.Id}"));
    }
}
