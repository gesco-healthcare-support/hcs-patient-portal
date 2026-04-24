# Appointment lead-time and max-time limits

## Source gap IDs

- [G2-03](../../gap-analysis/02-domain-entities-services.md) -- track 02 Delta row: `AppointmentLeadTime / MaxTimePQME / MaxTimeAME / MaxTimeOTHER`. OLD ref `AppointmentDomain.cs:119-157` + `SystemParameter.cs`. NEW state: no SystemParameter entity; AppointmentManager has zero time-window validation. Inventory effort: M (3 days).

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs:14-59`: full class is ~60 lines. `CreateAsync` and `UpdateAsync` do basic null/length checks via `Check.*` helpers, then write through the repo. No time-window validation, no `SettingProvider` dependency, no `DateTime.Now`-vs-slot comparisons.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:162-205`: `CreateAsync` validates FKs exist and calls `ValidateDoctorAvailabilityForBooking` (lines 235-262). That method checks slot availability, location match, type match, date match, and time-within-range -- but NOT lead-time or max-horizon. No `ISettingProvider` injection on the class.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:295-324`: `UpdateAsync` skips the availability validator entirely, going directly to `AppointmentManager.UpdateAsync`. Same lack of time-window checks.
- `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs:5-10`: project-level provider exists but defines zero settings. ABP `SettingDefinitionProvider` base class is wired -- the provider is discovered and called, it just has an empty `Define` body.
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ChangeIdentityPasswordPolicySettingDefinitionProvider.cs:6-33`: proof that `SettingDefinitionProvider` is active in this module -- this provider successfully mutates built-in ABP Identity password settings at runtime, confirming the setting subsystem runs.
- Grep `LeadTime|MaxTime|SystemParameter` across `src/` returns zero business-code matches; only appears in EF migration snapshot designer files for unrelated entities. Confirms no prior implementation attempt, no stale entity.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/AppointmentConsts.cs`: only contains string-length constants (PanelNumber=50, etc.); no time-window constants to promote.

## Live probes

- Probe 1 -- Swagger filter for lead-time / max-time / setting endpoints: ran `GET https://localhost:44327/swagger/v1/swagger.json`, filtered paths. Zero custom `lead`, `maxtime`, `parameter`, or `booking-policy` paths. 16 ABP `/api/*-management/settings*` and `/api/identity/settings*` endpoints are present (host-level ABP SettingManagement UI), which is the delivery surface for this capability. Total path count: 317. Log: `../probes/appointment-lead-time-limits-2026-04-24T20-03-33Z.md`.
- No POST/PUT/DELETE probes run (capability has no mutating NEW endpoint to exercise; verification belongs post-implementation).

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs:102-157`: `AddValidation(Appointment)` loads `vSystemParameter` (the view mirror of `spm.SystemParameters`), pulls `AppointmentLeadTime`, and enforces `DateTime.Now.AddDays(leadTime) < availableDate` for external users (internal users skip the gate via `ClaimTypes.GroupSid != InternalUser`). Then per-type max horizons: `PQME | PQMEREEVAL -> AppointmentMaxTimePQME`, `AME | AMEREEVAL -> AppointmentMaxTimeAME`, `OTHER -> AppointmentMaxTimeOTHER`. Validation strings are raw English, not i18n.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/SystemParameter.cs:12-101`: OLD entity in `spm.SystemParameters` table with 14 fields: `AppointmentCancelTime`, `AppointmentDueDays`, `AppointmentDurationTime`, `AppointmentLeadTime`, `AppointmentMaxTimeAME`, `AppointmentMaxTimePQME`, `AppointmentMaxTimeOTHER`, `AutoCancelCutoffTime`, `IsCustomField`, `JointDeclarationUploadCutoffDays`, `PendingAppointmentOverDueNotificationDays`, `ReminderCutoffTime`, `CcEmailIds`, plus audit. Every Int field is `[Range(1, int.MaxValue)]`. This is a single-row table (admin-edited).
- Applies to MVP: the four fields in scope for G2-03 are `AppointmentLeadTime` (days of notice required before an external user can book) and the three max-horizons (how far ahead a booking may extend by type). The other 10 fields belong to other capabilities (change-request cutoff, notifications, joint-declaration window) and are out of scope for this brief.
- Track-10 errata (`10-deep-dive-findings.md`) do not modify G2-03 directly. The scheduler hardcoded-`1` erratum is unrelated; the OLD SMS is 100% disabled erratum is unrelated; the server-side-PDF erratum is unrelated.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict.
- Row-level `IMultiTenant` (ADR-004): Appointment is tenant-scoped. A per-tenant (per-doctor) lead-time/max-horizon override is a realistic business need because different clinics may have different notice policies.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003), no ng serve (ADR-005) -- all satisfied by the ABP SettingManagement path (no new entity, no new controller).
- HIPAA: scheduling policy values are administrative config, not PHI. No redaction rules apply. Logging setting reads does not create PHI exposure.
- Dependency on Q8 (SystemParameter vs ABP SettingManagement): per `docs/gap-analysis/README.md:238`, the question is open. This brief assumes the direction from inventory ("DB-17 -- maybe intentional (ABP Settings)") and from the dedicated `system-parameters-vs-abp-settings` capability -- i.e. ABP SettingManagement is the target. If Q8 resolves in favor of porting a custom SystemParameter entity, this brief's Alternative C becomes the chosen path.
- Internal-vs-external user gate from OLD: the lead-time only applied to external users in OLD. Replicating this in NEW requires a user-type discriminator. NEW has no single "user type" enum equivalent to OLD's `UserType.InternalUser` claim; the equivalent is either (a) ABP role membership test ("user is in any of ItAdmin/StaffSupervisor/ClinicStaff/Adjuster roles") or (b) an explicit permission such as `CaseEvaluation.Appointments.BypassLeadTime`. Recommendation: permission-based gate because it is more granular and composes with other permissions.

