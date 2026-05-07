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
        Define(context, CaseEvaluationSettings.BookingPolicy.LeadTimeMinutes,             defaultValue: "1440");
        Define(context, CaseEvaluationSettings.BookingPolicy.MaxHorizonQmeMinutes,        defaultValue: "129600");
        Define(context, CaseEvaluationSettings.BookingPolicy.MaxHorizonAmeMinutes,        defaultValue: "129600");
        Define(context, CaseEvaluationSettings.BookingPolicy.MaxHorizonOtherMinutes,      defaultValue: "129600");
        Define(context, CaseEvaluationSettings.BookingPolicy.AppointmentDurationMinutes,  defaultValue: "60");
        Define(context, CaseEvaluationSettings.BookingPolicy.AppointmentDueDays,          defaultValue: "7");

        // Scheduling policy (CCR Title 8 Sections 31.5 / 33 / 34 -- legal staff sign-off pending).
        Define(context, CaseEvaluationSettings.SchedulingPolicy.CancelWindowMinutes,       defaultValue: "2880");
        Define(context, CaseEvaluationSettings.SchedulingPolicy.AutoCancelCutoffMinutes,   defaultValue: "1440");
        Define(context, CaseEvaluationSettings.SchedulingPolicy.ReminderCutoffMinutes,     defaultValue: "1440");
        Define(context, CaseEvaluationSettings.SchedulingPolicy.PendingAppointmentOverdueNotificationDays, defaultValue: "3");

        // Documents policy
        Define(context, CaseEvaluationSettings.DocumentsPolicy.JointDeclarationUploadCutoffDays, defaultValue: "7");
        Define(context, CaseEvaluationSettings.DocumentsPolicy.PackageDocumentReminderDays,      defaultValue: "7");

        // Notifications policy
        Define(context, CaseEvaluationSettings.NotificationsPolicy.CcEmailAddresses, defaultValue: "");
        Define(context, CaseEvaluationSettings.NotificationsPolicy.OfficeEmail,      defaultValue: "");
        // 2026-05-06 -- PortalBaseUrl default targets the Falkinstein subdomain so
        // tenant-scoped requests (email-confirmation, change-request links, etc.)
        // land on the right tenant context out of the box. Override per-tenant
        // once Phase 1B multi-tenant URL routing ships.
        Define(context, CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,    defaultValue: "http://falkinstein.localhost:4200");
        // S-6.1: AuthServer base URL for tenant-pre-filled register links in
        // "register as [role]" emails sent to non-registered parties.
        Define(context, CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl, defaultValue: "https://localhost:44368");

        // W2-10: 10 reminder-policy settings (CCR Sec. 31.5 + Sec. 34(e) + appointment-day).
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec31_5ElapsedDayAnchors,    defaultValue: "30,60,75,85,90");
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec34eElapsedDayAnchors,     defaultValue: "45,55");
        Define(context, CaseEvaluationSettings.RemindersPolicy.AppointmentDayTMinusAnchors, defaultValue: "7,1");
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec31_5Cron,                 defaultValue: "0 8 * * *");
        Define(context, CaseEvaluationSettings.RemindersPolicy.Sec34eCron,                  defaultValue: "0 8 * * *");
        Define(context, CaseEvaluationSettings.RemindersPolicy.AppointmentDayCron,          defaultValue: "0 7 * * *");
        Define(context, CaseEvaluationSettings.RemindersPolicy.ReminderTimezoneId,          defaultValue: "America/Los_Angeles");
        Define(context, CaseEvaluationSettings.RemindersPolicy.RemindersEnabled,            defaultValue: "true");
        Define(context, CaseEvaluationSettings.RemindersPolicy.ReminderCcEmail,             defaultValue: "");
        Define(context, CaseEvaluationSettings.RemindersPolicy.ReminderSignoff,             defaultValue: "");

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
