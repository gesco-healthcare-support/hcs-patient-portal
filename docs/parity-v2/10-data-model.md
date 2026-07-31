# 10. Data model -- OLD vs NEW structural parity

## Coverage

Scope: a holistic schema/entity diff. This audit answers one question
per OLD table -- does the DATA needed to support OLD behavior have a
home in NEW? Feature BEHAVIOR is covered by the other nine auditors;
here we only check that the columns exist somewhere to hold what OLD
stored. Structural swaps that preserve outcome (int PK -> Guid PK,
lookup tables -> enums, SQL views -> EF projections, stored procs ->
LINQ, custom audit tables -> ABP `[Audited]`, two normalized tables ->
one entity with a discriminator flag, adding `TenantId`) are treated as
EQUIVALENT, not gaps.

OLD anchor: `PatientAppointment.DbEntities\Models\*.cs` -- 65 base
(non-`v*`) model files were read in full, plus the 9 `Enums\*.cs`, the
16 `ExtendedModels\*.cs` partial classes (`[NotMapped]` view-model
helpers, NOT columns), and the Data Dictionary table-of-contents
(`Documents_and_Diagrams\ER Digram\Data Dictionary (Table).pdf`) to
confirm the table census. The `~76 v*` files are SQL read-models
(views) -- confirmed read-only projections, counted once as Equivalent.

NEW anchor: `src\...\Domain\<feature>\*.cs` entities;
`EntityFrameworkCore\CaseEvaluationDbContext.cs` +
`CaseEvaluationTenantDbContext.cs` DbSet registrations;
`Domain.Shared\Enums\*.cs` (the seven enums that absorbed OLD lookup
tables) and `Domain.Shared\ExternalSignups\ExternalUserType.cs`.

OLD base-table census (65 model files): excludes `AppointmentRequestReport.cs`
(a derived `: Appointment` projection class, not a table) and the
`v*` views. The Data Dictionary also lists `CacheKeys` and
`TempAppointmentInjuryDetails` -- infra/staging artifacts with no model
class; both are out of scope (transient cache + ETL temp table).

### Structural translation conventions applied (NOT gaps)

- **Int identity PK -> `Guid` PK** on every entity. Expected.
- **Lookup tables -> enums.** OLD's `AppointmentStatuses`,
  `DocumentStatuses`, `AccessType`, `BookingStatus`, `RequestStatus`,
  `Gender`, `PhoneNumberType`, `CustomFieldType` lookup rows became
  `Domain.Shared\Enums\*`. (Note: NEW *also* keeps `AppointmentStatus`
  and `DocumentStatus`... in two forms -- see Equivalent notes.)
- **SQL views (`v*`) -> EF `WithNavigationProperties` projections /
  `GetListWithNavigationPropertiesAsync`.** One Equivalent line.
- **Stored procs -> LINQ in repositories / domain services.** One
  Equivalent line.
- **Custom audit tables (`AuditRecords`, `AuditRecordDetails`,
  `AuditRequests`, `AppointmentChangeLogs`) -> ABP `[Audited]` +
  `AbpAuditLogs` / entity change tracking.** Equivalent (with one caveat
  -- see G-10-05).
- **`CreatedById` / `CreatedDate` / `ModifiedById` / `ModifiedDate` ->
  ABP `FullAuditedAggregateRoot` (`CreatorId`, `CreationTime`,
  `LastModifierId`, `LastModificationTime`, `IsDeleted`).** Expected on
  every entity.
- **Two sibling tables -> one entity + bool discriminator.** OLD
  `AppointmentDocument` + `AppointmentNewDocument` +
  `AppointmentJointDeclaration` collapsed into NEW `AppointmentDocument`
  with `IsAdHoc` + `IsJointDeclaration` flags. Expected.
- **Attorney free-text columns -> master entity + appointment-link
  entity.** OLD `AppointmentPatientAttorney` / `AppointmentDefenseAttorney`
  (flat tables on the appointment) became NEW `ApplicantAttorney` /
  `DefenseAttorney` masters + `AppointmentApplicantAttorney` /
  `AppointmentDefenseAttorney` link rows. Outcome-equivalent (the data
  has a home); see G-10-06 for the one column that moved meaning.
- **DOCX/AWS file paths -> blob-store pointers.** OLD's
  `DocumentAwsFilePath` / `DocumentFilePath` / `AttachmentLink` became
  NEW `BlobName` + `ContentType` (ABP `IBlobContainer`). Expected per
  the PDF-replaces-DOCX mission directive.

---

## Summary counts

| Class | Count |
|---|---|
| Missing data | 9 |
| Partial (column gaps) | 4 |
| Intent deviation | 3 |
| Equivalent (different implementation) | 14 |
| OLD-bug (do not port) | 2 |

OLD base tables: **65** model files. Status split: **Present 26** ->
NEW entities; **Replaced 17** (enums + views + stored-proc read-models +
ABP audit/identity/i18n infra); **Absent 13** (data-missing); plus
9 entity/ExtendedModel/derived classes that are not tables.