## Research sources consulted

- ABP Commercial docs -- Setting Definition Provider: `https://abp.io/docs/latest/framework/infrastructure/settings` (verified 2026-04-24; Settings overview, `SettingDefinition`, `SettingDefinitionProvider`, `ISettingProvider`, `ISettingManager`).
- ABP Commercial docs -- Setting Management module UI: `https://abp.io/docs/latest/modules/setting-management` (verified 2026-04-24; host + tenant scopes, Setting Management UI page).
- ABP Commercial docs -- Multi-tenancy and settings: `https://abp.io/docs/latest/framework/architecture/multi-tenancy` (verified 2026-04-24; tenant-scoped setting overrides auto-resolve via `ICurrentTenant`).
- Microsoft Learn -- AbpValidationException guidance: `https://abp.io/docs/latest/framework/fundamentals/exception-handling` (verified 2026-04-24; use `AbpValidationException` for validation errors to surface ABP's standard 400 response).
- Riok.Mapperly repo README: `https://github.com/riok/mapperly` (verified 2026-04-24; not directly needed -- no mapping changes in this capability).
- ABP community thread on per-tenant setting override pattern: `https://support.abp.io/QA/Questions/` (reviewed 2026-04-24; confirms tenant override precedence `User > Tenant > Global > Default`).

## Alternatives considered

A. **ABP SettingManagement with setting definitions (chosen).** Add 4 setting definitions (`CaseEvaluation.Booking.LeadTimeMinutes`, `CaseEvaluation.Booking.MaxHorizonQmeMinutes`, `...AmeMinutes`, `...OtherMinutes`) to `CaseEvaluationSettingDefinitionProvider`. `AppointmentManager` reads the 4 values via `ISettingProvider.GetAsync<int>(name)` inside a new `ValidateBookingTimeAsync(appointmentTypeId, appointmentStart)` method. Admin UI is the built-in ABP Setting Management page (no custom admin screen). Per-tenant overrides are free via the tenant-scope switcher in the existing UI. Integrates cleanly with ADR-004 doctor-per-tenant.

B. **Hardcoded constants in `AppointmentConsts`.** Simplest, one-day effort. Rejected because: (1) policies are business-configurable in OLD and clinics will expect the same; (2) per-tenant override is impossible; (3) violates the product intent that AppointmentLeadTime is admin-tunable.

C. **Custom `SystemParameter` aggregate root port.** Port OLD's entity wholesale (14 fields, single-row table, custom AppService + controller + UI). Rejected because: (1) Q8 direction from inventory is to delegate to ABP SettingManagement; (2) NEW already has a (currently empty) `CaseEvaluationSettingDefinitionProvider` scaffolded, signaling the intended path; (3) custom entity adds an unnecessary migration, repo, DbContext config, mapper, controller, Angular proxy, and admin screen; (4) NEW does not replicate OLD's single-row-table anti-pattern anywhere else.

D. **Per-appointment-type override table.** Model limits as rows on `AppointmentType` (new columns `LeadTimeMinutes`, `MaxHorizonMinutes`). Rejected because: (1) requires a schema migration on a host-scoped lookup entity; (2) splits the policy surface across 3+ rows instead of 1 setting block; (3) doesn't give per-tenant override without another entity.

E. **Feature flag + business-rule DSL (FluentValidation rule set).** Rejected because it overshoots MVP for a 4-integer decision table.

## Recommended solution for this MVP

Add 4 setting definitions to `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs` under a stable `CaseEvaluation.Booking.*` prefix, each with a sensible default (pending confirmation of OLD production values) and tenant-scope enabled so each doctor-tenant can override. In `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs`, inject `ISettingProvider` and add `ValidateBookingTimeAsync(Guid appointmentTypeId, string appointmentTypeName, DateTime appointmentStartUtc)` that reads the 4 values, computes `now + leadMinutes` and the per-type max horizon, and throws `AbpValidationException` when the candidate `appointmentStartUtc` falls outside the permitted window. Call it from `CreateAsync` and `UpdateAsync`. Wire a permission check for lead-time bypass by adding `CaseEvaluationPermissions.Appointments.BypassLeadTime` (new child permission) and short-circuiting the validator when the caller is authorized for it -- this replaces OLD's `InternalUser` claim gate. No new entity, no migration beyond the ABP-managed `AbpSettings` rows. Admin editing goes through the existing Setting Management module UI. The `AppointmentType` lookup name is used to resolve which max-horizon applies: the 3 OLD buckets map to NEW as `contains "PQME" -> Qme`, `contains "AME" -> Ame`, else `Other`. The mapping lookup happens once in the validator; no entity change.

## Why this solution beats the alternatives

- Matches the Q8 inventory direction and leverages the already-scaffolded (empty) `CaseEvaluationSettingDefinitionProvider` without porting OLD's 14-column entity.
- Per-tenant override comes free via ABP SettingManagement's existing UI; no admin screen work.
- Fewer moving parts than a custom entity: 1 file edit, 1 new method, 1 DI parameter, 1 permission, 1 set of localization keys. No migration, no controller, no proxy regen, no Angular admin screen.
- `AbpValidationException` produces the standard 400 response the Angular proxy already handles.

## Effort (sanity-check vs inventory estimate)

Inventory says **M (3 days)**. Analysis confirms **M (2-3 days)**. Breakdown: 0.5 day to author the 4 setting definitions + localization keys + permission; 1 day to implement `ValidateBookingTimeAsync` and wire it into Create/Update with the type-bucket mapping; 0.5 day for unit tests (setting override precedence, boundary cases at exactly lead-time minute, bypass-permission path, 3 type buckets); 0.5-1 day for integration test of the end-to-end 400 response and verification that SettingManagement UI shows the new settings.

## Dependencies

- Blocks: `appointment-state-machine` (G2-01) does not strictly block this, but both live on the Create/Update call sites -- the state-machine brief should assume this validator runs first. `appointment-booking-cascade` (G2-02) is orthogonal. `scheduler-notifications` is orthogonal.
- Blocked by: `system-parameters-vs-abp-settings` (the Q8 capability brief). If that capability resolves to "custom entity", this brief switches to Alternative C and requires re-estimation.
- Blocked by open question: **Q8 -- SystemParameter: keep as entity or delegate to ABP Settings Management? (Tracks 1, 3, 9)** (verbatim from `docs/gap-analysis/README.md:238`).

## Risk and rollback

- Blast radius if implementation goes wrong: only the `CreateAsync` / `UpdateAsync` paths on Appointment. A mis-typed setting key or mis-scaled unit (minutes vs days) would surface as every booking failing validation -- loud, not silent. Non-booking flows (list, view, accessor grants, attorney upsert) are unaffected.
- Rollback: remove the 4 setting definitions from `CaseEvaluationSettingDefinitionProvider.cs`, remove the call to `ValidateBookingTimeAsync` in Create/Update, drop the new permission child. No schema migration to revert. Existing `AbpSettings` rows written via the admin UI become orphan rows with no definition -- ABP tolerates orphan setting rows; they are ignored until a matching definition re-appears. No Angular proxy regen required for backend rollback (the admin UI is generic).
- Deploy-sequencing risk: the setting definition must exist before any tenant writes an override. Ship the definition in the same PR as the validator wiring.

## Open sub-questions surfaced by research

- What are OLD's production values for `AppointmentLeadTime`, `AppointmentMaxTimePQME`, `AppointmentMaxTimeAME`, `AppointmentMaxTimeOTHER`? Needed to pick sensible defaults for the 4 setting definitions. Requires clinic or Adrian input; OLD `spm.SystemParameters` in PROD would answer in one query.
- Unit: OLD stores these as `int days`. Should NEW keep days or normalize to minutes? Minutes give finer granularity (e.g. "2 hours notice"); days match existing policy and keep admin UI simple. Recommendation: minutes, with the default expressed as `24 * 60 * N` in the setting definition defaults.
- Timezone: NEW `Appointment.AppointmentDate` is `DateTime` (not `DateTimeOffset`). Is it stored UTC or local? If local, `DateTime.UtcNow.AddMinutes(leadMinutes)` vs `AppointmentDate` is incorrect across DST boundaries. Needs confirmation of the storage convention before shipping; if ambiguous, introduce a `DateTimeOffset`-conversion step in the validator.
- Type-name mapping: OLD used enum integer IDs for PQME/AME/OTHER. NEW has a host-scoped `AppointmentType` lookup with `Name` string. Which canonical strings exist in the seeded data? The mapping rule in the recommendation (substring match) is safe for PQME/AME/OTHER/REEVAL variants but brittle if the clinic adds a new type. Cross-reference with the `lookup-data-seeds` capability (DB-15) to pin the canonical names.
