# Deferred from MVP -- post-demo cleanup ledger

> Append-only register of work cut from Waves 0/1/2/3 to reach a demo-able MVP
> faster. Adrian returns to this list after Wave 3 ships and demo feedback lands.
> Each entry: what was cut, why, where the original decision lives, what unblocks
> picking it back up.

## From Wave 0 (locked when feat/mvp-wave-0 merged)

### Security / policy / hardening

- new-sec-05-hsts-header (XS) -- production HTTPS pinning. Source brief:
  docs/implementation-research/solutions/new-sec-05-hsts-header.md
- security-hardening (XS) -- CORS lock-down + password policy length 12.
  Source: docs/implementation-research/solutions/security-hardening.md
- new-sec-01-appointment-route-permission-guard -- Angular route guards on
  /appointments/view/:id and /appointments/add. Source:
  docs/implementation-research/solutions/new-sec-01-appointment-route-permission-guard.md
- new-sec-02-method-level-authorize -- finer-grained method-level [Authorize]
  on 3 AppServices + 4 helpers. Source:
  docs/implementation-research/solutions/new-sec-02-method-level-authorize.md
- new-sec-04-external-signup-real-defaults -- delete the
  ExternalSignupAppService class entirely (also closes SECURITY.md SEC-03).
  Source:
  docs/implementation-research/solutions/new-sec-04-external-signup-real-defaults.md

### Documentation / runbooks

- ADR-006 cascade-delete documentation. Source:
  docs/implementation-research/solutions/rest-api-parity-cleanup.md
- users-admin-management runbook. Source:
  docs/implementation-research/solutions/users-admin-management.md
- account-self-service runbook. Source:
  docs/implementation-research/solutions/account-self-service.md

### Wave 0 cap-internal carry-overs

- W0-1 data-quality fix: derived CaseEvaluationSaasTenantCreateDto carrying real
  FirstName/LastName/Gender for the onboarded Doctor + Angular tenant-create form
  widening. Doctor rows currently get LastName="" + Gender=Male placeholders.
  **CUT FROM W1-0** (2026-04-27): widening the volo SaaS Host vendor-module
  Angular page (the Adrian-side admin tenant-create form) requires verifying
  ABP's `ObjectExtensionManager.ConfigureSaas()` machinery propagates create-DTO
  extra properties through to the vendor Angular form, OR replacing the volo
  page with a custom Angular tenant-create page. Non-trivial; demo-non-blocking
  (Doctor display name shows in admin lists only, not in any demo flow). Pick
  back up post-W3 cleanup.
- W0-8 AppService delegation:
  PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync
  still uses email-only lookup. Wire to PatientManager.FindOrCreateAsync.
  **[LANDED IN W1-0]** (2026-04-27): wired in
  src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs.
  Email pre-check kept as fast-path; FindOrCreateAsync runs after IdentityUser
  resolution as 3-of-6 fuzzy-match safety net.

### W0-7 cosmetic gaps (verified 2026-04-27)

- en.json missing `Setting:CaseEvaluation.*` localization keys for the 12
  SettingDefinitions; admin UI shows raw keys until added. Cosmetic; not
  demo-blocking.
- `SystemParameters` permission group not registered in
  `CaseEvaluationPermissionDefinitionProvider.cs`. No UI consumer at W1;
  defer until a settings-admin screen is built.

### Tests / coverage

- All Wave 0 caps shipped without new tests. Aggregated under Wave 1
  new-qual-01-critical-path-test-coverage cap if Adrian keeps it in Wave 1; if
  cut from Wave 1, log it here too.

## From Wave 1

### On-the-fly W1-1 decisions (logged 2026-04-27 before execution; revisit in cleanup)

- **Send-back `FlaggedFields[]` shape: free-text `string` collection storing
  appointment-form field names (e.g. `["ClaimNumber", "EmployerAddress"]`).**
  *Why deferred:* a typed enum / form-field registry would give IDE-assisted
  refactoring + an admin-UI dropdown for the office's send-back action, but
  building the registry intersects W2's `custom-fields` cap (per-AppointmentType
  field visibility / pre-fill / disable config). Field-name strings keep W1-1
  decoupled from custom-fields scope.
  *Cleanup task:* once W2 custom-fields lands, refactor `FlaggedFields[]` to
  reference the `AppointmentTypeFieldDefinition` entity (or whatever form-field
  registry custom-fields produces); drop the freeform string list.
- **`Appointment.AppointmentStatus` setter visibility: stays PUBLIC at MVP.**
  *Why deferred:* the state-machine brief recommends narrowing to `internal`
  to force callers through `AppointmentManager`, but Mapperly target mapping,
  ABP `[ConcurrencyStamp]` reflection, and the auto-generated proxy projection
  all interact with the property. Verifying each integration is non-trivial
  (~half a day of touchwork). The convention is preserved at MVP via the
  `AppointmentManager` API surface (no AppService callers mutate status
  directly). Risk is a future contributor bypassing the manager.
  *Cleanup task:* narrow setter to `internal` (or `private set` with a
  `protected internal` mutation method); audit Mapperly targets,
  `AppointmentDto` projection, and `AppointmentManager.UpdateAsync` flow;
  add a unit test pinning the manager-only-mutation invariant.
- **Stateless graph: ALL 14 appointment transitions wired declaratively;
  only 4 endpoints exposed at W1-1.**
  *Why kept full:* cheap to wire all states in the same `BuildMachine` call;
  gives Wave 3 (`appointment-change-requests`) a clean integration point; the
  `ToDotGraph()` / `ToMermaid()` export documents the full lifecycle in code
  for review. Cancel / reschedule transitions are configured but unreachable
  until Wave 3 adds the corresponding endpoints + UI.
  *Cleanup task:* none beyond the Wave 3 endpoint additions; this is a
  positive over-investment, not a deferral. Listed here for audit only.

### Cuts logged during W1-0 execution

- **W0-1 SaaS DTO widening** (`CaseEvaluationSaasTenantCreateDto` derived from
  `SaasTenantCreateDto` + Angular volo-vendor-module form widening): cut from
  W1-0 -- see Wave 0 cap-internal carry-over note above for full rationale +
  cleanup task. Doctor row still seeds with `LastName=""` + `Gender=Male`
  placeholders; cosmetic only.

## From Wave 2

(append as Wave 2 ships)

## From Wave 3

(append as Wave 3 ships)

## Resumption order (filled in after Wave 3 demo)

(Adrian populates this when deciding what cleanup to tackle first)