---

## Entity / table map

Status legend: Present = NEW entity holds the data; Present-with-column-gaps
= entity exists but a behaviorally-meaningful column is missing;
Replaced-by-enum / Replaced-by-query / Replaced-by-infra = outcome-
equivalent structural swap; Absent = no NEW home for the data.

| OLD table | NEW entity / target | Status | Notes |
|---|---|---|---|
| `spm.Appointments` | `Appointments.Appointment` | Present | `OriginalAppointmentId`, `IsPatientAlreadyExist`, `PanelNumber`, reschedule/cancel/reject `*ById` all carried. `DoctorAvailabilityId` now non-null FK. `PrimaryResponsibleUserId` -> `PrimaryResponsibleUserId`. Adds `IsBeyondLimit`, denormalized `*Email` columns. |
| `spm.AppointmentInjuryDetails` | `AppointmentInjuryDetails.AppointmentInjuryDetail` | Present | `BodyParts` (string) -> `BodyPartsSummary`. FK to `WcabOffices` kept. |
| `spm.AppointmentInjuryBodyPartDetails` | `AppointmentBodyParts.AppointmentBodyPart` | Present | `BodyPartDescription` kept; parent FK kept. |
| `spm.AppointmentPrimaryInsurance` | `AppointmentPrimaryInsurances.AppointmentPrimaryInsurance` | Intent deviation | `InsuranceNumber` renamed `Suite` (Issue 2.3) -- meaning re-interpreted. See G-10-12. |
| `spm.AppointmentClaimExaminers` | `AppointmentClaimExaminers.AppointmentClaimExaminer` | Intent deviation | `ClaimExaminerNumber` renamed `Suite` (same fix). `ModifiedById`/`ModifiedById TypeName=date` OLD-bug not ported. See G-10-12, G-10-OLDBUG-1. |
| `spm.AppointmentPatientAttorneys` | `ApplicantAttorneys.ApplicantAttorney` + `AppointmentApplicantAttorneys.AppointmentApplicantAttorney` | Present | Flat table -> master + link. All address/firm/email columns carried on master. "Patient" attorney renamed "Applicant" attorney (WC vocabulary). |
| `spm.AppointmentDefenseAttorneys` | `DefenseAttorneys.DefenseAttorney` + `AppointmentDefenseAttorneys.AppointmentDefenseAttorney` | Present | Same master+link pattern. |
| `spm.AppointmentEmployerDetails` | `AppointmentEmployerDetails.AppointmentEmployerDetail` | Present | All columns carried (`ZipCode` <- OLD `Zip`). |
| `spm.AppointmentAccessors` | `AppointmentAccessors.AppointmentAccessor` | Partial | `FirstName`/`LastName`/`EmailId` dropped; accessor is now an `IdentityUserId` FK. See G-10-08. |
| `spm.AppointmentDocuments` | `AppointmentDocuments.AppointmentDocument` | Present | Unified table; `UserType`, `OtherDocumentTypeName`, `DocumentPackageId` FK dropped (see G-10-09, G-10-10). |
| `spm.AppointmentNewDocuments` | `AppointmentDocuments.AppointmentDocument` (`IsAdHoc=true`) | Present | Collapsed into discriminator flag. |
| `spm.AppointmentJointDeclarations` | `AppointmentDocuments.AppointmentDocument` (`IsJointDeclaration=true`) | Present | Collapsed into discriminator flag. `DocumentPackageId` FK on OLD JDF dropped. |
| `spm.AppointmentChangeRequests` | `AppointmentChangeRequests.AppointmentChangeRequest` | Present | `OldDoctorAvailabilityId` not carried (reschedule-chain handled via `OriginalAppointmentId` on the new appointment). `IsBeyodLimit` (OLD typo) -> `IsBeyondLimit`. |
| `spm.AppointmentChangeRequestDocuments` | `AppointmentChangeRequests.AppointmentChangeRequestDocument` | Present | File path -> blob pointer. |
| `spm.AppointmentChangeLogs` | ABP audit (`AbpEntityChanges`) | Replaced-by-infra | Field-level change log -> ABP entity-change tracking. See G-10-05 for the `IsMailSent` caveat. |
| `spm.AppointmentWorkerCompensations` | -- | Absent | See G-10-13 (OLD dead/duplicate of InjuryDetail). |
| `spm.Doctors` | `Doctors.Doctor` | Present | `FirstName`/`LastName`/`Email`/`Gender` kept. (Note: per memory "tenant IS the doctor" -- Doctor rows may be vestigial in Phase 1, but the entity holds the data.) |
| `spm.DoctorsAvailabilities` | `DoctorAvailabilities.DoctorAvailability` (+ `DoctorAvailabilityAppointmentType` join) | Present | Single `AppointmentTypeId` -> M2M join set. `DoctorId` FK dropped (single-doctor Phase 1). Adds `Capacity`. `StatusId` -> ABP soft-delete. |
| `spm.DoctorPreferredLocations` | `DoctorPreferredLocations.DoctorPreferredLocation` | Present | Composite-key M2M kept; `StatusId` -> `IsActive` + soft-delete. |
| `spm.DoctorsAppointmentTypes` | `Doctors.DoctorAppointmentType` | Present | Composite-key M2M kept. |
| `spm.Locations` | `Locations.Location` | Present | `ParkingFee`, `AppointmentTypeId`, `StateId` (`State` int -> Guid FK) carried. `City`/`ZipCode` kept. |
| `spm.WcabOffices` | `WcabOffices.WcabOffice` | Present | `WcabOfficeAbbreviation` -> `Abbreviation`. `StatusId` -> `IsActive`. |
| `spm.AppointmentTypes` | `AppointmentTypes.AppointmentType` | Partial | `ReEvalId` (self-ref re-eval link) dropped. See G-10-11. |
| `spm.AppointmentStatuses` | `Enums.AppointmentStatusType` (+ vestigial `AppointmentStatuses.AppointmentStatus` entity) | Replaced-by-enum | Status now an enum on Appointment. The name-only `AppointmentStatus` entity is a redundant lookup leftover (see Equivalent E-13). |
| `spm.DocumentStatuses` | `Enums.DocumentStatus` (on `AppointmentDocument.Status`) | Replaced-by-enum | -- |
| `spm.AppointmentLanguages` | `AppointmentLanguages.AppointmentLanguage` | Present | Name-only lookup; kept as table (FK from Patient). |
| `spm.AppointmentDocumentTypes` | -- | Absent | Document-type lookup dropped; NEW uses free-text `DocumentName`. See G-10-09. |
| `spm.Patients` | `Patients.Patient` | Present | All columns carried (`CellPhoneNumner` OLD typo -> `CellPhoneNumber`; `ReferredBy` -> `RefferedBy` NEW typo). Adds `IdentityUserId`. SSN kept. |
| `dbo.Users` | ABP `IdentityUser` (+ `Doctors`/attorney masters for non-login profiles) | Replaced-by-infra | Custom user table -> ABP Identity. `Password`/`Salt`/`VerificationCode` -> OpenIddict/Identity. `SignatureAWSFilePath`, `FirmName`/`FirmEmail`, `ApplicationTimeZoneId`, `IsAccessor` -- see G-10-07. |
| `dbo.Roles` + `RolePermissions` + `RoleUserTypes` + `RoleAppointmentTypes` | ABP `AbpRoles` + ABP permission system + `Enums.AccessType` | Replaced-by-infra | RBAC -> ABP permissions. `RoleAppointmentTypes` (role-to-type visibility) -- confirm covered by Auth/RBAC auditor. |
| `spm.SystemParameters` | `SystemParameters.SystemParameter` | Present | All 14 numeric gates + `IsCustomField` + `CcEmailIds` carried. |
| `spm.Templates` | `NotificationTemplates.NotificationTemplate` | Present | Int `TemplateCode` -> string code. `BodyEmail`/`BodySms`/`Subject`/`Description` kept. |
| `spm.TemplateTypes` | `NotificationTemplates.NotificationTemplateType` | Present | -- |
| `spm.Documents` | `Documents.Document` | Present | Master template catalog. File path -> blob. |
| `spm.DocumentPackages` | `PackageDetails.DocumentPackage` | Present | M2M PackageDetail<->Document, composite key kept. |
| `spm.PackageDetails` | `PackageDetails.PackageDetail` | Present | `PackageName`, `AppointmentTypeId` kept. |
| `spm.CustomFields` | `CustomFields.CustomField` | Present | `FieldLabel`/`DisplayOrder`/`FieldType`/`FieldLength`/`MultipleValues`/`DefaultValue`/`IsMandatory`/`AppointmentTypeId` all carried. `AvailableTypeId` (always Appointment=11) dropped -- vestigial. |
| `spm.CustomFieldsValues` | `CustomFields.CustomFieldValue` | Present | Polymorphic `ReferenceId` -> explicit `AppointmentId` FK (intentional correctness fix). |
| `spm.Notes` | -- | Absent | Appointment notes/comments thread. See G-10-01. |
| `spm.UserQueries` | -- | Absent | "Contact us" / help-query messages. See G-10-02. |
| `spm.States` | `States.State` | Present | Name-only lookup, kept as table (FK from many entities). |
| `dbo.Countries` | -- | Absent | Country lookup (US-only app). See G-10-03. |
| `spm.City` | -- | Absent | City lookup; NEW uses free-text `City` strings. See G-10-04. |
| `dbo.ApplicationTimeZones` | -- (ABP clock / per-user TZ setting) | Absent | TZ lookup. See G-10-07. |
| `dbo.GlobalSettings` | ABP Settings + `SystemParameters` | Replaced-by-infra | `RecordLock`/`LockDuration`/`TwoFactorAuthentication`/`SocialAuth`/`AutoTranslation`/`RequestLogging` -> ABP setting providers. See G-10-OLDBUG-2 for the `LockRecords` companion. |
| `dbo.LockRecords` | -- | Absent | Pessimistic record-lock rows. See G-10-OLDBUG-2 / Equivalent. |
| `dbo.ApplicationUserTokens` | OpenIddict token store | Replaced-by-infra | JWT/session tokens -> OpenIddict. |
| `dbo.ApplicationModules` + `ModuleMasters` + `ModuleContents` | ABP localization + permission definitions | Replaced-by-infra | Module/menu/i18n metadata. |
| `dbo.ApplicationObjects` + `ApplicationObjectTypes` | `Domain.Shared\Enums\*` | Replaced-by-enum | The central int-keyed "object" table that all OLD enums (`Status`, `Gender`, `AccessType`, etc.) referenced by id. Dissolved into typed enums. |
| `dbo.Languages` + `LanguageContents` + `ConfigurationContents` | ABP localization JSON | Replaced-by-infra | i18n content. |
| `dbo.AuditRecords` + `AuditRecordDetails` + `AuditRequests` | ABP `AbpAuditLogs` + `AbpEntityChanges` | Replaced-by-infra | Custom audit -> ABP `[Audited]`. |
| `dbo.ApplicationExceptionLogs` + `RequestLogs` | ABP audit logging / Serilog | Replaced-by-infra | Exception + request logs. |
| `dbo.SMTPConfigurations` | `appsettings.secrets.json` + ABP `Abp.Mailing` settings | Replaced-by-infra | SMTP creds out of DB into config (security improvement). |
| `~76 v* views` | EF `*WithNavigationProperties` projections | Replaced-by-query | Read-models. One Equivalent line. |
| stored procs (`sp*`, scheduler procs) | LINQ in repos / domain services | Replaced-by-query | One Equivalent line. |

