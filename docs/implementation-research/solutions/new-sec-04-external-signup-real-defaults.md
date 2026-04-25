# NEW-SEC-04: Remove hardcoded Gender/DOB/PhoneType in ExternalSignup.RegisterAsync

## Status (scope-locked 2026-04-24 -- SCOPE REWRITTEN)

Adrian's Q&A answer Q12 changes this capability's target action:
- **OLD scope (superseded):** fix hardcoded `Gender.Male` / today-DOB / `PhoneNumberType.Home` in `ExternalSignupAppService.RegisterAsync`.
- **NEW scope (locked):** REMOVE or gut the anonymous `ExternalSignupAppService.RegisterAsync` endpoint entirely. All external-user creation is now email-invite only (driven by admin through `/identity/users` + the `FindOrCreateExternalUserAsync` method in the revised `appointment-accessor-auto-provisioning` capability).
- Implementation:
  1. Remove `[AllowAnonymous]` + `[RemoteService(IsEnabled = true)]` from `ExternalSignupAppService.RegisterAsync` (or delete the service entirely).
  2. Remove Angular routes / components that call `/api/public/external-signup/register`.
  3. Remove `/api/public/external-signup/register` controller route.
  4. Ensure there is no other anonymous user-creation surface left.
- Effort unchanged (~1 day), but the action changes from "patch fields" to "remove endpoint".
- Tests encoded in `new-qual-01-critical-path-test-coverage` should assert the endpoint returns 404 / is absent from the swagger surface post-fix.

## Source gap IDs

- NEW-SEC-04 -- `../gap-analysis/10-deep-dive-findings.md:80-90` (MVP-blocking,
  effort S ~1 day per `../gap-analysis/10-deep-dive-findings.md:256`).
- Cross-reference: `patient-auto-match` capability
  (`../gap-analysis/02-domain-entities-services.md:188`, G2-04). The auto-match
  recommendation introduces a `PatientManager.FindOrCreateAsync` that becomes the
  single Patient-create pathway; that work subsumes this one when both land in
  the same wave. See `./patient-auto-match.md:160-162` for the cross-link.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:211-224`
  -- `RegisterAsync` creates a `Patient` row via `_patientManager.CreateAsync(...)`
  with three hardcoded values that the signup form does not collect:
  `genderId: Gender.Male`, `dateOfBirth: DateTime.UtcNow.Date`, and
  `phoneNumberTypeId: PhoneNumberType.Home`. `stateId` and
  `appointmentLanguageId` are passed `null` (acceptable -- those FKs are already
  nullable on `Patient`).
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/ExternalUserSignUpDto.cs:6-29`
  -- the DTO the anonymous endpoint binds. Fields: `UserType`, `FirstName`,
  `LastName`, `Email`, `Password`, `TenantId`. No demographic, DOB, or phone
  fields. Matches the signup UX: public anonymous, minimal friction.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs:32-34,57`
  -- today the columns are declared as value types (`public virtual Gender GenderId`,
  `public virtual DateTime DateOfBirth`, `public virtual PhoneNumberType PhoneNumberTypeId`).
  Value types cannot hold null, so defaults always exist if EF-materialised.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs:83-126`
  -- the ctor takes all three as required (non-nullable) parameters at positional
  slots 9, 10, 11. Domain ctor has no `Check.NotNull` on them (they are value
  types), but the call sites always supply them.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientManager.cs:23-48`
  -- `CreateAsync` takes all three as required non-nullable parameters.
  `Check.NotNull(genderId, ...)` at line 32, `Check.NotNull(dateOfBirth, ...)`
  at line 33, `Check.NotNull(phoneNumberTypeId, ...)` at line 34 run but are
  no-ops against value types.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260210185726_Added_Patient.cs:23,24,32`
  -- the table columns are `GenderId int NOT NULL`, `DateOfBirth datetime2 NOT NULL`,
  `PhoneNumberTypeId int NOT NULL`. A migration is required to relax any of them to
  nullable.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientCreateDto.cs:23-24,47`
  and `CreatePatientForAppointmentBookingInput.cs:25,27,50` -- both DTOs also
  default `GenderId` and `PhoneNumberTypeId` to `Enum.GetValues<T>()[0]` when
  unset. Those are intake/booking DTOs and do collect `DateOfBirth` as a required
  field. They are unaffected by this fix except that their call sites pass real
  user-entered values.
- `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:92-187`
  -- `GetOrCreatePatientForAppointmentBookingAsync` (the booking path) DOES
  collect Gender/DOB/PhoneType from the form via `input.GenderId`,
  `input.DateOfBirth`, `input.PhoneNumberTypeId`. So the booking path already
  behaves correctly; only the anonymous public-signup path is broken.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs:42-48`
  -- the public route is `POST /api/public/external-signup/register` with
  `[AllowAnonymous]` and `[IgnoreAntiforgeryToken]`. Binding happens via
  `[FromBody] ExternalUserSignUpDto`. Contract changes here propagate to the
  anonymous public endpoint.
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/CLAUDE.md:80-98`
  (feature-scoped doc read via root `CLAUDE.md` context) -- explicitly flags
  "hardcoded Patient defaults" as gotcha #4, confirms "Patient profile needs
  updating after registration". Also flags HIPAA concern #5 (anonymous PII
  collection without rate limiting / CAPTCHA) which is out of scope for this
  brief but captured for Adrian's awareness.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md:71-76`
  -- Patient is NOT `IMultiTenant`; the tenant FK is a manual nullable Guid.
  Implication for this brief: the new null Gender/DOB/PhoneType columns will not
  be auto-filtered by tenant, but that is a property of the row as a whole, not
  of these three fields, and is out of scope for NEW-SEC-04.
