# Application Layer -- use cases, AppServices, Mapperly mappers

Orchestrates domain logic and exposes DTOs to the HTTP API. Every feature under `Domain/`
has a corresponding AppService here.

## What Lives Here

- One folder per feature. List them with
  `ls -d src/HealthcareSupport.CaseEvaluation.Application/*/` rather than trusting a copied
  list here; a hand-maintained list went stale (it named a removed `Books` folder and missed
  a dozen newer ones).
- **Cross-cutting files** at the project root:
  - `CaseEvaluationApplicationMappers.cs` -- primary Mapperly mapper file; split across
    partial files (`*.AppointmentChangeRequests.cs`, `*.CustomFields.cs`,
    `*.DoctorPreferredLocations.cs`, `*.NotificationTemplates.cs`, `*.PackageDetails.cs`)
  - `CaseEvaluationApplicationModule.cs` -- ABP module definition
  - `CaseEvaluationAppService.cs` -- base class; wires localization + permission helpers

## Conventions

### AppService base class

IMPORTANT: Extend `CaseEvaluationAppService`, NOT `ApplicationService` directly.

Two known deviations (do not replicate):

- `SystemParametersAppService` extends `ApplicationService` -- localization calls fall back
  to the default ABP resource instead of the project's `CaseEvaluationResource`.
- `NotificationTemplatesAppService` extends `ApplicationService` -- same localization fallback.

Fix in a dedicated chore ticket; do not silently add more `ApplicationService` subclasses.

### RemoteService attribute

See root CLAUDE.md for the `[RemoteService(IsEnabled = false)]` rule.

Not every service carries it. Some are served on purpose by ABP's conventional controllers
(for example `DoctorTenantAppService` at `/api/app/doctor-tenant`); others lack it but also
have a hand-written controller (for example `ExternalSignupAppService`). List the services
without it before assuming a service is or is not on the HTTP surface:

```bash
git grep -L "RemoteService(IsEnabled = false)" -- 'src/HealthcareSupport.CaseEvaluation.Application/*AppService.cs'
```

New services follow the rule unless being auto-exposed is the intent.

### Mapperly mappers

Add new mappers as additional `partial class` entries in `CaseEvaluationApplicationMappers.cs`
(or a new named partial file if it is a cross-cutting concern). Annotate with
`[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]` and extend
`MapperBase<TSource, TDest>`. Missing target members are compile errors, not runtime errors.

See ADR: `docs/decisions/001-mapperly-over-automapper.md`.

### Permissions

Enforce in AppServices, not controllers. Apply
`[Authorize(CaseEvaluationPermissions.{Entity}.Default)]` at class level; override with
`.Create` / `.Edit` / `.Delete` on individual methods.

### SSN masking -- mandatory on all patient DTO exits

Every method that returns a `PatientDto` or `PatientWithNavigationPropertiesDto` MUST call
`SsnVisibility.MaskToLast4(dto)` before returning (both read and write paths). The SSN
field is masked to the last 4 digits on every standard response.

`GetFullSsnAsync` is the ONLY endpoint that returns the full SSN value; it is audited and
lives in `Patients/PatientsAppService.cs`. Do not bypass masking on any other path.

## Gotchas

- `CaseEvaluationApplicationMappers.cs` is one logical unit spread across multiple `partial`
  files. Searching only the root file misses mappers for CustomFields, NotificationTemplates,
  PackageDetails, DoctorPreferredLocations, and AppointmentChangeRequests.
- `ExternalSignups/` is not a standard entity CRUD feature -- it operates on ABP's
  `IdentityUser` and `Tenant` entities. On registration it claims or creates the Patient
  record, or creates or adopts the Applicant Attorney, Defense Attorney or Claim Examiner
  master record, and it owns the external-user invitation lifecycle.
  Its `ExternalSignupController` sits at `api/public/external-signup` (not `api/app/`).

## Related

- docs/backend/APPLICATION-SERVICES.md
- docs/security/AUTHORIZATION.md
- docs/decisions/001-mapperly-over-automapper.md
- docs/decisions/002-manual-controllers-not-auto.md