---

## Data gaps

### G-10-01 -- `Notes` table absent (appointment notes thread)

- **Class:** Missing data
- **OLD:** `spm.Notes` (`Models\Note.cs`). Columns: `Comments`,
  `AppointmentId` FK, `ParentNoteId`, `EditNoteId`, `IsLatest`,
  `StatusId`, full audit.
- **NEW:** No entity. `Appointment` had an `ICollection<Note>` in OLD;
  NEW `Appointment` has no notes collection and there is no `Note`
  entity or DbSet anywhere in `src`.
- **What it is:** A threaded comment log attached to an appointment.
  `ParentNoteId` + `EditNoteId` + `IsLatest` implement an edit-history
  chain (each edit creates a new row, old row keeps history, latest is
  flagged).
- **Why it existed:** Internal staff record running commentary on a
  case (scheduling notes, special instructions, follow-ups) visible on
  the appointment detail view.
- **What it does + user impact:** Without it, staff have nowhere to log
  per-appointment notes. `Appointment.InternalUserComments` (max 250)
  exists but is a single overwrite field, not a thread/history.
- **Plain-English:** OLD let staff add multiple dated notes to a case
  and keep a history of edits. NEW only has one short comment box.
- **Keep in NEW?** Yes if the Dashboard/Notes auditor confirms staff
  use it. Cross-ref: Audit 09 (Dashboard / notes / query). Likely a
  real gap.

