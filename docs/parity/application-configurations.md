---
feature: application-configurations
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\ApplicationConfigurationsController.cs
old-docs:
  - data-dictionary-table.md (ConfigurationContents, GlobalSettings, LanguageContents, ModuleContents)
audited: 2026-05-01
status: audit-only
priority: 4
strict-parity: true
internal-user-role: system + IT Admin
depends-on: []
required-by:
  - external-user-registration   # form labels read from this
---

# Application configurations (i18n + global settings)

## Purpose

Holds language-keyed strings used by frontend (form labels, validation messages, button text) and global toggle settings (RecordLock, RequestLogging, SocialAuth, TwoFactorAuthentication, AutoTranslation, etc.).

**Strict parity with OLD.** Replace OLD's custom i18n + GlobalSettings with ABP localization + ABP `ISettingProvider`.

## OLD behavior (binding)

### Schema

`ConfigurationContents`: `ConfigurationContentId, ConfigurationContentName, En, Fr, ...` -- multi-language strings keyed by name.

`LanguageContents`: per-content language values (En, Fr) plus `ContentType` flag (server message vs client key).

`ModuleContents`: module-specific i18n (per `ApplicationModuleId`).

`GlobalSettings`: single-row config with toggles:
- `RecordLock` (bit) -- record locking feature
- `LockDuration` (varchar 10)
- `ApplicationTimeZoneId` (FK)
- `LanguageId` (FK -- default language)
- `RequestLogging` (bit)
- `SocialAuth` (bit)
- `TwoFactorAuthentication` (bit)
- `AutoTranslation` (bit)

### Endpoint

- `GET /api/ApplicationConfigurations/{languageName}` -- returns all i18n strings for a language; consumed by Angular at app-init.

### Critical OLD behaviors

- **Language-keyed string bag** loaded at app-init.
- **Global toggles read by various features** (e.g., `IsCustomField` is a separate setting; SocialAuth gates social-login flow; 2FA gates two-factor flow).
- **TwoFactorAuthentication and SocialAuth flags exist but are configurable-not-enabled** per Adrian's Q5 decision in registration audit -- keep flags but skip enablement in Phase 1.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Api/Controllers/Api/Core/ApplicationConfigurationsController.cs` | Single GET endpoint |
| `DbEntities.Models.{ConfigurationContent, LanguageContent, ModuleContent, GlobalSetting}` | EF entities |

## NEW current state

- ABP localization (`Volo.Abp.Localization`) replaces `LanguageContents` / `ModuleContents` / `ConfigurationContents`. Strings live in JSON files at `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/<lang>.json`.
- ABP settings (`ISettingProvider`) replace `GlobalSettings`. Per-tenant overridable.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Multi-language strings | OLD: DB-managed | NEW: JSON files (recompile to change) -- ABP also has `Volo.Abp.LocalizationManagement` for DB-managed | **Phase 1: use JSON files** for parity simplicity. **Phase 2: enable `LocalizationManagement` if IT-Admin-editable strings are required** (matches OLD's DB-managed behavior). | I |
| GlobalSettings entity | OLD | NEW: `ISettingProvider` | **Map each GlobalSettings field to an ABP setting key**: `CaseEvaluation.RecordLock`, `CaseEvaluation.LockDuration`, etc. | I |
| `IsCustomField` flag | OLD: in `SystemParameters` (covered) | -- | (covered) | -- |
| SocialAuth flag | OLD `GlobalSettings` | -- | **Add ABP setting `CaseEvaluation.SocialAuthEnabled`** -- default false; reserved for future enable | I |
| TwoFactorAuthentication flag | OLD `GlobalSettings` | ABP has `Account` settings | **Use ABP's `Account.IsTwoFactorEnabled`** | I |
| AutoTranslation flag | OLD | -- | **Add setting** -- default false (feature not implemented in Phase 1) | C |
| ApplicationTimeZoneId | OLD | NEW: ABP `Timing` module | **Use `IClock` + `ITimezoneProvider`** | -- |
| Default LanguageId | OLD | NEW: ABP `Localization:DefaultCulture` setting | None | -- |
| RecordLock | OLD: bit toggle for editing locks on records | -- | **Defer to Phase 2** -- not in critical workflow | -- |
| RequestLogging | OLD | NEW: ABP audit logging | None | -- |

## Internal dependencies surfaced

- ABP localization and settings -- standard infra.

## Branding/theming touchpoints

- All localized strings flow through this; localization keys are the brand-token surface for any user-facing text.

## Replication notes

### ABP wiring

- Localization JSON: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/{en,es,zh}.json`. Key per UI string. Used via `IStringLocalizer<CaseEvaluationResource>` in C# and `| abpLocalization` in Angular.
- Settings: `CaseEvaluationGlobalSettings` static class with constants like `SocialAuthEnabled`, `RecordLockEnabled`. Register via `ISettingDefinitionProvider`.
- All user-visible strings MUST come from localization (per branch CLAUDE.md ABP conventions). Hardcoded strings forbidden.

### Things NOT to port

- DB-managed i18n in Phase 1 (use JSON; can move to LocalizationManagement later).
- `ConfigurationContents` / `LanguageContents` / `ModuleContents` entities -- not needed.
- `GlobalSettings` entity -- replaced by `ISettingProvider`.

### Verification

1. Switch language -> labels update
2. SocialAuth setting toggled in config -> not yet wired (Phase 1) but no error
3. TwoFactor setting toggled -> ABP Account module respects it
4. New translations added to JSON -> picked up after rebuild
