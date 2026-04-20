[Home](../../INDEX.md) > [Issues](../) > Research > ARC-07

# ARC-07: Hardcoded English Strings Bypass Localisation -- Research

**Severity**: Low
**Status**: Open (verified 2026-04-17)
**Source files**:
- `angular/src/app/appointments/appointment-add.component.ts` lines 299, 314, 682, 687, 690, 741, 744
- `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 177, 181, 185, 300, 305

---

## Current state (verified 2026-04-17)

Angular `appointment-add.component.ts`:
```typescript
this.patientLoadMessage = 'To create a new patient, First Name, Last Name, Email and Date of Birth are required.';
this.patientLoadMessage = 'Patient loaded. You can edit details below if needed.';
this.patientLoadMessage = 'No patient found with this email. Fill in the form below to create a new patient.';
```

Backend `DoctorAvailabilitiesAppService.cs`:
```csharp
throw new UserFriendlyException("From date cannot be in the past.");
throw new UserFriendlyException("From time must be before To time.");
```

All other `UserFriendlyException` calls in the same file correctly use `L["Key"]`. These 5 are the exception.

---

## Official documentation

- [ABP Angular Localization](https://abp.io/docs/latest/framework/ui/angular/localization) -- `LocalizationService.instant('Resource::Key')` or `get(...)` in TS; `{{ 'Resource::Key' | abpLocalization }}` in templates. Keys: `ResourceName::Key`; `::Key` uses configured default resource.
- [ABP Backend Localization](https://abp.io/docs/latest/framework/fundamentals/localization) -- inject `IStringLocalizer<TResource>`; `L["Key"]` shortcut on `ApplicationService`, `AbpController`, `AbpPageModel`. JSON: `{ "culture": "en", "texts": { "Key": "Value", "Nested__Key": "..." } }`, `__` for nesting.
- [ABP `UserFriendlyException`](https://abp.io/docs/latest/framework/fundamentals/exception-handling) -- intended for human-readable messages; a localization key routed through `IStringLocalizer` translates per current culture; raw string disables that path.
- [Angular native i18n (`@angular/localize`)](https://angular.dev/guide/i18n) -- compile-time extraction with `$localize`; requires rebuild per locale. Not runtime-switchable without SSR/ESM tricks.
- [ABP Language Management module](https://abp.io/docs/latest/modules/language-management) -- ABP v10 moved to JSON-based localization to enable runtime culture switching and admin-UI editing; reinforces why ABP teams avoid `@angular/localize` for tenant-facing strings.

## Community findings

- Reinforced by [BUG-12 (partially resolved)](BUG-12.md) investigation: `en.json` is the single source of truth for shared strings.
- ABP localization is functionally equivalent to ngx-translate/Transloco but ships with server/client parity and admin UI. MEDIUM confidence on equivalence.

## Recommended approach

1. Add missing keys to `src/.../Domain.Shared/Localization/CaseEvaluation/en.json` under the default resource:
   ```json
   "texts": {
     "FromDateCannotBeInPast": "From date cannot be in the past.",
     "FromTimeMustBeBeforeToTime": "From time must be before To time.",
     "PatientLoadRequired": "To create a new patient, First Name, Last Name, Email and Date of Birth are required.",
     "PatientLoaded": "Patient loaded. You can edit details below if needed.",
     "PatientNotFoundCreateNew": "No patient found with this email. Fill in the form below to create a new patient."
   }
   ```
2. Backend: replace each raw-string `UserFriendlyException(...)` with `UserFriendlyException(L["Key"])` -- matches existing convention in the same file.
3. Angular: replace raw assignments with `this.localization.instant('::Key')`. Keys come from backend over `/api/abp/application-localization`, so en.json on the backend is the single source of truth.

## Gotchas / blockers

- Angular-side keys populated at app start from backend `/abp/application-localization` endpoint. New key requires backend rebuild + browser refresh on client; otherwise key string shows instead of translation.
- Keys omitted from non-English resource files fall back to default culture value. Safe default but audit any Spanish file that may already exist.
- `UserFriendlyException` with literal string still returns structured HTTP error; only visible change is message text. Verify existing E2E tests don't assert on exact strings.
- Interpolation: `L["Key", arg1, arg2]` (backend) vs `localization.instant('Key', [arg1, arg2])` (Angular). Mixing placeholder syntaxes (`{0}` vs `{{0}}`) is a common bug source.

## Open questions

- Does the project use a default resource name (`MyProjectName`?) that should be updated as part of the [BUG-12](BUG-12.md) template-rename sweep?
- Is there a Spanish (or other) resource JSON in the repo, and do any English-only keys need translations before the fix ships to avoid exposing untranslated text to non-English-locale users?
- Should error messages be standardised to a prefix (e.g. `Error:FromDateCannotBeInPast`) for consistency?

## Related

- [BUG-07](BUG-07.md) -- ToasterService consumer needs localisation too
- [BUG-12](BUG-12.md) -- same `en.json` surface
- [docs/issues/ARCHITECTURE.md#arc-07](../ARCHITECTURE.md#arc-07-hardcoded-english-strings-in-user-visible-messages)