### G-10-02 -- `UserQueries` table absent (help / contact messages)

- **Class:** Missing data
- **OLD:** `spm.UserQueries` (`Models\UserQuery.cs`). Columns:
  `Message` (max 500), `UserId` FK, full audit.
- **NEW:** No entity / DbSet.
- **What it is:** Free-text "contact us / I have a question" messages
  submitted by a logged-in user.
- **Why it existed:** A lightweight in-app help channel -- user posts a
  question, staff read the queue.
- **What it does + user impact:** Without it, external users have no
  in-app way to send a question; they fall back to phone/email.
- **Plain-English:** OLD had a "send us a question" box. NEW does not.
- **Keep in NEW?** Confirm with Audit 09. If the feature was used,
  port; otherwise drop (low-value). Likely low priority.

### G-10-03 -- `Countries` table absent

- **Class:** Missing data (low impact)
- **OLD:** `dbo.Countries` (`Models\Country.cs`). Columns: `CountryName`,
  `CountryCode`, `CurrencyFormat`, `DateFormat`, `PhoneFormat`,
  `PostalCodeFormat`, `DefaultLanguageId`, `Active`.
- **NEW:** No entity. `City` referenced `CountryId`; both gone.
- **What it is:** A country lookup with per-country formatting metadata
  (date/phone/postal masks).
- **Why it existed:** OLD was built as a generic multi-country product
  (the formatting columns suggest i18n ambition); the live app is
  US-only.