- `angular/src/app/patients/patient/components/patient-profile.component.ts:70-96`
  -- today the `/patients/my-profile` form declares `genderId`, `dateOfBirth`,
  and `phoneNumberTypeId` with `[Validators.required]`. The form is already the
  natural forcing gate for profile completion after first login; we need only to
  add a "profile incomplete" check and route-guard.

## Live probes

**None executed for this brief.** Per the task brief: `RegisterAsync` is a
state-mutating endpoint that creates a persistent `IdentityUser` row, optionally
a persistent `IdentityRole` row, and (for `UserType = Patient`) a persistent
`Patient` row. The Live Verification Protocol in `../README.md:262-272`
forbids probes that "probe SaaS tenant creation, IdentityUser creation,
OpenIddict client creation, ApplicantAttorney creation, or Patient creation"
because they leave persistent state a manual cleanup might miss.

Static code proof from `ExternalSignupAppService.cs:211-224` is conclusive:
the literal `Gender.Male`, `DateTime.UtcNow.Date`, and `PhoneNumberType.Home`
are present in the call; there is no conditional branch or input-driven path.
The three hardcoded values are deterministic outputs of every invocation.

Full static-evidence log at
`../probes/new-sec-04-external-signup-real-defaults-2026-04-24T23-15-00.md`.

## OLD-version reference

Not applicable. This is a NEW-side defect. The Patient-create pathway in OLD
did not expose a public-anonymous signup endpoint; OLD's self-registration used
a separate Identity flow that collected demographics on a multi-step wizard.
The defect is specific to NEW's decision to add an anonymous signup endpoint
and wire it directly to `PatientManager.CreateAsync` without collecting the
required fields.

Track-10 errata applicability: none. Track 10 errata concern PDF renderer,
SMS, scheduler, and CustomField. None touches the external-signup path.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. The endpoint
  `POST /api/public/external-signup/register` must remain `[AllowAnonymous]`
  for the SPA registration UX; no auth change.
- Row-level `IMultiTenant` (ADR-004), doctor-per-tenant. Patient is NOT
  `IMultiTenant` by design. The nullable-column fix does not alter tenant
  semantics.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003),
  no ng serve (ADR-005). None of these is touched by this fix. Mapperly
  `[Mapper]` definitions for Patient read-DTOs do not require regeneration
  because target DTOs already declare `GenderId` and `PhoneNumberTypeId` as
  value-type-valued (see `PatientDto.cs`); when the entity goes nullable the
  compiler will force the `PatientDto` field to become nullable too -- a
  compile-time surface the reviewer must inspect, not a runtime change.
- HIPAA applicability: DOB is PHI. Logging DOB or Gender values on the signup
  path is already not done; this fix removes a data-quality problem, not a
  logging problem. No change to logging policy.
