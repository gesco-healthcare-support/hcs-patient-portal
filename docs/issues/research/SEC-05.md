[Home](../../INDEX.md) > [Issues](../) > Research > SEC-05

# SEC-05: Password Policy Fully Relaxed -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ChangeIdentityPasswordPolicySettingDefinitionProvider.cs`

---

## Current state (verified 2026-04-17)

```csharp
public class ChangeIdentityPasswordPolicySettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // All four complexity flags explicitly defaulted to false
        context.GetOrNull(IdentitySettingNames.Password.RequireNonAlphanumeric).DefaultValue = false.ToString();
        context.GetOrNull(IdentitySettingNames.Password.RequireLowercase).DefaultValue = false.ToString();
        context.GetOrNull(IdentitySettingNames.Password.RequireUppercase).DefaultValue = false.ToString();
        context.GetOrNull(IdentitySettingNames.Password.RequireDigit).DefaultValue = false.ToString();
    }
}
```

Facts:

- `RequireNonAlphanumeric` / `RequireLowercase` / `RequireUppercase` / `RequireDigit` are all seeded to `false`.
- `RequiredLength` is NOT overridden -- ABP's default of 6 applies.
- Effect: `abc123` / `aaaaaa` accepted at signup and password reset.
- Hardcoded `CaseEvaluationConsts.AdminPasswordDefaultValue` exists and is used by DbMigrator + `PatientsAppService.GetOrCreatePatient...` -- combined with the relaxed policy, every auto-created patient shares a weak, guessable password (see Q12 in TECHNICAL-OPEN-QUESTIONS.md).

---

## Official documentation

- [ABP Identity Module](https://abp.io/docs/latest/modules/identity) -- `Abp.Identity.Password.*` settings; stored in settings store; passed to ASP.NET Core Identity via `AbpIdentityOptionsFactory`.
- [AbpIdentitySettingDefinitionProvider source](https://github.com/abpframework/abp/blob/dev/modules/identity/src/Volo.Abp.Identity.Domain/Volo/Abp/Identity/AbpIdentitySettingDefinitionProvider.cs) -- canonical defaults: `RequiredLength=6`, all four complexity flags `true`, `RequiredUniqueChars=1`.
- [IdentitySettingNames.Password (ABP 10.0 API)](https://abp.io/docs/api/10.0/Volo.Abp.Identity.Settings.IdentitySettingNames.Password.html) -- constant names.
- [Configure ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0) -- underlying `IdentityOptions.Password` Microsoft defaults (same as ABP's: 6 chars, all complexity on).
- [ABP Settings system](https://abp.io/docs/1.1/Settings) -- provider chain: `DefaultValueProvider` (seed) -> Global -> Tenant -> User. **A `SettingDefinitionProvider.DefaultValue` is the fallback only; admin-UI-stored values in the DB supersede.** (HIGH confidence.)
- [NIST SP 800-63B Rev 4 (published July 2025)](https://pages.nist.gov/800-63-4/sp800-63b.html) -- "Verifiers SHALL require subscriber-chosen memorized secrets to be at least 8 characters" and when password is sole authenticator, "SHALL require a minimum length of 15 characters." Verifiers "SHALL NOT impose other composition rules" and "SHALL compare the prospective secrets against" known-compromised lists.
- [NIST SP 800-63 FAQ](https://pages.nist.gov/800-63-FAQ/) -- official explanation of the shift away from composition rules.

## Community findings

- [ABP Support #3203 -- Change password rules](https://abp.io/support/questions/3203/Change-password-rules) -- shows both `Configure<IdentityOptions>` and `appsettings.json` (`Settings:Abp.Identity.Password.*`) patterns; notes admin UI overrides win.
- [ABP Support #6286 -- Question on password requirements](https://abp.io/support/questions/6286/Question-on-password-requirements) -- confirms settings flow; changes must be in tenant settings (DB), not only in provider code, for existing tenants.
- [abpframework/abp #2755](https://github.com/abpframework/abp/issues/2755) -- discussion of settings <-> `IdentityOptions` bridge; `AbpIdentityOptionsFactory` reads settings per request.
- [abpframework/abp #18113 -- Password rules not applied on ResetPasswordAsync](https://github.com/abpframework/abp/issues/18113) -- historical class of bugs where settings don't propagate to every reset pathway; verify 10.0.2 includes the fix.
- [Andrew Lock: Creating custom password validators](https://andrewlock.net/creating-custom-password-validators-for-asp-net-core-identity-2/) -- standard pattern for adding `IPasswordValidator<TUser>` that checks a breach list (e.g. Have I Been Pwned k-anonymity API).

## Why a developer might have turned these off (inferred)

Common community rationales in ABP threads:

1. UX friction during self-signup -- patients mistype, abandon registration.
2. Imported legacy credentials from an older system that don't satisfy modern complexity rules.

Neither is documented in this codebase. Confidence LOW. The hardcoded `AdminPasswordDefaultValue` + relaxed policy pattern suggests this was pre-demo seeding convenience.

## Recommended approach

1. **Align with NIST Rev 4**: raise `RequiredLength` to **12-15** (15 if password is sole authenticator; 12 if MFA enforced). Keep the four complexity flags OFF (NIST explicitly says do not impose composition rules). Add an `IPasswordValidator<IdentityUser>` that checks a breach list via HIBP k-anonymity (the first 5 chars of SHA-1, so actual password never leaves network).
2. **Do not rely solely on `SettingDefinitionProvider.DefaultValue`** for tenants that already saved values -- their DB rows win. Write a data seeder or explicit settings-migration that calls `ISettingManager.SetGlobalAsync` / `SetForTenantAsync` to update existing tenants.
3. **Keep `RequiredUniqueChars` at 4+** to block trivial `aaaaaaaaaaaa`.
4. **Remove or rotate** `CaseEvaluationConsts.AdminPasswordDefaultValue`. Auto-created patients should get a single-use signup email with a password-set link, not a shared default (see FEAT-05 email system).
5. **Update the Angular client-side validator** in the register component to mirror the server rules, or the UI will accept/reject differently.

## Gotchas / blockers

- `AbpIdentityOptionsFactory` reads settings on demand -- `SettingDefinitionProvider.DefaultValue` changes only affect tenants that have NOT saved a value. For existing tenants, run a settings-migration step.
- Angular Account module's client-side validator in `@volo/abp.ng.account` may duplicate ABP's rules -- update both or the UI will drift.
- `#18113` historical: `ResetPasswordAsync` bypassed configured rules; confirm 10.0.2 has the fix.
- HIBP check is an outbound call; in HIPAA context, use the k-anonymity range endpoint (first 5 hash chars) so no password material leaves your network.
- Tenants may be running with the old lax policy in production data -- a hardening push means existing users with weak passwords will fail on next password change.

## Open questions

- Original developer's documented reason for relaxing complexity. Check Gesco Decision-Log.
- Is any patient-facing path exempt from MFA? NIST Rev 4 wants 15 char minimum for MFA-less accounts.
- Does the business require the shared `AdminPasswordDefaultValue` pattern for any workflow (e.g. attorney-booking-on-behalf-of-patient flow where patient hasn't yet registered)?

## Related

- Q12 in [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md#q12-is-the-default-password-for-all-users-intentional-for-production)
- [FEAT-05 Email system](../INCOMPLETE-FEATURES.md#feat-05-email-system-is-not-wired-up) -- needed for first-login password-set email
- [docs/issues/SECURITY.md#sec-05](../SECURITY.md#sec-05-password-policy-fully-relaxed)