- **What it does + user impact:** None visible -- the app only ever
  used USA. Formatting masks are now handled by Angular locale.
- **Plain-English:** Leftover from a "support any country" design. The
  app only serves the US, so nothing is lost.
- **Keep in NEW?** No. US-only; Angular `en-US` locale covers
  formatting.

### G-10-04 -- `City` table absent (free-text city in NEW)

- **Class:** Intent deviation
- **OLD:** `spm.City` (`Models\City.cs`). Columns: `CityName`,
  `CityCode`, `StateId`, `CountryId` FK, `StatusId`.
- **NEW:** No `City` entity. Every address-bearing NEW entity
  (`Patient`, `Location`, `WcabOffice`, attorney masters, employer,
  claim-examiner, insurance) stores `City` as a free-text `string`.
- **What it is:** A normalized city reference list.
- **Why it existed:** OLD normalized cities into a lookup (a
  cascading Country->State->City picker).
- **What it does + user impact:** OLD's address forms used a
  city dropdown; NEW uses a free-text box. Behavior changes from
  picklist to typed entry. No data loss (city is still captured) but
  validation/consistency differs.
- **Plain-English:** OLD picked the city from a list; NEW lets you type
  it. The city is still saved either way.
- **Keep in NEW?** Free-text is acceptable and simpler. Flag only if a
  feature auditor finds a screen that depended on city-by-id.

### G-10-05 -- `AppointmentChangeLogs.IsMailSent` flag has no NEW home

- **Class:** Partial (column gap)
- **OLD:** `spm.AppointmentChangeLogs` (`Models\AppointmentChangeLog.cs`).
  Field-level diff rows (`FieldName`, `OldValue`, `NewValue`,
  `TableName`, `ChangedById`, `ChangedDate`) PLUS two behavior flags:
  `IsInternalUserUpdate` and `IsMailSent`.
- **NEW:** Field-level change history is replaced by ABP entity-change
  tracking (`AbpEntityChanges` / `AbpEntityPropertyChanges`) -- the
  diff data itself IS covered (Equivalent). But ABP's audit log has no
  `IsMailSent` column.
- **What it is:** `IsMailSent` marked whether a notification email had
  already been dispatched for a given field change, so the change-mail
  job would not double-send.
- **Why it existed:** Idempotency guard for the "email on appointment
  change" path -- prevents re-notifying on the same diff.
- **What it does + user impact:** If NEW drives change-notification
  emails off the audit log, it needs its own "already-sent" bookkeeping;
  ABP audit rows are immutable and have no such flag.
- **Plain-English:** OLD ticked a box on each logged change once its
  "we changed your appointment" email went out, so it never sent twice.
  NEW's audit trail has no such tick.
- **Keep in NEW?** Only the FLAG behavior matters, and only IF a
  change-notification feature exists. Cross-ref Audit 04 (emails). If
  NEW's email pipeline has its own dedup, this is a non-issue.

### G-10-06 -- attorney name/email persistence (resolved, note for completeness)

- **Class:** Equivalent (different implementation) -- documented here
  because the column physically moved.
- **OLD:** `AppointmentPatientAttorney` / `AppointmentDefenseAttorney`
  stored `AttorneyName` + `AttorneyEmail` flat on the appointment-link
  row.
- **NEW:** `ApplicantAttorney` / `DefenseAttorney` master rows hold
  `FirstName`/`LastName`/`Email` (BUG-042 split), and the link rows
  (`AppointmentApplicantAttorney`) hold only the FK + optional
  `IdentityUserId`. `Appointment` ALSO denormalizes
  `ApplicantAttorneyEmail` / `DefenseAttorneyEmail` for fast send.
- **Verdict:** No data lost; name/email have a home on the master. Not
  a gap. Listed so the column move is traceable.

### G-10-07 -- `Users` profile columns without a clear NEW home

- **Class:** Partial (column gaps)
- **OLD:** `dbo.Users` (`Models\User.cs`). Beyond auth (which ABP
  Identity replaces), OLD stored profile columns: `SignatureAWSFilePath`
  (e-signature image), `FirmName` + `FirmEmail` (for attorney users),
  `ApplicationTimeZoneId`, `IsAccessor`, `Address`/`City`/`State`/`Zip`,
  `DateOfBirth`, `PhoneNumber`.
- **NEW:** Auth/identity -> ABP `IdentityUser`. Attorney firm fields
  -> `ApplicantAttorney`/`DefenseAttorney` masters (covered). But:
  - `SignatureAWSFilePath` -- no NEW column anywhere. See sub-gap.
  - `ApplicationTimeZoneId` -- see G-10-OQ (per-user TZ).
  - `IsAccessor` -- behaviorally replaced by `AppointmentAccessor`
    rows (Equivalent).
- **What it is / why:** `SignatureAWSFilePath` held the user's
  e-signature used when stamping generated documents (esp. attorney
  Joint Declaration / cover pages).
