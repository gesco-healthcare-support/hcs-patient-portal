# Application Layer -- use cases, AppServices, Mapperly mappers

Orchestrates domain logic and exposes DTOs to the HTTP API. Every feature under `Domain/`
has a corresponding AppService here.

## What Lives Here

- **38 feature folders** (39+ counting any added after this doc): Appointments, Doctors,
  Patients, DoctorAvailabilities, ApplicantAttorneys, DefenseAttorneys, AppointmentAccessors,
  AppointmentApplicantAttorneys, AppointmentDefenseAttorneys, AppointmentBodyParts,
  AppointmentChangeRequests, AppointmentClaimExaminers, AppointmentDocuments,
  AppointmentEmployerDetails, AppointmentInjuryDetails, AppointmentLanguages,
  AppointmentPrimaryInsurances, AppointmentStatuses, AppointmentTypeFieldConfigs,
  AppointmentTypes, Books, CustomFields, Dashboards, DefenseAttorneys,
  DoctorPreferredLocations, Documents, Emailing, ExternalAccount, ExternalSignups,
  InternalUsers, Locations, Notifications, NotificationTemplates, PackageDetails,
  States, SystemParameters, UserProfile, Users, WcabOffices
- **Cross-cutting files** at the project root:
  - `CaseEvaluationApplicationMappers.cs` -- primary Mapperly mapper file; split across
    6 partial files (`*.AppointmentChangeRequests.cs`, `*.CustomFields.cs`,
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

IMPORTANT: Every AppService MUST carry `[RemoteService(IsEnabled = false)]`. Without it,
ABP auto-exposes duplicate routes alongside the manual controller, causing 500s on ambiguous
route resolution.

Known deviation: `ExternalSignupAppService` is missing this attribute. It may register
duplicate routes. Do not extend this pattern.

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

- `CaseEvaluationApplicationMappers.cs` is one logical unit spread across 6 `partial` files.
  Searching only the root file misses mappers for CustomFields, NotificationTemplates,
  PackageDetails, DoctorPreferredLocations, and AppointmentChangeRequests.
- `Books/BookAppService.cs` also extends `ApplicationService` directly (scaffold leftover).
- `ExternalSignups/` is not a standard entity CRUD feature -- it operates on ABP's
  `IdentityUser` and `Tenant` entities and calls `PatientManager` to create Patient records.
  Its `ExternalSignupController` sits at `api/public/external-signup` (not `api/app/`).

## Related

- docs/backend/APPLICATION-SERVICES.md
- docs/security/AUTHORIZATION.md
- docs/decisions/001-mapperly-over-automapper.md
- docs/decisions/002-manual-controllers-not-auto.md
