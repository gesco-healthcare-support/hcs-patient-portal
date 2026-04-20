[Home](../../INDEX.md) > [Issues](../) > Research > ARC-04

# ARC-04: Role Name Strings Duplicated Across 8+ Files -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- Backend: `PatientsAppService.cs` (5 occurrences), `ExternalUserRoleDataSeedContributor.cs` (4), `ExternalSignupAppService.cs` (many), `DoctorTenantAppService.cs` ("Doctor"), various `L["Patient"]` in AppService error messages
- Angular: `app.component.ts`, `home.component.ts`, `appointment-add.component.ts` (6), `appointment-view.component.ts` (3), `patient-profile.component.ts` (2)

---

## Current state (verified 2026-04-17)

Role name strings `"Patient"`, `"Applicant Attorney"`, `"Defense Attorney"`, `"Doctor"`, `"Claim Examiner"` hardcoded as literals in at least 8 files across backend and frontend. ExternalSignupAppService has its own local `ToRoleName` helper, not accessible to `DoctorTenantAppService` or any Angular code.

ABP's own identity module does NOT export a public `IdentityRoleNames` constant -- only internal `IdentityRoleConsts` for DB column lengths. Application code must define its own.

---

## Official documentation

- [Volo.Abp.Identity module source (GitHub)](https://github.com/abpframework/abp/tree/dev/modules/identity/src/Volo.Abp.Identity.Domain.Shared) -- `IdentityRoleConsts` exists for `MaxNormalizedNameLength` etc., not role name values. ABP does NOT ship role-name constants.
- [ABP Identity Management Module](https://abp.io/docs/latest/modules/identity) -- role names are free-text per deployment; only built-in role is `admin` (seeded by `IdentityDataSeedContributor`).
- [Angular Style Guide](https://v19.angular.dev/style-guide) -- recommends per-feature constants files over one global file.
- [Angular TS Style Guide](https://v2.angular.io/docs/ts/latest/guide/style-guide.html) -- cross-feature constants belong in `core` or `shared`; export barrel pattern.

## Community findings

- [Medium -- Share Constants in TypeScript Project](https://medium.com/codex/how-to-share-constants-in-typescript-project-8f76a2e40352) -- `as const` pattern with literal-type narrowing; preferred over string enums (stays string-valued at runtime for JSON interop).
- [DEV.to -- Tips to Use Constants File in TypeScript](https://dev.to/amirfakour/tips-to-use-constants-file-in-typescript-27je) -- `const` vs `enum`: enums add runtime objects, `as const` does not.
- [Delft Stack -- Angular 2 Global Constants](https://www.delftstack.com/howto/angular/angular-2-global-constants/) -- `InjectionToken` + `providedIn: 'root'` pattern; overkill for role names.
- [CloudHadoop -- Static Constants in TypeScript/Angular](https://www.cloudhadoop.com/typescript-static-const) -- `static readonly` on a constants class works but discouraged by modern Angular style guide.

## Recommended approach

**Backend**: introduce `public static class CaseEvaluationRoles` in `.../Domain.Shared/` with:
```csharp
public const string Patient = "Patient";
public const string ApplicantAttorney = "Applicant Attorney";
public const string DefenseAttorney = "Defense Attorney";
public const string Doctor = "Doctor";
public const string ClaimExaminer = "Claim Examiner";
```
Domain.Shared is the right layer -- referenced by every project. Mirrors ABP's own internal pattern.

**Angular**: create `angular/src/app/shared/constants/role-names.ts`:
```typescript
export const RoleNames = {
  Patient: 'Patient',
  Doctor: 'Doctor',
  ApplicantAttorney: 'Applicant Attorney',
  DefenseAttorney: 'Defense Attorney',
  ClaimExaminer: 'Claim Examiner',
} as const;
export type RoleName = typeof RoleNames[keyof typeof RoleNames];
```

`as const` (not `enum`) so runtime values interop cleanly with JSON from the backend.

**Keep localisation keys untouched** -- `L["Patient"]` is a localization key (English label), not a role identifier. They happen to share a string. Orthogonal concerns.

## Gotchas / blockers

- ABP's role names are stored free-text in `AbpRoles.Name`. Changing a value (e.g. `"Patient"` -> `"patient"`) requires an `IDataSeedContributor` update or manual DB migration. Refactor to constants must preserve exact casing.
- `L["Patient"]` (localization) uses the same string by coincidence. Refactoring the role constant does NOT change the localization key. Grep both independently.
- ABP Suite-generated code reintroduces string literals. If Suite is used again, add post-generation replacement step. INFERENCE: no official support.
- `app.component.ts` likely compares roles via `PermissionService` + `CurrentUserDto.roles` array -- verify comparison is case-insensitive or refactor preserves casing exactly.

## Open questions

- Is there a pre-existing `ExternalUserRoles` enum or constant somewhere in Domain that should be the single source of truth?
- Does `DoctorTenantAppService`'s hardcoded `"Doctor"` correspond to a role, a user type, or both? Semantic ambiguity needs resolving.

## Related

- [FEAT-02](FEAT-02.md) -- ClaimExaminer role handling; fix together
- [src/.../Application/ExternalSignups/CLAUDE.md](../../../src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/CLAUDE.md) Gotcha table has `ToRoleName` reference
- [docs/issues/ARCHITECTURE.md#arc-04](../ARCHITECTURE.md#arc-04-role-name-strings-duplicated-across-8-files-with-no-shared-constant)