- **What it does + user impact:** If generated PDFs in OLD embedded a
  user signature image, NEW packet generation has nowhere to pull it
  from. Cross-ref Audit 03 (documents & packets).
- **Plain-English:** OLD stored each user's saved signature image to
  drop into generated paperwork. NEW has no signature storage.
- **Keep in NEW?** Confirm with Audit 03 whether packets embed a
  signature. If yes, this is a real gap (add a signature blob to the
  identity profile). If packets never used it, drop.

### G-10-08 -- `AppointmentAccessor` name/email columns dropped

- **Class:** Partial (column gap)
- **OLD:** `spm.AppointmentAccessors` (`Models\AppointmentAccessor.cs`)
  stored `FirstName`, `LastName`, `EmailId` directly on the row, plus
  `AccessTypeId`, `RoleId` FK, `AppointmentId`.
- **NEW:** `AppointmentAccessors.AppointmentAccessor` keeps
  `AccessTypeId` + `AppointmentId` but replaces the three name/email
  columns with a single `IdentityUserId` FK; `RoleId` dropped.
- **What it is:** An accessor = an extra person granted View/Edit on a
  specific appointment.
- **Why it mattered:** OLD let you grant access by typing a
  name+email even for someone who might not be a full user row; the
  stored name/email rendered on the access list.
- **What it does + user impact:** NEW requires the accessor to be an
  existing `IdentityUser` (FK). If OLD allowed granting access to a
  not-yet-registered email, that path changes. Likely intentional
  (accessors must be invited users now -- ties to the `Invitation`
  feature).
- **Plain-English:** OLD could share a case with any name+email; NEW
  shares only with registered users. Probably deliberate.
- **Keep in NEW?** Confirm with Audit 06 (Auth/RBAC) that accessor =
  registered user is the intended model. If so, not a gap.

### G-10-09 -- `AppointmentDocumentTypes` lookup absent

- **Class:** Missing data
- **OLD:** `spm.AppointmentDocumentTypes` (`Models\AppointmentDocumentType.cs`):
  `DocumentTypeName` + `StatusId`. Referenced by `AppointmentDocument`
  and `AppointmentNewDocument` via nullable `AppointmentDocumentTypeId`,
  with `OtherDocumentTypeName` as the free-text "Other" overflow.
- **NEW:** No document-type lookup; `AppointmentDocument` uses a
  free-text `DocumentName` only (the entity's own XML doc-comment lists
  "AppointmentDocumentType lookup" as an explicit MVP cut).
- **What it is:** A managed list of document categories (e.g.
  "Medical Records", "Imaging", "Other") chosen at upload time.
- **Why it existed:** Lets staff filter/group uploaded docs by type and
  drives some packet logic.
- **What it does + user impact:** NEW uploaders type a free name; no
  categorization, no type filter. `OtherDocumentTypeName` also gone.
- **Plain-English:** OLD made you pick a document category from a list;
  NEW just lets you name the file. Categorization is lost.
- **Keep in NEW?** Confirm with Audit 03. If staff filter docs by type,
  port the lookup; otherwise free-text is acceptable.

### G-10-10 -- `AppointmentDocument.DocumentPackageId` FK dropped

- **Class:** Partial (column gap)
- **OLD:** `AppointmentDocument` and `AppointmentJointDeclaration` both
  carried a `DocumentPackageId` (which package-template doc a given
  upload satisfies). `AppointmentDocument.UserType` also recorded which
  stakeholder type uploaded.
- **NEW:** `AppointmentDocument` has no `DocumentPackageId` link and no
  `UserType`. Package association at MVP is via the `VerificationCode` +
  `IsAdHoc`/`IsJointDeclaration` flags + `Status` queue, not a hard FK
  to the package-detail row.
- **What it is:** The link from an uploaded file back to the specific
  required package document it fulfills.
- **What it does + user impact:** Without the FK, NEW cannot
  deterministically mark "this upload satisfies package item X" via the
  data model; it infers via the queued-row pattern. Cross-ref Audit 03.
- **Plain-English:** OLD knew exactly which required document each
  upload was answering; NEW tracks it more loosely.
- **Keep in NEW?** Confirm with Audit 03 whether the looser model
  preserves the "which required doc is still outstanding" behavior.

### G-10-11 -- `AppointmentType.ReEvalId` dropped

- **Class:** Missing data
- **OLD:** `spm.AppointmentTypes.ReEvalId` (nullable self-reference) --
  links a base type (e.g. "PQME") to its re-evaluation variant
  ("PQME-REVAL").
- **NEW:** `AppointmentTypes.AppointmentType` has only `Name` +
  `Description`; no `ReEvalId`.
- **What it is:** A pointer pairing an initial-eval type with its
  follow-up re-eval type.
- **Why it existed:** Drives different max-time gates and form behavior
  for re-evaluations (the SystemParameter has distinct AME/PQME max
  windows; re-eval pairing tells the booker which window applies).
