# Domain layer -- aggregate roots, domain services, repository interfaces

Root namespace `HealthcareSupport.CaseEvaluation`. All business invariants live here;
AppServices orchestrate, managers enforce.

## What lives here

| File / folder | Purpose |
|---|---|
| `Appointments/AppointmentManager.cs` | Aggregate root manager: create, update, state machine |
| `Appointments/Appointment.cs` | Aggregate root: 5 required FKs, `AppointmentStatusType`, `IMultiTenant` |
| `DoctorAvailabilities/DoctorAvailability.cs` | Slot aggregate: `AvailableDate`, `FromTime`/`ToTime`, `Capacity` (default 3), M2M `AppointmentTypes` |
| `Patients/PatientManager.cs` | Patient domain service: `CreateAsync`, `UpdateAsync`, `FindOrCreateAsync` (fuzzy match) |
| `AppointmentDocuments/AppointmentDocumentManager.cs` | Upload guard + `CreateQueuedAsync` factory |
| `AppointmentDocuments/AppointmentDocument.cs` | Document aggregate; `CreateQueued()` static factory |
| `AppointmentDocuments/AppointmentPacketManager.cs` | `EnsureGeneratingAsync` / `MarkGeneratedAsync` / `MarkFailedAsync` for PDF packet lifecycle |
| `Invitations/InvitationManager.cs` | Token issue / validate / accept with SHA256 hash storage |
| `BlobContainers/` | 7 ABP `[BlobContainerName]` marker classes (see below) |
| `Data/CaseEvaluationDbMigrationService.cs` | Host + tenant migration runner; calls `IDataSeeder` then iterates tenants |

## Framework subfolders

`Identity/`, `Saas/`, `OpenIddict/`, `Emailing/`, `Settings/`, `Data/`, `BlobContainers/` --
wrap ABP integration points. Rarely edited; change only when upgrading ABP or adding a new
blob container.

## Conventions

### Managers own all invariants; AppServices orchestrate only

Domain logic (guards, status transitions, capacity checks, token crypto) belongs in managers
or entity methods. AppServices call managers and coordinate cross-aggregate reads.

### IMPORTANT: Status transitions go through the state machine

NEVER set `Appointment.AppointmentStatus` directly from outside `AppointmentManager`.
The Stateless state machine in `AppointmentManager.ApplyTransitionAsync` (called via
`ApproveAsync`, `RejectAsync`, `RequestRescheduleAsync`) is the only valid path. Setting
the property directly bypasses the guard, skips the `AppointmentStatusChangedEto` publish,
and leaves `AppointmentApproveDate` / `RejectionNotes` unset.

### Capacity-aware slot booking

A slot stays `BookingStatus.Available` after a booking. Fullness is computed at booking
time: `active-appointment-count >= DoctorAvailability.Capacity` (default 3). The five
terminal statuses (`Rejected`, `CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`,
`RescheduledLate`) do not count toward active-appointment-count, so cancellation naturally
frees capacity. `BookingStatus.Booked` is a legacy value; the new model ignores it.

### Patient SSN never-clear rule

`SocialSecurityNumber` is stored plaintext (no encryption at rest). `PatientManager.UpdateAsync`
only overwrites it when the incoming value is non-empty. Sending an empty SSN on any of the
three update callers (admin, profile, booking) leaves the stored value unchanged. Do NOT
change this without an explicit product decision.

### Patient fuzzy matching

Call `PatientMatching.Normalise` / `NormaliseSsn` / `NormalisePhone` BEFORE
`IPatientRepository.FindBestMatchAsync`. Match threshold: any 3 of 6 keys
(FirstName, LastName, DOB, SSN digits-only, Phone digits-only, ZipCode).
`PatientManager.FindOrCreateAsync` is the canonical onboarding entry point.

### Dual-ctor pattern

`AppointmentChangeRequestManager` and `AppointmentAccessorManager` each expose a slim
constructor (repository only) and a full constructor (all collaborators).
When a method requires collaborators resolved only via the full ctor and they are null,
the manager throws `InvalidOperationException` (NOT `BusinessException`). Resolve via
the DI container to guarantee the full ctor is used.

### Appointment date check