- Capability-specific: the three columns currently have NOT-NULL on existing
  rows (zero seeded patients per `probes/service-status.md`). Making them
  nullable is a safe schema change against an empty table. A future production
  deployment with existing rows would require a data-backfill step, but the
  MVP corpus is empty so the migration is a pure schema change.
- Any solution must not re-introduce the hardcoded defaults elsewhere in the
  codebase; the three call sites that currently pass real values
  (`PatientsAppService.CreateAsync`, `GetOrCreatePatientForAppointmentBookingAsync`,
  `UpdatePatientForAppointmentBookingAsync`, `UpdateMyProfileAsync`) must
  continue to pass the form-supplied values unchanged.
- `patient-auto-match` (G2-04) introduces a `PatientManager.FindOrCreateAsync`
  that is the future single entry point. The chosen solution must be
  forward-compatible with that sibling capability.

## Research sources consulted

- ABP Commercial docs, Value Objects and nullable enums (HIGH):
  https://abp.io/docs/10.0/framework/architecture/domain-driven-design/value-objects
  -- accessed 2026-04-24. Confirms enum-typed columns may be declared
  nullable (`Gender?`) with no special repository treatment.
- Microsoft Learn, EF Core 10 nullable value types and provider-level storage
  (HIGH): https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties
  and https://learn.microsoft.com/en-us/ef/core/modeling/relationships/foreign-and-principal-keys#optional-dependents
  -- accessed 2026-04-24. Confirms changing `int NOT NULL` -> `int NULL` is a
  single `AlterColumn<int?>` migration with no data-loss risk when the table
  is empty.
- Microsoft Learn, EF Core migrations -- `AlterColumn` semantics (HIGH):
  https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
  -- accessed 2026-04-24. Confirms relaxing NOT-NULL to NULL is a non-blocking
  schema change supported by SQL Server.
- ABP docs, Multi-tenancy (HIGH): https://abp.io/docs/10.0/framework/architecture/multi-tenancy
  -- accessed 2026-04-24. Confirms Patient's non-`IMultiTenant` status is
  unchanged; the three columns are tenant-agnostic.
- ABP docs, `[AllowAnonymous]` on anonymous signup endpoints (HIGH):
  https://abp.io/docs/10.0/framework/fundamentals/authorization
  -- accessed 2026-04-24. Confirms that leaving the endpoint `[AllowAnonymous]`
  is the correct ABP pattern for public signup.
- Microsoft Learn, data-annotations `[Required]` on reference types vs nullable
  value types (MEDIUM): https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations.requiredattribute
  -- accessed 2026-04-24. Confirms that a `DateTime? DateOfBirth` property with
  `[Required]` will correctly reject a missing JSON field on the booking DTO
  without breaking existing callers that always send one.
- ABP community article, forcing profile completion with ABP Account module
  (MEDIUM): https://abp.io/community/articles/forcing-a-user-to-change-password-or-complete-profile-after-first-login-ba0s5-8m
  -- accessed 2026-04-24. Confirms pattern: Angular `canActivate` guard on
  feature routes that redirects to `/patients/my-profile` when the Patient
  record has any required field null.

## Alternatives considered

- **A. Make `Gender`, `DateOfBirth`, `PhoneNumberType` nullable on the
  `Patient` entity + drop the three hardcoded lines in `RegisterAsync` + force
  profile completion via Angular route guard before allowing booking**
  -- **chosen**. Task brief Alternative A. Rationale: zero change to the
  anonymous signup UX (no extra fields), clean data quality (no placeholder
  rows), forward-compatible with `patient-auto-match` (FindOrCreateAsync can
  emit nulls for unknown values), smallest blast radius in MVP scope. Full
  shape under "Recommended solution".
- **B. Collect `Gender`, `DateOfBirth`, `PhoneNumberType` on the signup form**
  -- **rejected**. Task brief Alternative B. Adds 3 required fields to an
  anonymous signup flow that currently asks only name + email + password.
  Increases drop-off: DOB is a regulated PHI field the user may not want to
  share on an anonymous pre-login form. Verification of DOB cannot happen
  pre-login. Also forces a schema change on `ExternalUserSignUpDto` and the
  Angular signup form that only benefits one downstream use case.