- **What it does + user impact:** If NEW relies on naming convention
  (e.g. string contains "REVAL") instead of the FK, behavior may match;
  if a screen offered "book a re-eval of this appointment", that link is
  gone.
- **Plain-English:** OLD formally linked each exam type to its
  follow-up exam type; NEW doesn't store that link.
- **Keep in NEW?** Confirm with Audit 01/02 (booking / re-eval flow). If
  re-eval is identified by name only, low impact; if a re-eval booking
  shortcut existed, port the link.

### G-10-12 -- `InsuranceNumber` / `ClaimExaminerNumber` re-meant as `Suite`

- **Class:** Intent deviation (resolved decision, flagged for the record)
- **OLD:** `AppointmentPrimaryInsurance.InsuranceNumber` (255) and
  `AppointmentClaimExaminer.ClaimExaminerNumber` (255).
- **NEW:** Both renamed to `Suite` (Issue 2.3, 2026-05-12) on the basis
  that the on-screen label was always "STE" (USPS Suite) and the column
  name was a misnomer.
- **What it does + user impact:** Same column, re-interpreted meaning.
  If any OLD report/export column header said "Insurance Number" /
  "Claim Examiner Number", NEW now labels it "Suite". Cross-ref
  Audit 08 (reporting/export) to confirm no report relied on the old
  semantic.
- **Plain-English:** A field OLD mislabeled "Insurance Number" was
  actually the suite/address line; NEW calls it what it is.
- **Keep in NEW?** Decision already made (rename kept). Verify the
  report-export auditor agrees no report header regressed.

### G-10-13 -- `AppointmentWorkerCompensations` absent (OLD redundant)

- **Class:** Missing data (low confidence -- likely dead)
- **OLD:** `spm.AppointmentWorkerCompensations`
  (`Models\AppointmentWorkerCompensation.cs`): `ClaimNumber`,
  `DateOfInjury`, `WcabAdj`, `WcabOfficeId`, `AppointmentId`.
- **NEW:** No entity.
- **What it is:** A near-exact duplicate of the data already on
  `AppointmentInjuryDetail` (claim number + date of injury + WCAB adj +
  WCAB office).
- **Why it existed:** Appears to be a superseded/parallel modeling of
  the same injury-claim data; `Appointment` has NO navigation
  collection to it (unlike `AppointmentInjuryDetails`), suggesting it
  was orphaned.
- **What it does + user impact:** Almost certainly none -- the live
  injury data flows through `AppointmentInjuryDetails`, which IS ported.
- **Plain-English:** A leftover duplicate of the injury/claim info. The
  real copy is already in NEW.
- **Keep in NEW?** No, unless a feature auditor finds code that wrote
  to this table. Treat as superseded.

---

## Equivalent (different implementation)

These are outcome-equivalent structural swaps -- explicitly NOT gaps.

- **E-01 -- Int identity PKs -> `Guid` PKs.** Every entity. Adds
  multi-tenant-safe global uniqueness.
