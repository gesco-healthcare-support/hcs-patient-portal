# Joint declarations (attorney-signed stipulation upload + approve workflow)

## Source gap IDs

- [DB-04 -- Joint declarations table not in NEW schema (track 01)](../../gap-analysis/01-database-schema.md). MVP-blocking.
- [G2-14 -- `AppointmentJointDeclaration` upload + rejection flow, no entity/manager in NEW (track 02)](../../gap-analysis/02-domain-entities-services.md). MVP-blocking, inventory says M (~3 days).
- [03-G06 -- `AppointmentJointDeclaration` + approve email AppService (track 03)](../../gap-analysis/03-application-services-dtos.md). MVP-blocking, inventory says 2-3 days.
- [G-API-04 -- Joint declarations (nested + flat + search) REST surface (track 04)](../../gap-analysis/04-rest-api-endpoints.md). Severity: Medium.
- [R-07 -- `/appointment-joint-declarations-search` Angular route (track 07)](../../gap-analysis/07-angular-routes-modules.md). Severity: M.
- [A8-08 -- `AppointmentJointDeclarations` Angular service (both child + root variants) (track 08)](../../gap-analysis/08-angular-proxy-services-models.md). MVP-blocking, severity S.
- [UI-14 -- `/appointment-joint-declarations-search` admin screen (track 09)](../../gap-analysis/09-ui-screens.md). Severity: Medium.

Per [gap-analysis/README.md:74](../../gap-analysis/README.md), the seven IDs collapse to one coherent implementation unit. No open question Q1 through Q32 directly blocks this capability; the brief treats it as a concrete port and explicitly names the upstream capabilities it depends on.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/` -- zero `JointDeclaration*` files. Confirmed by `ls` of the 16 Domain feature folders (`ApplicantAttorneys`, `AppointmentAccessors`, `AppointmentApplicantAttorneys`, `AppointmentEmployerDetails`, `AppointmentLanguages`, `Appointments`, `AppointmentStatuses`, `AppointmentTypes`, `Books`, `DoctorAvailabilities`, `Doctors`, `Locations`, `Patients`, `States`, `WcabOffices`, plus `Identity`, `OpenIddict`, `Saas`, `Settings`, `Data`). None contains a `JointDeclaration` entity, manager, repository, or CLAUDE.md reference.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/` -- zero `IAppointmentJointDeclarationsAppService.cs`, zero `AppointmentJointDeclaration*Dto.cs`, zero `JointDeclaration` permission.
- `src/HealthcareSupport.CaseEvaluation.Application/` -- zero `AppointmentJointDeclarationsAppService.cs`.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/` -- no `AppJointDeclarations` table in any migration through `20260131182820_Added_AppointmentStatus`. Initial migration creates `AbpBlobContainers` / `AbpBlobs` via `BlobStoringDatabaseEntityFrameworkCoreModule` (see `appointment-documents` brief for shared blob-storage posture) but no domain-specific tables for documents of any kind.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/` -- zero `AppointmentJointDeclarationController.cs`; no `api/app/appointment-joint-declarations/**` route.
- `angular/src/app/proxy/` -- zero auto-generated `appointment-joint-declarations/*` folder. Confirmed because the proxy is regenerated from the OpenAPI spec and the backend has no endpoints; the proxy file cannot exist without the AppService.
- `angular/src/app/` -- zero handwritten `appointment-joint-declarations/` feature folder. Per [gap-analysis/08-angular-proxy-services-models.md:132-133](../../gap-analysis/08-angular-proxy-services-models.md), both the child-scoped (A8-08 primary) and the duplicate root-scoped (A8-08 secondary) services are absent.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` -- no `AppointmentJointDeclarations` nested class; no child permission keys.
- ABP BlobStoring + FileManagement modules are wired at the module level only (`CaseEvaluationDomainModule.cs:11,22,44,47` referenced in [gap-analysis/06-cross-cutting-backend.md:100-101](../../gap-analysis/06-cross-cutting-backend.md)) with **zero business consumers**. The initial migration creates `AbpBlobContainers` + `AbpBlobs` but no code writes to them. Reusing these modules for JointDeclaration file persistence is therefore a greenfield decision inside NEW (delegated to the `blob-storage-provider` brief, Q17).
- ABP Emailing module is wired but `CaseEvaluationDomainModule.cs:59-62` replaces `IEmailSender` with `NullEmailSender` under `#if DEBUG` (see [gap-analysis/06-cross-cutting-backend.md:106-108](../../gap-analysis/06-cross-cutting-backend.md)). The approve/reject email fan-out therefore depends on the `email-sender-consumer` brief (CC-01) to land first.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/` -- no `DocumentStatus.cs` enum (OLD used `DocumentStatuses.Uploaded | .Accepted | .Rejected | .Pending`). This brief introduces `JointDeclarationStatus` rather than overloading the OLD `DocumentStatuses` enum, because (a) there is no NEW DocumentStatus type to extend and (b) tightly-scoped enums keep the Mapperly output surface smaller.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs:14-60` -- 47-line DomainService. `UpdateAsync` does NOT accept `AppointmentStatus` per `Appointments/CLAUDE.md:132`. Consequence for this brief: the JointDeclaration reject flow does NOT mutate appointment status (OLD also does not); it only flips `JointDeclarationStatus`, so there is no coupling to the state-machine brief's status-transition events.