- **C. Reject the anonymous signup path + require admin to create external
  users** -- **rejected**. Task brief Alternative C. Breaks the self-service
  model the product is built around. Adds an operational-bottleneck step to
  onboarding. Inconsistent with the broader SaaS positioning.
- **D. Admin-approval gate for new external signups** -- **conditional,
  rejected for MVP**. Task brief Alternative D. Adds a review queue, admin
  notification, pending-state UX. That is a larger feature that interacts
  with `users-admin-management` and `scheduler-notifications`. Not required
  to resolve NEW-SEC-04 alone. Revisit post-MVP if HIPAA audit demands it.
- **E. Make `PatientManager.FindOrCreateAsync` (from `patient-auto-match`) the
  single entry and drop the inline `_patientManager.CreateAsync` call in
  `RegisterAsync` entirely, leaving Patient creation to the first booking
  form submission** -- **conditional, deferred**. Cleanest long-term shape,
  but blocks on `patient-auto-match` landing first. Recommended as the
  post-MVP evolution once both capabilities are shipped. For now, Alternative
  A ships the nullable-column fix + profile-completion gate in isolation and
  allows `patient-auto-match` to land later without re-doing the fix.

## Recommended solution for this MVP

Make the three entity properties nullable, propagate the nullability through
`PatientManager.CreateAsync`/`UpdateAsync` and the Patient ctor, drop the three
hardcoded literals from `ExternalSignupAppService.RegisterAsync`, and add an
Angular route guard that forces the user to complete profile before booking.