- **E-02 -- `~76 v* views -> EF query projections.** All `v*` files
  (e.g. `vAppointmentDetail`, `vPatient`, `vDoctorDetail`, every
  `*LookUp` and `*Record`) are SQL read-models; NEW reproduces them as
  `*WithNavigationProperties` classes +
  `GetListWithNavigationPropertiesAsync` repository methods. One line,
  not 76 gaps.
- **E-03 -- Stored procedures -> LINQ.** OLD `sp*` (incl. the 9
  scheduler notification procs) replaced by repository LINQ / domain
  services. One line.
- **E-04 -- Custom audit (`AuditRecords` + `AuditRecordDetails` +
  `AuditRequests`) -> ABP `[Audited]` (`AbpAuditLogs`,
  `AbpEntityChanges`).** Field-level diff data preserved by ABP entity
  change tracking. (Caveat: `IsMailSent` flag -- G-10-05.)
- **E-05 -- `AppointmentChangeLogs` -> ABP entity-change tracking.**
  Same as E-04 for the appointment-specific diff log.
- **E-06 -- `CreatedById`/`CreatedDate`/`ModifiedById`/`ModifiedDate`
  + `StatusId`(=Delete) -> ABP `FullAuditedAggregateRoot`** (`CreatorId`,
  `CreationTime`, `LastModifierId`, `LastModificationTime`,
  `IsDeleted` soft-delete). On every entity.
- **E-07 -- `ApplicationObjects` + lookup tables -> typed enums.**
  `AppointmentStatuses`, `DocumentStatuses`, `AccessType`,
  `BookingStatus`, `RequestStatus`, `Gender`, `PhoneNumberType`,
  `CustomFieldType` -> `Domain.Shared\Enums\*`. (NEW preserves OLD's
  exact int values for `AccessType`, `BookingStatus`,
  `CustomFieldType`, `PhoneNumberType`, `RequestStatusType`; remaps
  `Gender` to 1/2/3 and `AppointmentStatusType` to 1-13.)
- **E-08 -- `dbo.Users` auth columns -> ABP Identity + OpenIddict.**
  `Password`/`Salt`/`VerificationCode`/`IsVerified`/`IsActive` ->
  `AbpUsers` + OpenIddict.
- **E-09 -- `ApplicationUserTokens` -> OpenIddict token store.**
- **E-10 -- RBAC (`Roles`/`RolePermissions`/`RoleUserTypes`) -> ABP
  permission system + roles.** Cross-ref Audit 06.
- **E-11 -- i18n (`Languages`/`LanguageContents`/`ModuleContents`/
  `ConfigurationContents`) -> ABP localization JSON.**
- **E-12 -- `SMTPConfigurations` -> `appsettings.secrets.json` + ABP
  mailing settings.** Security improvement (creds out of DB).
- **E-13 -- `AppointmentStatuses`/`AppointmentLanguages`/`States`
  name-only lookups.** Status is now the enum (E-07); but NEW ALSO
  keeps a name-only `AppointmentStatus` entity + DbSet and the
  `AppointmentLanguage`/`State` lookups as tables (FKs reference them).
  Equivalent -- the data has a home. The standalone `AppointmentStatus`
  table is slightly redundant alongside `AppointmentStatusType` but
  harmless.
- **E-14 -- DOCX/AWS file-path columns -> blob-store pointers.**
  `DocumentAwsFilePath`/`DocumentFilePath`/`AttachmentLink`/
  `JointDeclarationFilePath`/`SignatureAWSFilePath` -> `BlobName` +
  `ContentType` over ABP `IBlobContainer`. (Signature blob is the one
  not yet wired -- G-10-07.)
- **E-15 -- `GlobalSettings` + `LockRecords` pessimistic locking -> ABP
  concurrency (`ConcurrencyStamp` optimistic).** OLD's record-lock
  table is replaced by ABP's optimistic-concurrency stamp on aggregate
  roots. Different concurrency model, same goal (no lost updates). See
  G-10-OLDBUG-2.

---

## OLD bugs (do not port)

### G-10-OLDBUG-1 -- `AppointmentClaimExaminer.ModifiedById` typed as `date`

- **OLD:** `Models\AppointmentClaimExaminer.cs` --
  `[Column("ModifiedById",TypeName = "date")] public Nullable<DateTime> ModifiedById`.
  The `ModifiedById` column is declared as a `DateTime` with SQL type
  `date`. Same copy-paste error in
  `AppointmentWorkerCompensation.ModifiedById` (also `DateTime`/`date`).
- **Why it is a bug:** `ModifiedById` should be an int user id, not a
  date. It is a clear copy-paste slip from the adjacent `ModifiedDate`
  column.
- **NEW:** Correctly uses ABP `LastModifierId` (Guid) +
  `LastModificationTime` (DateTime). Bug not inherited. Correct.

### G-10-OLDBUG-2 -- pessimistic `LockRecords` table (design smell)

- **OLD:** `dbo.LockRecords` + `GlobalSettings.RecordLock` /
  `LockDuration` implement application-level pessimistic row locking
  (a `UserName` holds a `RecordId` until `ExpiresAt`).
- **Why flagged:** App-managed pessimistic locks are fragile (stale
  locks on crash, no DB enforcement). Not a data-loss bug, but a
  pattern not worth porting.
- **NEW:** ABP optimistic concurrency (`ConcurrencyStamp`) replaces it
  (E-15). Correct not to port the table.

---

## Open questions

1. **Notes (G-10-01) + UserQueries (G-10-02):** were these features
   actually used by HCS, or vestigial? Audit 09 owns the answer; if
   used, they are the two highest-value data gaps.
2. **User signature (`SignatureAWSFilePath`, G-10-07):** do OLD
   generated packets/JDF embed a user e-signature image? Audit 03 owns
   the answer; if yes, NEW needs a signature blob on the profile.
3. **Document type lookup (G-10-09) + package-doc FK (G-10-10):** does
   the looser NEW document model preserve OLD's "which required package
   doc is still outstanding" + "filter docs by type" behavior?
   Audit 03 owns the answer.
4. **`AppointmentType.ReEvalId` (G-10-11):** is the re-eval type pairing
   relied on for booking/max-time logic, or is it inferred from the type
   name? Audit 01/02 owns the answer.
5. **Per-user time zone (`ApplicationTimeZoneId`):** OLD stored a
   per-user TZ. NEW relies on ABP clock + a single deployment TZ
   (America/Los_Angeles). For a US-Pacific single-office Phase 1 this is
   fine; confirm no per-user TZ display behavior is expected.
6. **`AppointmentAccessor` registered-user-only (G-10-08):** confirm
   with Audit 06 that accessors must now be registered IdentityUsers
   (vs OLD's free name+email grant).
