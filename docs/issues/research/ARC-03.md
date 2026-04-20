[Home](../../INDEX.md) > [Issues](../) > Research > ARC-03

# ARC-03: Hardcoded Placeholder Values for Gender and DOB -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` lines 206-219
- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs` lines 135-142

---

## Current state (verified 2026-04-17)

`ExternalSignupAppService.RegisterAsync` for `UserType == Patient`:

```csharp
await _patientManager.CreateAsync(
    stateId: null,
    appointmentLanguageId: null,
    identityUserId: user.Id,
    tenantId: CurrentTenant.Id,
    firstName: input.FirstName,
    lastName: input.LastName,
    email: input.Email,
    genderId: Gender.Male,              // hardcoded
    dateOfBirth: DateTime.UtcNow.Date,  // hardcoded
    phoneNumberTypeId: PhoneNumberType.Home  // hardcoded
);
```

`DoctorTenantAppService.CreateDoctorProfileAsync`:

```csharp
var doctor = new Doctor(
    id: GuidGenerator.Create(),
    identityUserId: user.Id,
    firstName: input.Name,
    lastName: "",
    email: user.Email,
    gender: Gender.Male   // hardcoded
);
```

Facts:

- `ExternalUserSignUpDto` has NO Gender, DateOfBirth, PhoneNumberType fields -- patients cannot provide real values at signup.
- Every self-registered patient has `Gender = Male`, `DateOfBirth = <today>` at registration.
- Every auto-provisioned doctor has `Gender = Male`.
- E2E test B6.2 confirmed.
- IME medical-legal reports require accurate patient identification. Wrong DOB is a top driver of duplicate-record errors per AHIMA MPI research (cited below).

---

## Official documentation

- [HHS HIPAA Privacy Rule](https://www.hhs.gov/hipaa/for-professionals/privacy/laws-regulations/index.html) -- name, address, **date of birth** are explicitly "individually identifiable health information" subject to the Privacy Rule. Incorrect DOB isn't a Privacy Rule violation per se, but demographic fields are regulated.
- [NIST SP 800-66 Rev. 2](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-66r2.pdf) + [NIST overview](https://www.nist.gov/programs-projects/security-health-information-technology/hipaa-security-rule) -- HIPAA Security Rule; integrity standard (45 CFR 164.312(c)) requires protection against improper alteration of ePHI. Hardcoding default demographic values is arguably a failure of integrity-at-origin.
- [CA DWC QME Form 105](https://www.dir.ca.gov/dwc/forms/qmeforms/qmeform105.pdf) + [DWC FAQ](https://www.dir.ca.gov/dwc/medicalunit/faqiw.html) -- panel request form lists patient DOB as identifying field; QME report relies on the provided identification. No DWC regulation explicitly invalidates a report for DOB mismatch (so legal framing is operational-risk, not statutory). Confidence MEDIUM on the legal framing.
- [ABP Community -- Add custom property to user entity](https://community.abp.io/posts/how-to-add-custom-property-to-the-user-entity-6ggxiddr) -- canonical way to add `DateOfBirth`/`GenderId`/`PhoneNumberType` to `IdentityUser` and register DTOs without forking the Account module.
- [ABP Angular Account Module](https://abp.io/docs/latest/framework/ui/angular/account-module) + [ABP Support #6799](https://abp.io/support/questions/6799/Customize-account-login-and-register-page-in-angular-app) -- `@abp/ng.account` customisation via `ManageProfileTabsService.patchTab` / component replacement.
- [EF Core -- Managing Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) -- canonical guidance for schema changes with existing data.
- [Nielsen Norman Group -- Progressive Disclosure](https://www.nngroup.com/articles/progressive-disclosure/) + [NN/g Cognitive Load in Forms](https://www.nngroup.com/articles/4-principles-reduce-cognitive-load/) -- ask for minimum up front, defer advanced fields. Multi-step forms have ~14% higher completion vs single-step.

## Community findings

- [AHIMA MPI research (PMC4832129)](https://pmc.ncbi.nlm.nih.gov/articles/PMC4832129/) + [AHIMA best practices PDF](https://journal.ahima.org/Portals/0/archives/AHIMA%20files/Best%20Practices%20for%20Patient%20Matching%20at%20Patient%20Registration.pdf) -- DOB discrepant in **6.30%** of duplicate record pairs; gender in **6.32%**. Directly quantifies cost of bad demographics.
- [ABP Support #5696 -- Customize Registration of Account in ABP](https://abp.io/support/questions/5696/Customize-Registration-of-Account-in-ABP) -- step-by-step extending register DTO + UI.
- [ABP Discussion #10001 -- Add Custom Properties to Register Page](https://github.com/abpframework/abp/discussions/10001) -- module entity extensions flow for register-page custom fields.
- [dotnet/efcore #26807](https://github.com/dotnet/efcore/issues/26807) + [#32062](https://github.com/dotnet/efcore/issues/32062) -- edge cases when altering nullable/non-nullable columns; `AlterColumn` can emit invalid SQL with implied defaults. Mitigation: split into (a) add nullable column, (b) `migrationBuilder.Sql(...)` backfill, (c) `AlterColumn(nullable: false)` if ever needed.
- [Descope -- Progressive profiling](https://www.descope.com/learn/post/progressive-profiling) + [Auth0 -- Progressive profiling](https://auth0.com/blog/progressive-profiling/) -- industry describes healthcare apps deferring insurance-detail collection until first booking (25% signup-conversion lift in one cited case).

## Recommended approach

In increasing effort -- start with the short-term fix, then the correct long-term one:

1. **Short-term (do now)**: make `Patient.GenderId` and `Patient.DateOfBirth` nullable in the Domain. Add an EF migration that:
   - Alters the columns to `nullable: true`.
   - Runs a targeted `migrationBuilder.Sql("UPDATE Patients SET DateOfBirth = NULL WHERE ...")` to null out placeholder values (being very careful with the predicate).
   - Gate appointment booking in `AppointmentManager.CreateAsync` on "profile complete" -- throw `BusinessException` if `GenderId` or `DateOfBirth` is null for a patient-owned booking.
2. **Correct long-term**: add `DateOfBirth`, `Gender`, `PhoneNumberType` fields to `ExternalUserSignUpDto`; surface in Angular register form via ABP module entity extensions / DTO extensions. Required when `UserType == Patient`.
3. **Add a "Profile Complete" guard** on first-appointment-booking: Angular `CanActivate` + API-side check on `AppointmentManager`. Users land on a "complete your profile" page if any required demographic is null.
4. **`DoctorTenantAppService.CreateDoctorProfileAsync`** should not hardcode Gender either -- either collect from the doctor during provisioning, or make Gender nullable on the Doctor entity.

## Gotchas / blockers

- **Nullable-column migration on existing data**: straight `AlterColumn(nullable: true)` works, but you also want to **overwrite placeholder values with NULL** so they're flagged for re-collection. Do it as a two-step migration: (1) alter to nullable, (2) `UPDATE` with a careful predicate. EF Core issues [#26807](https://github.com/dotnet/efcore/issues/26807) / [#32062](https://github.com/dotnet/efcore/issues/32062) note edge cases.
- **Angular register form replacement** -- `@abp/ng.account` register component override via `ReplaceableComponentsService` is the supported path. Don't fork the module.
- **Validator parity** -- if you add `[Required]` server-side but the generated Angular proxy still treats the fields as optional, you get confusing 400s. Regenerate proxies via `abp generate-proxy` after the DTO change.
- **Workers' comp legal surface** (inference): even if no CA DWC rule explicitly invalidates a QME report for DOB mismatch, wrong demographics on medical-legal correspondence create cross-claimant identity risk and an "deny on records" angle for opposing counsel. Risk category: operational + reputational, not statutory.
- **`Patient` does NOT implement `IMultiTenant`** (per root CLAUDE.md) -- any cross-tenant view during migration could widen PHI exposure. Guard the migration script carefully.

## Open questions

- **Product**: for patients already registered with placeholder data, force re-entry on next login (good for data quality) vs quietly null-out and prompt only at booking (good for retention)? Inference: healthcare -> force re-entry.
- **Product**: is Gender a single value, a sex-assigned-at-birth + gender-identity pair, or free-text? Workers' comp forms use single "Gender/Sex" field. Inference: single value is fine but needs a product call.
- **Product**: should `DoctorTenantAppService.CreateDoctorProfileAsync` collect Gender from admin UI, default to "prefer not to say", or make it nullable on Doctor entirely?
- **Engineering**: required in DTO at API boundary (400) vs required in Domain (throw `BusinessException`) vs both? ABP convention is both -- DataAnnotations on DTO + domain-invariant check in `PatientManager`.
- **Engineering**: one migration (nullable + backfill together) or two separate migrations (nullable first, backfill later)? Two is safer for prod rollback.
- **Open legal**: is an IME report with wrong DOB ever rejected by the CA DWC Medical Unit? Worth a call to (510) 286-3700; no definitive public answer surfaced.

## Related

- [FEAT-05 Email system](../INCOMPLETE-FEATURES.md#feat-05-email-system-is-not-wired-up) -- needed for "complete your profile" email nudge
- [DAT-04](DAT-04.md) -- Doctor tenant creation also hardcodes Gender here
- [docs/issues/ARCHITECTURE.md#arc-03](../ARCHITECTURE.md#arc-03-hardcoded-placeholder-values-for-gender-and-date-of-birth)