`AppointmentManager.EnsureAppointmentDateNotInPast` compares `appointmentDate.Date < DateTime.Today`
(local server timezone). "Today" at the domain layer matches the Angular datepicker anchor.
On update, the guard fires only when the date is actually changing, so completed appointments
with past dates can still be edited.

## Blob containers

Seven `[BlobContainerName]` marker classes, all in `BlobContainers/`:

| Class | Container name |
|---|---|
| `AnonymousUploadsContainer` | `anonymous-uploads` |
| `AppointmentDocumentsContainer` | `appointment-documents` |
| `AppointmentPacketsContainer` | `appointment-packets` |
| `DocumentPackagesContainer` | `document-packages` |
| `JointDeclarationsContainer` | `joint-declarations` |
| `MasterDocumentsContainer` | `master-documents` |
| `UserSignaturesContainer` | `user-signatures` |

All are backed by ABP's DB-BLOB provider at MVP. Anonymous uploads require an explicit
`_currentTenant.Change(tenantId)` scope because the uploader has no tenant in the ABP
resolution chain yet.

## Thin host-scoped lookups (no own CLAUDE.md)

These live under their own subfolders but have no feature CLAUDE.md. Key facts only.

**States** -- single `Name` field; no `NameMaxLength` constant (effectively `nvarchar(max)`).
5 inbound FKs, all `SetNull`. `StatesAppService` uses `ObjectMapper.Map<>` -- pre-existing
Mapperly violation; flag it when that file is touched, do not silently copy the pattern.

**AppointmentStatuses** -- entity is NOT the state machine. The lifecycle enum is
`AppointmentStatusType` in `Domain.Shared`; `AppointmentStatus` lookup rows are display-name
metadata and are disconnected from `AppointmentStatusType` by design.

**AppointmentTypes** -- host-scoped; M2M with Doctor via `DoctorAppointmentType`; supports
Excel export. Referenced by slot type-matching in the booking gate.

**AppointmentLanguages** -- exists in BOTH DbContexts but has no `DataSeedContributor`;
defaults to English via `AppointmentLanguageId = null` on Patient. Missing seed means
the language picker is empty until admin creates entries.

**WcabOffices** -- 6 string fields; Excel export + download-token CSRF pattern.
Host-scoped; no tenant data.

## Notable features (no own CLAUDE.md)

**AppointmentTypeFieldConfigs** -- composite unique `(TenantId, AppointmentTypeId, FieldName)`.
Carries `Hidden`, `ReadOnly`, `DefaultValue` per field. No manager; AppService mutates
directly.

**CustomFields** -- per-`AppointmentType` dynamic fields; `CustomFieldType` enum; max 10
active per type. Distinct from `AppointmentTypeFieldConfig` (type-level config vs.
custom data collection).

**PackageDetails** -- per-`AppointmentType` packet template; `DocumentPackage` M2M. At most
one `IsActive` row per type is enforced at the AppService layer. No domain manager.

**DefenseAttorneys** -- full entity with `IdentityUserId` + `StateId` FK + firm fields.
No `AppointmentDefenseAttorney` join entity (asymmetric vs. `ApplicantAttorney`, which does
have a join entity). Attorney data hangs directly off `Appointment` via denormalized email
fields.

## Gotchas

- `DoctorAvailability.BookingStatusId` is mutable on update with no guard. A staff user
  can flip a `Booked` slot back to `Available` while an active `Appointment` still references
  it. No domain-level protection exists today.
- `AppointmentDocument.CreateQueued()` uses `"(pending-upload)"` as sentinel for `BlobName`
  and `FileName`. Guard against blob-fetch attempts on rows in `DocumentStatus.Pending`
  before real metadata is written by the upload path.
- Notification HTML loading is silent on missing template. A missing `.html` file does not
  throw; the email is sent with an empty body. Verify template paths when adding new
  notification types.
- `Appointment.AppointmentApproveDate` is stamped in two places: inside `ApplyTransitionAsync`
  on `Approve` trigger and in `CreateAsync` when the initial status is already `Approved`
  (internal fast-path). Both use `DateTime.UtcNow`.

## Related

- docs/decisions/004-doctor-per-tenant-model.md
- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- docs/database/EF-CORE-DESIGN.md