- **Entity** -- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs`:
  - `public virtual Gender? GenderId { get; set; }` (line 32)
  - `public virtual DateTime? DateOfBirth { get; set; }` (line 34)
  - `public virtual PhoneNumberType? PhoneNumberTypeId { get; set; }` (line 57)
  - Update the ctor (line 83-126) to take `Gender? genderId`, `DateTime? dateOfBirth`,
    `PhoneNumberType? phoneNumberTypeId`. Keep the parameter order; only type
    changes. Remove the corresponding `Check.NotNull` calls in the ctor for
    these three (they were always no-ops against value types; now the column
    is genuinely optional).
- **Domain service** --
  `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientManager.cs`:
  - `CreateAsync` (line 23) and `UpdateAsync` (line 51) parameter types change
    from `Gender genderId`, `DateTime dateOfBirth`, `PhoneNumberType phoneNumberTypeId`
    to the nullable equivalents.
  - Drop `Check.NotNull` on these three (lines 32-34 of the current method) --
    they become legitimately nullable.
  - `UpdateAsync` assignment lines 83-85 continue to assign whatever the caller
    passes; semantics are preserved.
- **DTOs** --
  - `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/PatientCreateDto.cs`
    lines 23, 24, 47: change `Gender GenderId`, `DateTime DateOfBirth`,
    `PhoneNumberType PhoneNumberTypeId` to their nullable equivalents. Drop
    the `= Enum.GetValues<T>()[0]` default initializers (they become
    meaningful-null instead of arbitrary-first-enum).
  - Same three fields in `PatientUpdateDto.cs` (via Mapperly-partner).
  - Same three fields in `CreatePatientForAppointmentBookingInput.cs`
    (lines 25, 27, 50). The booking form already collects these as required;
    the DTO type becoming `T?` does not change the form's client-side
    `[Validators.required]` behavior.
  - `PatientDto.cs` (Mapperly target) -- nullable propagates automatically
    because Riok.Mapperly maps entity `T?` -> DTO `T?` one-to-one.
- **AppService (anonymous signup path)** --
  `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:211-224`:
  - Replace the three hardcoded lines (`Gender.Male`, `DateTime.UtcNow.Date`,
    `PhoneNumberType.Home`) with `genderId: null`, `dateOfBirth: null`,
    `phoneNumberTypeId: null`.
  - No DTO change on `ExternalUserSignUpDto` -- the anonymous signup remains
    minimal.
- **AppService (booking + admin paths)** --
  `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs`
  lines 148-170 (`GetOrCreatePatientForAppointmentBookingAsync`) and 304-313
  (`CreateAsync`). No behavior change: both pass `input.GenderId`,
  `input.DateOfBirth`, `input.PhoneNumberTypeId` through. Source DTO fields
  are nullable now but callers (the booking form and the admin UI) continue
  to supply real values.
- **Controller / HTTP** -- no change. The anonymous endpoint contract
  (`ExternalUserSignUpDto`) is unchanged.
- **Angular** --
  - Regenerate `angular/src/app/proxy/` via `abp generate-proxy`. The three
    fields become `Gender | null`, `Date | string | null`,
    `PhoneNumberType | null` in TS. Per ADR (never hand-edit proxy), this is
    automatic.
  - Add a route guard that redirects to `/patients/my-profile` when the
    logged-in user's Patient record has any of those three fields null, and
    the requested route requires a complete profile (booking flow, appointment
    create). The guard is a new file under
    `angular/src/app/patients/patient/providers/` following the existing
    `authGuard` / `permissionGuard` composition pattern. Register it on the
    booking routes in `angular/src/app/appointments/providers/appointment.routes.ts`.
  - `patient-profile.component.ts:70-96` already declares all three fields
    with `[Validators.required]`; no form change. Add client-side UX to
    highlight "profile incomplete -- please complete before booking" when
    the guard redirects.
- **EF migration** -- one migration:
  `dotnet ef migrations add Patient_RelaxRequiredDemographicsToNullable`
  against `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore`. The
  migration is three `AlterColumn<int?>` / `AlterColumn<DateTime?>` calls on
  the `Patients.GenderId`, `Patients.DateOfBirth`, `Patients.PhoneNumberTypeId`
  columns. Zero data loss because the table is empty per
  `probes/service-status.md`.

Total call sites reviewed for nullable propagation:
`ExternalSignupAppService.cs:211-224` (1),
`PatientsAppService.cs` at lines 148-170, 203-228, 304-313, 317-324, 334-361
(5), plus entity, manager, DTO changes. All continue to compile with nullable
types because the real callers either supply values (booking, admin) or
explicitly pass `null` (external signup, and any future null-allowed path).

## Why this solution beats the alternatives

- Preserves the anonymous-signup UX: no extra fields, no drop-off, no pre-login
  PHI prompt. Alternative B would regress onboarding conversion to improve data
  quality of a row the user will revisit on first login anyway.
- Single migration, empty table, zero data-loss risk. Alternative D adds a
  new entity (pending-approvals), a new admin UI, and scheduler wiring -- out of
  scope for the NEW-SEC-04 fix.
- Forward-compatible with `patient-auto-match` (Alternative E): once
  `FindOrCreateAsync` lands, the three null values that this fix permits become
  natural inputs to the 3-of-6 match (missing fields simply do not contribute
  to the match count). No rework needed.
- Treats the profile-completion step as a client-side routing concern rather
  than a server-side enforcement, keeping server APIs simple and consistent
  with the ABP Account module's first-login-change-password pattern cited
  above.
- Removes all three legally-problematic hardcoded values (DOB = today for every
  patient, fixed gender, fixed phone type) in one change.

## Effort (sanity-check vs inventory estimate)

Inventory says **S (1 day)** per `../gap-analysis/10-deep-dive-findings.md:256`.
Analysis confirms **S (1 day)**, possibly creeping to S+ (1.5 days) with tests.

Breakdown:
- 0.25 day: entity + `PatientManager` + DTO nullable propagation. Compile-time
  driven; compiler errors enumerate every call site needing a look.
- 0.25 day: EF migration file + local verification
  (`dotnet ef migrations add` only -- no `database update` per the research
  protocol; migration is committed and run by the `feature-build` phase, not
  here).
- 0.25 day: drop three hardcoded lines in `RegisterAsync`; adjust any proxy
  regeneration.
- 0.25 day: Angular `profileCompleteGuard` + integration on the booking routes
  + minor UI hint on profile page.
- 0.25 day buffer: tests for the three changed paths (see Risk / Rollback).

Total: ~1.25 developer-days. Inventory's S (1 day) holds.

## Dependencies

- **Blocks:**
  - `patient-auto-match` -- NOT a hard block. Both capabilities can land
    independently. If `patient-auto-match` lands first and introduces
    `FindOrCreateAsync`, this brief's work to drop the three hardcoded lines
    in `RegisterAsync` reduces to a 1-line swap
    (`_patientManager.CreateAsync(...)` -> `_patientManager.FindOrCreateAsync(...)`)
    plus the Angular guard. If this brief lands first, `patient-auto-match`
    can later integrate by routing both call sites through
    `FindOrCreateAsync`. Either order works; coordination is a style
    preference.
  - Angular `/patients/my-profile` route contract -- the `profileCompleteGuard`
    redirects to this route. The route exists today
    (`angular/src/app/patients/patient/components/patient-profile.component.ts`)
    and requires no modification beyond the minor "profile incomplete" UX.

- **Blocked by:** none. The three column types are already in use at every
  required call site with either real values (booking, admin) or literal
  hardcoded values (external signup, the defect itself). Everything the fix
  needs exists today.

- **Blocked by open question:** none.

## Risk and rollback

- **Blast radius:** scoped to the Patient entity shape, the three Patient
  DTOs, `PatientManager` signatures, the anonymous signup path's call to
  `PatientManager.CreateAsync`, and the Angular booking routes. No cross-entity
  effects. Existing patient data: none (empty table). Existing seeded rows:
  none. Admin CRUD behavior: unchanged except nullable DTO propagation.
- **Rollback:**
  - EF migration revert: `dotnet ef database update <previous-migration>` +
    revert the source change. Because the table is empty at MVP time, no data
    reconciliation is needed.
  - Code revert: single commit revert restores today's NOT-NULL shape and
    restores the three hardcoded values (the exact defect -- but at least the
    system compiles).
  - Angular revert: remove the `profileCompleteGuard` import and route
    wiring; the `/patients/my-profile` page continues to work standalone.
- **Test plan:**
  - Application test: `CaseEvaluationApplicationTestBase` +
    `ExternalSignupAppService.RegisterAsync` with `UserType = Patient`.
    Assert that the created `Patient` row has `GenderId == null`,
    `DateOfBirth == null`, `PhoneNumberTypeId == null`, and that
    `FirstName / LastName / Email / IdentityUserId` are populated.
  - Application test: admin `PatientsAppService.CreateAsync` with a full DTO
    still populates all three fields (regression check).
  - Application test: booking `GetOrCreatePatientForAppointmentBookingAsync`
    still populates all three fields when form provides them (regression).
  - EF migration test: `PatientMigrationTests` loads the migration in
    SQLite in-memory and confirms column nullability metadata matches
    expectations.
  - Angular unit test: `profileCompleteGuard` redirects to `/patients/my-profile`
    when any of the three fields is null; allows through otherwise.
  - Manual smoke: sign up as Patient via the anonymous endpoint; log in;
    attempt to navigate to booking; confirm redirect to profile; fill the
    three fields; confirm booking route opens.

## Open sub-questions surfaced by research

- Should the `profileCompleteGuard` enforce completeness only on the booking
  path, or on all authenticated external-user routes? **Recommendation: booking
  path only for MVP.** Over-guarding risks a loop on first-login navigation.
  Adrian can broaden post-MVP after observing real-user behavior.
- Should the profile-complete check live server-side too (e.g.,
  `BookingAppService.CreateAsync` rejecting calls when the calling
  `CurrentUser` has incomplete Patient fields)? **Recommendation: yes,
  belt-and-suspenders.** A simple `UserFriendlyException` at the booking
  AppService entry is one line and prevents API-tool circumvention of the
  Angular guard. Adds ~5 minutes to the 1-day estimate.
- After `patient-auto-match` lands, should `ExternalSignupAppService.RegisterAsync`
  delegate to `PatientManager.FindOrCreateAsync` with null demographics, or
  stop creating a Patient at signup time entirely and let the booking flow be
  the first create? **Recommendation: delegate to `FindOrCreateAsync` with
  nulls.** The current anonymous-signup path's semantic is "prepare a Patient
  row for this IdentityUser", which stays valid; `FindOrCreateAsync` will
  simply return the new row on the first call and the same row on any
  subsequent booking-flow submission. Cleaner than a first-booking-creates
  pattern because a Patient row pre-exists the first booking and can serve as
  the IdentityUser<->Patient link target.
- Should the hardcoded values in `DoctorTenantAppService.cs:137-138`
  (`Gender.Male` + empty LastName for the seeded tenant doctor) be addressed
  in this same fix? **Recommendation: no.** That defect is NEW-SEC-03
  (transactional tenant provisioning), a sibling brief. Scope discipline.
