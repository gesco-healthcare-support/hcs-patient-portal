using HealthcareSupport.CaseEvaluation.Localization;
using Volo.Abp.Identity.Settings;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Settings;

/// <summary>
/// Registers 12 CaseEvaluation policy settings with ABP's SettingDefinitionManager.
/// Tied to OLD's `SystemParameter` columns per Q8 lock 2026-04-24. All settings
/// are visible to clients and inheritable so per-tenant overrides resolve through
/// `ISettingProvider`.
/// </summary>
public class CaseEvaluationSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // Booking policy
        Define(context, CaseEvaluationSettings.BookingPolicy.LeadTimeMinutes, defaultValue: "1440");
        Define(context, CaseEvaluationSettings.BookingPolicy.MaxHorizonQmeMinutes, defaultValue: "129600");
        Define(context, CaseEvaluationSettings.BookingPolicy.MaxHorizonAmeMinutes, defaultValue: "129600");
        Define(context, CaseEvaluationSettings.BookingPolicy.MaxHorizonOtherMinutes, defaultValue: "129600");
        Define(context, CaseEvaluationSettings.BookingPolicy.AppointmentDurationMinutes, defaultValue: "60");
        Define(context, CaseEvaluationSettings.BookingPolicy.AppointmentDueDays, defaultValue: "7");

        // Scheduling policy (CCR Title 8 Sections 31.5 / 33 / 34 -- legal staff sign-off pending).
        Define(context, CaseEvaluationSettings.SchedulingPolicy.CancelWindowMinutes, defaultValue: "2880");
        Define(context, CaseEvaluationSettings.SchedulingPolicy.AutoCancelCutoffMinutes, defaultValue: "1440");
        Define(context, CaseEvaluationSettings.SchedulingPolicy.ReminderCutoffMinutes, defaultValue: "1440");
        Define(context, CaseEvaluationSettings.SchedulingPolicy.PendingAppointmentOverdueNotificationDays, defaultValue: "3");

        // Documents policy
        Define(context, CaseEvaluationSettings.DocumentsPolicy.JointDeclarationUploadCutoffDays, defaultValue: "7");
        Define(context, CaseEvaluationSettings.DocumentsPolicy.PackageDocumentReminderDays, defaultValue: "7");

        // Notifications policy
        Define(context, CaseEvaluationSettings.NotificationsPolicy.CcEmailAddresses, defaultValue: "");
        Define(context, CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, defaultValue: "");
        // 2026-05-06 -- PortalBaseUrl default targets the Falkinstein subdomain so
        // tenant-scoped requests (email-confirmation, change-request links, etc.)
        // land on the right tenant context out of the box. Override per-tenant
        // once Phase 1B multi-tenant URL routing ships.
        Define(context, CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl, defaultValue: "http://falkinstein.localhost:4200");
        // S-6.1: AuthServer base URL for tenant-pre-filled register links in
        // "register as [role]" emails sent to non-registered parties.
        // 2026-05-07 (Wave 3 #17.1): default flipped from
        // "https://localhost:44368" to the Falkinstein subdomain on HTTP so
        // the AuthServer Razor pages -- which the Docker compose binds to
        // 127.0.0.1:44368 plain HTTP -- are reachable from email links in
        // dev/Phase 1A. Override per-tenant once Phase 1B HTTPS dev wiring
        // ships. The non-tenant-aware "https://localhost:44368" stranded
        // recipients on a host that did not respond.
        Define(context, CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl, defaultValue: "http://falkinstein.localhost:44368");

        // W2-10: 10 reminder-policy settings (CCR Sec. 31.5 + Sec. 34(e) + appointment-day).
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec31_5ElapsedDayAnchors, defaultValue: "30,60,75,85,90");
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec34eElapsedDayAnchors, defaultValue: "45,55");
        Define(context, CaseEvaluationSettings.RemindersPolicy.AppointmentDayTMinusAnchors, defaultValue: "7,1");
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec31_5Cron, defaultValue: "0 8 * * *");
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec34eCron, defaultValue: "0 8 * * *");
        Define(context, CaseEvaluationSettings.RemindersPolicy.AppointmentDayCron, defaultValue: "0 7 * * *");
        Define(context, CaseEvaluationSettings.RemindersPolicy.ReminderTimezoneId, defaultValue: "America/Los_Angeles");
        Define(context, CaseEvaluationSettings.RemindersPolicy.RemindersEnabled, defaultValue: "true");
        Define(context, CaseEvaluationSettings.RemindersPolicy.ReminderCcEmail, defaultValue: "");
        Define(context, CaseEvaluationSettings.RemindersPolicy.ReminderSignoff, defaultValue: "");

        // G4 / F8 (Phase 9, 2026-05-04): forces email-verification before
        // login, matching OLD's IsVerified gate at
        // P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs:143.
        //
        // 2026-05-06 (Adrian directive): re-enabled. Previous Phase 1A
        // flip to "false" (2026-05-05) was a demo convenience because the
        // local SMTP rejects RFC-2606 *.test addresses; we now want OLD
        // parity. Users who registered with a *.test address before this
        // flip must either click a real verification link OR have
        // EmailConfirmed=1 set manually in AbpUsers. New registrations
        // need a working mailbox to actually receive the link.
        //
        // ABP setting key: "Abp.Identity.SignIn.RequireConfirmedEmail".
        var emailConfirmRequired = context.GetOrNull(
            IdentitySettingNames.SignIn.RequireConfirmedEmail);
        if (emailConfirmRequired != null)
        {
            emailConfirmRequired.DefaultValue = "true";
        }

        // Lockout policy (Adrian decision 2026-05-18, proposed-copy.md
        // section 2.9): bump ABP defaults from 5 attempts / 5 minutes
        // to 10 attempts / 1 hour. The LockedOut Razor page hardcodes
        // "Try again in 1 hour" so keep these in sync if the policy
        // ever changes.
        //
        // ABP setting keys:
        //   "Abp.Identity.Lockout.MaxFailedAccessAttempts" (integer count)
        //   "Abp.Identity.Lockout.LockoutDuration" (seconds)
        var maxFailedAttempts = context.GetOrNull(
            IdentitySettingNames.Lockout.MaxFailedAccessAttempts);
        if (maxFailedAttempts != null)
        {
            maxFailedAttempts.DefaultValue = "10";
        }

        var lockoutDuration = context.GetOrNull(
            IdentitySettingNames.Lockout.LockoutDuration);
        if (lockoutDuration != null)
        {
            lockoutDuration.DefaultValue = "3600";
        }

        // BUG-020 (2026-05-19) -- ABP's Volo.Abp.Emailing module declares
        // "Abp.Mailing.Smtp.Password" with IsEncrypted=true by default.
        // Every read through ISettingProvider calls
        // SettingEncryptionService.Decrypt, which Convert.FromBase64String's
        // the raw value. The Docker dev stack passes a plaintext password
        // via docker/appsettings.secrets.json (Settings:Abp.Mailing.Smtp.Password),
        // so the Base64 decode throws FormatException on every send. ABP's
        // catch block falls back to the raw plaintext (delivery still
        // succeeds), but the warning + stack trace pollute logs on every
        // SendAppointmentEmailJob invocation. Flipping IsEncrypted=false
        // here makes the setting honor the plaintext shape we already use
        // for the dev SMTP relay and silences OBS-11 base-64 warnings.
        //
        // Production deployments that genuinely want the password encrypted
        // at rest should pre-encrypt the value via
        // IStringEncryptionService.Encrypt and either flip this back to
        // IsEncrypted=true via a host-only override or move the secret to
        // a managed secret store (key vault / docker secret). Single-source
        // is better than mixed; the demo stack standardizes on plaintext.
        var smtpPassword = context.GetOrNull("Abp.Mailing.Smtp.Password");
        if (smtpPassword != null)
        {
            smtpPassword.IsEncrypted = false;
        }
    }

    private static void Define(ISettingDefinitionContext context, string name, string defaultValue)
    {
        // Localization keys live in `Domain.Shared/Localization/CaseEvaluation/en.json`.
        // Naming convention: `Setting:<setting-name>` for DisplayName and
        // `Setting:<setting-name>:Description` for Description.
        context.Add(new SettingDefinition(
            name: name,
            defaultValue: defaultValue,
            displayName: L($"Setting:{name}"),
            description: L($"Setting:{name}:Description"),
            isVisibleToClients: true,
            isInherited: true,
            isEncrypted: false));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<CaseEvaluationResource>(name);
    }
}