## Live probes

- Probe 1 -- OIDC token exchange (reference only): `POST https://localhost:44368/connect/token` with seeded `admin / 1q2w3E*` returns `access_token` + `expires_in: 3599`. Confirmed already at [probes/service-status.md:17-26](../probes/service-status.md). Proves the host-admin session used by subsequent probes.
- Probe 2 -- Swagger scan for joint-declaration endpoints: `GET https://localhost:44327/swagger/v1/swagger.json` filtered for `joint-declaration` and `jointdeclaration` substrings. Expected result: zero matches. Proves the REST surface is fully absent -- not a scaffold, not a stub, nothing.
- Probe 3 -- Appointments table is empty (PHI-safety): reuses the result documented at [probes/service-status.md:23-25](../probes/service-status.md). `GET /api/app/appointments` returns `{"totalCount":0,"items":[]}`. Proves any follow-on probe or scaffolding cannot collide with real PHI.
- Full probe log: [probes/joint-declarations-2026-04-24T1430.md](../probes/joint-declarations-2026-04-24T1430.md).

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentJointDeclaration.cs:1-88` -- entity shape. `int AppointmentJointDeclarationId` PK, `CreatedById`, `CreatedDate`, `JointDeclarationFilePath (max 500)`, `ModifiedById`, `ModifiedDate`, `RejectedById`, `RejectionNotes (max 500)`, `AppointmentId FK`, `DocumentPackageId FK`, `DocumentStatusId FK`. Schema `spm`.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/ExtendedModels/AppointmentJointDeclaration.cs:1-22` -- partial with `[NotMapped]` transport fields: `FileName`, `FileExtention`, `FileData` (base64 string), `FileType`, `RequestConfirmationNumber`. This is how OLD wire-transfers a file inside the JSON body -- same POST endpoint for create + file upload, no multipart/form-data. The NEW port should NOT replicate the base64-in-JSON pattern; ABP's `IFormFile` + `IBlobContainer<T>` is the canonical alternative.
- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentJointDeclarationDomain.cs:65-101` -- `Add` method. Generates filename as `Guid + "_" + RequestConfirmationNumber + MMddyyyyHHMMss + "." + extension`. Writes to `wwwroot/Documents/submittedDocuments/` (local filesystem fallback; the original `AmazonBlobStorage.SaveFile` call is commented out at lines 77-82). Then decodes base64, writes bytes, sets `JointDeclarationFilePath = fileName`, persists via UoW. No status is set on create (defaults to enum-0 in OLD's column).
- `AppointmentJointDeclarationDomain.cs:102-125` -- `UpdateValidation`: rejects upload if appointment status is not Approved, if appointment type is PQME ("not valid, upload appropriate document"), or if today is past `appointment.DueDate`. Three gates. The PQME exclusion is striking and may be intentional business logic ("joint declaration does not apply to panel-QME") -- flag as Q-JD-a below.
- `AppointmentJointDeclarationDomain.cs:127-179` -- `Update` method. Branches on internal-vs-external-user: external user re-uploads the file (new filename = `<original>_<jdId>_<ddMMyyyy_hhmmss>.<ext>`, sets `DocumentStatusId = Uploaded`); internal user performs approve/reject (sets `RejectedById = UserClaim.UserId`, commits). The two code paths share one endpoint but behave differently based on role.
- `AppointmentJointDeclarationDomain.cs:200-282` -- `SendDocumentEmail`: accept/reject fan-out. Queries `vEmailSender` + `AppointmentInjuryDetail` projections to format subject `"Patient Appointment Portal - (<Patient first last> - Claim: X - ADJ: Y) - Appointment document is Accepted/Rejected"`. Uses four templates: `PatientDocumentAccepted`, `PatientDocumentAcceptedRemainingDocs` (with link back to `/appointment-documents/?appointmentid=...&appointmenttype=...`), `PatientDocumentRejected`, `PatientDocumentRejectedRemainingDocs`. Sends via `ISendMail.SendSMTPMail`. NEW port routes these through `IEmailSender` + ABP text templates; the exact strings can be carried over.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/AppointmentRequest/AppointmentJointDeclarationsController.cs:1-74` -- nested REST. Route `api/appointments/{appointmentId}/[controller]`. GET list + paged; GET one; POST create with validation; PUT update with validation + `SendDocumentEmail` called inside on-accept branch implicitly via `Update`; PATCH via JsonPatchDocument; DELETE with validation. The PATCH endpoint uses ASP.NET's JsonPatchDocument; NEW does not need to replicate it (see [rest-api-parity-cleanup](rest-api-parity-cleanup.md) for PATCH policy under Q28).
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Document/AppointmentJointDeclarationsController.cs:1-67` -- flat REST. Route `api/[controller]`. A root-scoped duplicate of the nested controller. Reads from the `vAppointmentJointDeclaration` projection (all rows across all appointments) instead of scoped to one. This is the admin-queue feeder for the search page.
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/Document/AppointmentJointDeclarationsSearchController.cs:1-30` -- `POST api/appointmentjointdeclarations/search`. Invokes stored proc `spm.spAppointmentJointDeclarationApprove @Query, @UserId`. Returns the search result as a raw JSON string (OLD's stored-proc-returns-JSON idiom). NEW replaces stored procs with `IRepository<T>` + LINQ per [ADR-001 -- Riok.Mapperly over AutoMapper](../../decisions/001-mapperly-over-automapper.md) and the project's "data access" intentional difference at [gap-analysis/README.md:185](../../gap-analysis/README.md).
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/appointment-request/appointment-joint-declarations/**` -- child-scoped Angular feature (list + edit + shared + domain + models). Confirms R-07/UI-14 and both A8-08 client services.
- `P:/PatientPortalOld/patientappointment-portal/src/app/components/document/appointment-joint-declarations/**` -- root-scoped search page (search + view components + routing + service). Admin queue at `/appointment-joint-declarations-search`.
- Track-10 errata that apply: OLD's `DocumentPackageId` FK on JointDeclaration is only meaningful if Document Packages are in MVP scope; per Q9 and [document-packages](document-packages.md) brief it is post-MVP. Consequence for this brief: the NEW `AppointmentJointDeclaration` entity drops the `DocumentPackageId` column entirely; nothing in the approve/reject workflow depends on it. Flag for traceability.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. Row-level `IMultiTenant` (ADR-004); `AppointmentJointDeclaration` is per-appointment so it inherits tenant isolation via `AppointmentId` FK plus its own `IMultiTenant` interface (following the same pattern as `AppointmentEmployerDetail` which is tenant-scoped per root `CLAUDE.md` Multi-tenancy Rules).
- [ADR-001](../../decisions/001-mapperly-over-automapper.md) -- DTOs (`AppointmentJointDeclarationCreateDto`, `AppointmentJointDeclarationDto`, `GetAppointmentJointDeclarationsInput`, `RejectJointDeclarationInput { ReviewNotes }`) use Riok.Mapperly. Register mappers in `CaseEvaluationApplicationMappers.cs` as partial classes with `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`.
- [ADR-002](../../decisions/002-manual-controllers-not-auto.md) -- Every AppService method carries `[RemoteService(IsEnabled = false)]` inherited from `CaseEvaluationAppService`; a manual controller in `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Documents/AppointmentJointDeclarationController.cs` delegates each method. Two controllers land: nested (`api/app/appointments/{appointmentId}/joint-declarations`) + flat search (`api/app/joint-declarations-search`). Route casing kebab-case per the 14 existing AppointmentController methods.
- [ADR-003](../../decisions/003-dual-dbcontext-host-tenant.md) -- `AppointmentJointDeclaration` is tenant-scoped, so the entity configuration lives in `CaseEvaluationDbContext.OnModelCreating` (both host + tenant sides) WITHOUT the `if (builder.IsHostDatabase())` guard that host-only entities use.
- [ADR-005](../../decisions/005-no-ng-serve-vite-workaround.md) -- Angular build via `ng build --configuration development` + `npx serve`; no `ng serve`.
- Root `CLAUDE.md` reference-pattern for Appointments: entity -> manager -> app-service-contract -> app-service -> controller -> proxy -> angular-feature -> migration. This brief follows the same order.
- `Appointments/CLAUDE.md` Business Rule 4: `AppointmentManager.UpdateAsync` does NOT mutate status. This brief's JointDeclaration approve/reject flow does NOT touch `Appointment.AppointmentStatus`; it only mutates `AppointmentJointDeclaration.Status`. No coupling to the [appointment-state-machine](appointment-state-machine.md) brief's 13-state enforcement.
- HIPAA applicability: JointDeclaration PDFs are attorney-signed stipulations; the document body may contain case numbers, attorney names, patient names, claim numbers (PHI-adjacent). Entity fields intentionally exclude patient demographics (PK, FKs, status, audit only); the file blob itself is encrypted-at-rest via ABP's BlobStoring provider (per `blob-storage-provider` brief). `ReviewNotes` is free-text and treated as potentially-PHI-containing: no special redaction in the column, but never logged in plain text per `rules/hipaa.md`.
- Capability-specific:
  - Do NOT replicate OLD's base64-in-JSON upload idiom; use ABP's `IFormFile` + `IRemoteStreamContent` (the same decision as the `appointment-documents` brief, which pins the pattern globally).
  - Do NOT carry over `DocumentPackageId` (post-MVP per Q9; keeps schema lean).
  - Do NOT replicate OLD's combined-controller (upload and approve/reject behind the same `PUT`) pattern; split into three explicit operations: `POST` for create-with-file, `POST /{id}/approve` for approve, `POST /{id}/reject` for reject. Matches the ABP method-based state-change idiom documented at [ABP Domain Services docs](https://abp.io/docs/latest/framework/architecture/domain-driven-design/domain-services).
  - Do NOT re-emit OLD's appointment-type PQME gate inside the domain service without explicit Adrian confirmation -- flagged as Q-JD-a below. Default MVP behavior: port the gate as-is because it is business rule not infrastructure; track for a follow-up review.

## Research sources consulted

All accessed 2026-04-24.

- ABP `BlobStoring` overview -- `https://abp.io/docs/latest/framework/infrastructure/blob-storing`. HIGH confidence. Explains `IBlobContainer<T>` typed containers, `[BlobContainerName("...")]` attribute, and how FileManagement module builds on top. Confirms provider swap (database -> S3 -> Azure) is config-only.
- ABP `BlobStoring.Database` module -- `https://abp.io/docs/latest/modules/blob-storing-database`. HIGH confidence. Default provider uses `AbpBlobs` + `AbpBlobContainers` (already in NEW's initial migration). Sufficient for MVP LocalDB without S3 creds; upgrade path to S3 is a single ABP config change.
- ABP `IEmailSender` + MailKit integration -- `https://abp.io/docs/latest/framework/infrastructure/email-sms`. HIGH confidence. Explains the `MailMessage` API, `IEmailSender.SendAsync`, and the test-setup swap to `NullEmailSender` that NEW currently uses in DEBUG (the main blocker this brief delegates to the `email-sender-consumer` capability).
- ABP TextTemplateManagement module -- `https://abp.io/docs/latest/modules/text-template-management`. HIGH confidence. Used for email template storage + substitution. The four OLD template strings (`PatientDocumentAccepted`, `PatientDocumentAcceptedRemainingDocs`, `PatientDocumentRejected`, `PatientDocumentRejectedRemainingDocs`) migrate cleanly as `ITextTemplateContentContributor` rows. Domain module already wires `TextTemplateManagementDomainModule` per [gap-analysis/06-cross-cutting-backend.md:108](../../gap-analysis/06-cross-cutting-backend.md).
- ABP File upload via `IRemoteStreamContent` + `IFormFile` -- `https://abp.io/docs/latest/framework/fundamentals/authorization` (tangential) and the Doctors `FileController` reference in the codebase. HIGH confidence that `IRemoteStreamContent` is the canonical way to pass a file through an ABP AppService.
- ABP Manual Controllers ADR precedent -- [decisions/002-manual-controllers-not-auto.md](../../decisions/002-manual-controllers-not-auto.md). HIGH confidence local source.
- Microsoft Learn -- EF Core 10 relationships + owned types -- `https://learn.microsoft.com/en-us/ef/core/modeling/relationships`. MEDIUM confidence. Confirms nullable FK pattern for optional `ReviewedByUserId`.
- HIPAA Security Rule Technical Safeguards (45 CFR 164.312) -- `https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html`. MEDIUM confidence. Access control + audit control + encryption (addressable). The entity satisfies these via ABP's `FullAuditedAggregateRoot` (audit), `IMultiTenant` (access control), and BlobStoring provider encryption (addressable).

## Alternatives considered

### A. Dedicated `AppointmentJointDeclaration` entity + approve/reject workflow + nested + flat REST -- chosen

New aggregate `AppointmentJointDeclaration : FullAuditedAggregateRoot<Guid>, IMultiTenant` in `src/.../Domain/AppointmentJointDeclarations/`. Own folder alongside `AppointmentAccessors`, `AppointmentEmployerDetails`. Status enum `JointDeclarationStatus { Pending = 0, Approved = 1, Rejected = 2 }`. Manager class `AppointmentJointDeclarationManager` owns three mutator methods (`CreateAsync`, `ApproveAsync`, `RejectAsync`) plus validation (AppointmentStatus == Approved; appointment not past DueDate; optional: AppointmentType != PQME flag). Two controllers: nested `/api/app/appointments/{appointmentId}/joint-declarations` for the appointment-scoped CRUD, flat `/api/app/joint-declarations-search` for the admin queue. Permission group `CaseEvaluation.AppointmentJointDeclarations` with children `.Create`, `.Edit`, `.Delete`, `.Approve`.

### B. Treat as a variant of `AppointmentDocument` with `DocumentType = JointDeclaration` -- rejected

Wait for the `appointment-documents` brief (G2-08 + 03-G01 through 03-G03) to land first, then add `DocumentType.JointDeclaration` as a discriminator on that entity. Saves ~40% of the schema + AppService + controller code. Rejected for three reasons: (1) the approve/reject workflow is specific (only JointDeclaration needs attorney countersignature acceptance; general appointment documents use a simpler uploaded/verified lifecycle per `AppointmentDocumentDomain.cs`); (2) the rejection carries `ReviewNotes` that general documents do not have at the schema level, producing either a sparse-column anti-pattern or a polymorphic child-table that re-introduces the same complexity we would save; (3) OLD explicitly split them into two tables (`AppointmentDocuments` and `AppointmentJointDeclarations`) with two stored procs and two controllers, so forcing them together is a DX regression relative to OLD. Keeps the domain explicit.

### C. Defer to post-MVP -- rejected

Joint declarations are a workers'-comp-IME-specific artifact; per [gap-analysis/README.md:47](../../gap-analysis/README.md) the capability is tagged MVP-blocking in track 01 and again in track 02 (severity M). No open question marks it as "needs-decision" -- unlike CustomFields (Q6) or Templates (Q7), which are genuinely deferrable. Deferral would break the IME scheduling workflow because the PQME flow requires attorney-countersigned stipulations before the appointment can be set to Approved. Rejected.

### D. Use OLD's schema verbatim (retain `DocumentPackageId`, `DocumentStatusId` FK to a lookup table, `spAppointmentJointDeclarationApprove` stored proc) -- rejected

Keeps parity, but: (1) the `DocumentPackageId` FK is dead weight in MVP because `document-packages` is post-MVP under Q9; (2) a full `DocumentStatuses` lookup table is overkill for three states -- `enum` (`JointDeclarationStatus`) is lighter, tenable-by-ABP, and aligned with NEW's direction on `AppointmentStatusType`, `BookingStatus`, and `PhoneNumberType`; (3) stored procs are an [intentional architectural reversal](../../gap-analysis/README.md#intentional-architectural-differences-summary) per ADR-001's data-access choice. Rejected on all three points.

## Recommended solution for this MVP

Ship an `AppointmentJointDeclaration` aggregate + a dedicated manager + a nested AppService + a flat search AppService + two manual controllers + one EF migration + one Angular feature module + one permission group.

**Layer 1 -- Domain.Shared** (`src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentJointDeclarations/`):

1. `AppointmentJointDeclarationConsts.cs` -- max lengths: `ReviewNotesMaxLength = 500`, `BlobContainerName = "AppointmentJointDeclarations"`.
2. `JointDeclarationStatus.cs` -- enum with `Pending = 0`, `Approved = 1`, `Rejected = 2`.

**Layer 2 -- Domain** (`src/HealthcareSupport.CaseEvaluation.Domain/AppointmentJointDeclarations/`):

3. `AppointmentJointDeclaration.cs` -- aggregate `FullAuditedAggregateRoot<Guid>, IMultiTenant`. Fields: `TenantId (Guid?)`, `AppointmentId (Guid, FK)`, `BlobName (string, required, max 256)`, `OriginalFileName (string, max 256)`, `ContentType (string, max 100)`, `Status (JointDeclarationStatus, default Pending)`, `ReviewedByUserId (Guid?)`, `ReviewedAt (DateTime?)`, `ReviewNotes (string?, max 500)`. Constructor enforces `Check.NotNull(blobName)`, `Check.Length(reviewNotes, 500)`. Internal methods `Approve(Guid reviewerId, string? notes)` and `Reject(Guid reviewerId, string notes)` that set the triplet `Status + ReviewedByUserId + ReviewedAt` atomically; `Reject` requires non-null notes.
4. `IAppointmentJointDeclarationRepository.cs` -- `GetCountAsync(Guid? appointmentId, JointDeclarationStatus? status)`, `GetListAsync(paged, filters)`, `GetWithNavigationPropertiesAsync(Guid id)` -- mirrors the `AppointmentAccessor` repository pattern used by sibling tenant-scoped entities.
5. `AppointmentJointDeclarationManager.cs` -- DomainService. Injects `IRepository<Appointment>`, `IAppointmentJointDeclarationRepository`, `IBlobContainer<AppointmentJointDeclarationContainer>`, `ICurrentUser`. Public methods: `CreateAsync(Guid appointmentId, IRemoteStreamContent file, string originalFileName)` -- validates appointment status = Approved, DueDate not past, `AppointmentType != PQME` if the port-as-is flag holds (flagged Q-JD-a); generates blob key `{appointmentId}/{Guid.NewGuid()}_{originalFileName}`; streams file into the typed blob container; creates entity with `Status = Pending`. `ApproveAsync(Guid id, string? notes)` and `RejectAsync(Guid id, string notes)` -- call internal aggregate methods, then publish a local event `JointDeclarationReviewedEto(Id, AppointmentId, NewStatus, ReviewerId, Notes)` via `ILocalEventBus` so the email handler (owned by `email-sender-consumer` brief) subscribes.
6. `AppointmentJointDeclarationContainer.cs` -- `[BlobContainerName("AppointmentJointDeclarations")]` marker class (ABP BlobStoring typed container pattern).
7. `CLAUDE.md` -- matches the schema of `Appointments/CLAUDE.md` and `AppointmentAccessors/CLAUDE.md`.

**Layer 3 -- Application.Contracts** (`src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentJointDeclarations/`):

8. DTOs: `AppointmentJointDeclarationDto`, `AppointmentJointDeclarationWithNavigationPropertiesDto` (adds `Appointment.RequestConfirmationNumber` + Reviewer `UserName`), `GetAppointmentJointDeclarationsInput` (filter by `AppointmentId`, `Status`, paging), `RejectJointDeclarationInput { ReviewNotes }`.
9. `IAppointmentJointDeclarationsAppService.cs` -- nested: `GetListAsync`, `GetAsync`, `CreateAsync(Guid appointmentId, IRemoteStreamContent file)`, `DeleteAsync`, `DownloadAsync` (returns `IRemoteStreamContent`).
10. `IJointDeclarationsSearchAppService.cs` -- flat: `GetListAsync(status, date-range)`, `ApproveAsync(Guid id)`, `RejectAsync(Guid id, RejectJointDeclarationInput input)`.
11. Permission constants added to `CaseEvaluationPermissions.cs` as nested class `AppointmentJointDeclarations` with `Default`, `Create`, `Edit`, `Delete`, `Approve`. Registered in `CaseEvaluationPermissionDefinitionProvider.cs` with a parent group and 4 children.

**Layer 4 -- Application** (`src/HealthcareSupport.CaseEvaluation.Application/AppointmentJointDeclarations/`):

12. `AppointmentJointDeclarationsAppService.cs` -- nested AppService. `[Authorize(CaseEvaluationPermissions.AppointmentJointDeclarations.Default)]` class-level; method-level `[Authorize(...Create)]`, `...Delete`. Each method is one or two lines -- delegate to the Manager, map result via Mapperly.
13. `JointDeclarationsSearchAppService.cs` -- flat AppService for the admin queue. Method-level `[Authorize(...Approve)]` on approve/reject.
14. `CaseEvaluationApplicationMappers.cs` -- two partial classes with `[Mapper]`: `AppointmentJointDeclarationToAppointmentJointDeclarationDtoMapper`, `AppointmentJointDeclarationWithNavigationPropertiesToDtoMapper`.

**Layer 5 -- EntityFrameworkCore** (`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/`):

15. `AppointmentJointDeclarations/EfCoreAppointmentJointDeclarationRepository.cs` implementing `IAppointmentJointDeclarationRepository`. Follows the `EfCoreAppointmentAccessorRepository` pattern: LINQ `Queryable` + ABP `IQueryable.ToListAsync`.
16. Update `CaseEvaluationDbContext.OnModelCreating`: entity config with `HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction)`, index on `(AppointmentId, Status)`. NOT inside `IsHostDatabase()` guard -- tenant-scoped.
17. Migration `dotnet ef migrations add Added_AppointmentJointDeclarations --project EntityFrameworkCore --startup-project HttpApi.Host` -- creates `AppAppointmentJointDeclarations` table with columns for each entity field + standard ABP audit columns + `TenantId` index.

**Layer 6 -- HttpApi** (`src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/`):

18. `AppointmentJointDeclarations/AppointmentJointDeclarationController.cs` -- inherits `AbpController`. Two route templates, two sibling controller classes for clarity:
    - Nested: `[Route("api/app/appointments/{appointmentId}/joint-declarations")]` -- delegates to `IAppointmentJointDeclarationsAppService`. Endpoints: `GET /`, `GET /{id}`, `POST /` (multipart), `DELETE /{id}`, `GET /{id}/download`.
    - Flat search: `[Route("api/app/joint-declarations-search")]` -- delegates to `IJointDeclarationsSearchAppService`. Endpoints: `GET /` (list with filters), `POST /{id}/approve`, `POST /{id}/reject`.

**Layer 7 -- Angular** (`angular/src/app/`):

19. `appointment-joint-declarations/` -- feature module with list + edit + upload components per the OLD child-scoped feature at `P:/PatientPortalOld/patientappointment-portal/src/app/components/appointment-request/appointment-joint-declarations/**`. Route `/appointments/:id/joint-declarations` lazy-loaded with `authGuard + permissionGuard('CaseEvaluation.AppointmentJointDeclarations')`. Uses `FormBuilder` + `ngx-datatable` per the AppointmentAccessor pattern.
20. `joint-declarations-search/` -- admin queue at `/joint-declarations-search` (NEW naming; deliberately drops OLD's `/appointment-joint-declarations-search` because shorter URL, same page). Lazy-loaded, `permissionGuard('CaseEvaluation.AppointmentJointDeclarations.Approve')`. List with status filter + inline approve/reject actions.
21. Run `abp generate-proxy` AFTER the backend lands. Never hand-edit `angular/src/app/proxy/appointment-joint-declarations/*`.

**Domain events + email fan-out**:

22. A `ILocalEventHandler<JointDeclarationReviewedEto>` registered in a future email-integration project (owned by `email-sender-consumer` brief, CC-01). This brief ships the event + the templates; subscriber lands with CC-01.

Reference implementations to follow verbatim:

- Entity + Manager + CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentAccessors/` (sibling tenant-scoped entity with its own manager).
- AppService + permissions: `src/HealthcareSupport.CaseEvaluation.Application/AppointmentEmployerDetails/AppointmentEmployerDetailsAppService.cs` (sibling nested AppService with one controller).
- Controller: `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs` (canonical manual controller).
- Angular feature: `angular/src/app/appointments/appointment/**` (manual-controller Angular feature with list + modal + routing).
- Mapperly: `CaseEvaluationApplicationMappers.cs` -- `AppointmentToAppointmentDtoMappers` pattern.

## Why this solution beats the alternatives

- Honours ADR-001 (Mapperly), ADR-002 (manual controllers + `[RemoteService(IsEnabled = false)]`), ADR-003 (tenant-scoped config, no host guard), ADR-004 (row-level tenancy via `IMultiTenant` + auto-filter), ADR-005 (no `ng serve`). Each layer matches the existing pattern; no architectural reversal.
- Keeps the domain explicit -- OLD splits JointDeclaration from Document; NEW preserves the split. Later if Adrian wants a unified "appointment attachment" taxonomy, collapse post-MVP; do not collapse preemptively and then re-split under load.
- Decouples the email fan-out via `ILocalEventBus` so this capability can ship BEFORE the `email-sender-consumer` brief (CC-01) wires a real sender -- the event publishes into a zero-subscriber fan-out in the meantime. Same pattern as `appointment-state-machine`.
- HIPAA posture by default -- file blob encrypted-at-rest via ABP BlobStoring (via `blob-storage-provider` brief), audit fields free via `FullAuditedAggregateRoot`, access control via row-level `IMultiTenant` + method-level `[Authorize(...)]`. No bespoke security code added.

## Effort (sanity-check vs inventory estimate)

Inventory says M (~3 days) per [track 02 G2-14 row](../../gap-analysis/02-domain-entities-services.md) and 2-3 days per [track 03 03-G06 row](../../gap-analysis/03-application-services-dtos.md). Analysis confirms M (~3 days), broken down:

- 0.5 day: Domain.Shared + Domain (entity + manager + blob container + CLAUDE.md + repo interface).
- 0.5 day: Application.Contracts (DTOs + two IAppService interfaces + permission constants + permission definition registration).
- 0.5 day: Application (two AppService impls + Mapperly partials + event publish).
- 0.5 day: EFCore (OnModelCreating + migration + repository impl).
- 0.5 day: HttpApi (two controller files + route config + Swagger docstrings + multipart handling).
- 0.5 day: Angular (run `abp generate-proxy` + two feature folders + routing + lazy-loading + permission guards).
- Excluded: email template authoring (owned by `email-sender-consumer` brief) + test coverage for the new entity (owned by `new-qual-01-critical-path-test-coverage` brief).

## Dependencies

- Blocks:
  - [scheduler-notifications](scheduler-notifications.md) (G2-11) -- one of the 9 scheduled notification jobs is "joint-declaration-reminder" (notifies external users of pending JDs approaching DueDate). Needs the entity + status enum to exist.
- Blocked by:
  - [blob-storage-provider](blob-storage-provider.md) (CC-04) -- Q17 pending Adrian's DB-BLOB-vs-S3 choice. This brief writes `IBlobContainer<AppointmentJointDeclarationContainer>` which is provider-agnostic, so implementation can proceed before Q17 resolves; the provider selection is a module-level config swap. HARD blocker only if Adrian chooses S3 AND the S3 creds are not yet provisioned -- then the PR lands with DB-BLOB fallback (`BlobStoringDatabaseModule` is already wired) and the provider swaps in the `blob-storage-provider` PR.
  - [email-sender-consumer](email-sender-consumer.md) (CC-01) -- current `IEmailSender` is `NullEmailSender` in DEBUG. This brief publishes `JointDeclarationReviewedEto` into a zero-subscriber fan-out; the subscriber ships with CC-01. HARD blocker only if approve/reject emails are required on day 1 -- they are (per OLD's workflow and [03-G06 row](../../gap-analysis/03-application-services-dtos.md)). Sequence: land CC-01 first so this brief's first deploy actually mails.
  - [attorney-defense-patient-separation](attorney-defense-patient-separation.md) (DB-05 + DB-06 + G2-09) -- Q1 + Q2 pending. Soft dependency: OLD's JointDeclaration is signed by plaintiff + defense attorneys, so the "who submitted" semantics are role-contextual. NEW's `CreatorId` (from `FullAuditedAggregateRoot`) is the authenticated user -- sufficient if the attorney entities resolve to `IdentityUser` rows. Not a hard blocker; the entity schema has no attorney FK.
- Blocked by open question: none directly. Q17 is architecture-level (provider selection) and resolves in a separate brief's scope.

## Risk and rollback

- Blast radius: Low-Medium. The feature is purely additive: one new table, one new permission group, one new Angular feature module, two new controllers. No modification to existing entities. The `Appointment` entity gains no new FK (the inverse navigation is optional and does not force a schema change on `Appointments` beyond an index). Multi-tenant filter is automatic via `IMultiTenant`. PHI exposure is bounded to the file blob (encrypted) + `ReviewNotes` column (free-text, never logged per HIPAA rule); no patient names or DOBs enter the schema.
- Rollback: revert the PR. Migration is additive -- `dotnet ef database update <previous-migration>` drops `AppAppointmentJointDeclarations` table. No data to preserve because the table is empty until the first approve/reject. If reviews have occurred, export the rows first (CSV) before revert, then replay after the fix-forward PR lands. Feature-flag at the AppService layer (`if (!_settings.GetOrNull("EnableJointDeclarations")) { throw new NotImplementedException(); }`) is a follow-on hedge; not required for first land.

## Open sub-questions surfaced by research

- **Q-JD-a (carry-over from OLD business rule):** OLD's `UpdateValidation` at `AppointmentJointDeclarationDomain.cs:110` rejects uploads when `appointment.AppointmentTypeId == (int)AppointmentType.PQME` with the message "Appointment type is not valid. Please upload appropriate document." Port as-is (PQME does not accept joint declarations), or relax (all appointment types accept joint declarations)? Default: port as-is; reason: business rules should not be silently dropped. Needs Adrian confirmation before this brief lands.
- **Q-JD-b (consolidation with AppointmentDocument):** The `appointment-documents` brief introduces a polymorphic `DocumentType` discriminator (`Packet`, `Packet_Attorney`, `Packet_ClaimExaminer`, etc.). Should `JointDeclaration` be one more value in that enum post-MVP, so the two features collapse into one entity with two workflows? Non-blocking for MVP; revisit after both ship.
- **Q-JD-c (file-type restriction):** OLD allows any extension. NEW should allow-list PDF + DOC + DOCX per HIPAA-aligned practice of restricting executable uploads. Confirm whitelist; default: `{".pdf", ".doc", ".docx"}`. Enforced in `AppointmentJointDeclarationManager.CreateAsync` via content-type check.
- **Q-JD-d (reviewer-user authorization):** `ApproveAsync` / `RejectAsync` require `CaseEvaluation.AppointmentJointDeclarations.Approve`. Should this permission be granted only to internal roles (Admin, ClinicStaff, StaffSupervisor) or also to defense/applicant attorneys? Default: internal-only, matching OLD's `vInternalUser` check in `AppointmentJointDeclarationDomain.cs:129`. Needs confirmation once the internal-role seed decisions (Q21, Q22) land.
