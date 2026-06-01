# 07. Admin & master data -- OLD vs NEW behavioral parity

## Coverage

Scope: the configuration / master-data entities and their admin behavior --
CRUD field sets + business rules, slot generation, system parameters, custom
fields, notification-template editing, document/package catalogs, doctor
management M:M, and the master lookup tables. Booking/approval consumption of
these configs is deferred to areas 01/02; pure schema column-diff of NON-config
entities to area 10; auth/RBAC to area 06.

OLD anchors read (controllers): `AppointmentTypesController`, `LocationsController`,
`WcabOfficesController`, `CustomFieldsController`, `SystemParametersController`,
`TemplatesController`, `DocumentsController`, `PackageDetailsController`,
`AppointmentDocumentTypesController`, `DoctorsController`,
`DoctorsAvailabilitiesController` (domain), `DoctorPreferredLocationsController`
(domain), `DoctorsAppointmentTypesController` (domain).

OLD domains read: `AppointmentTypeDomain`, `LocationDomain`, `WcabOfficeDomain`,
`DoctorDomain`, `DoctorsAvailabilityDomain` (incl. `GenerateDoctorsAvailability`),
`DoctorPreferredLocationDomain`, `DoctorsAppointmentTypeDomain`,
`CustomFieldDomain`, `SystemParameterDomain`, `TemplateDomain`.

OLD entities read: `AppointmentType` (incl. `ReEvalId`), `AppointmentStatus`,
`AppointmentLanguage`, `Location` (incl. `ParkingFee`, `AppointmentTypeId`),
`WcabOffice`, `CustomField` (+ `MultipleValues`, `FieldTypeId`, `AvailableTypeId`),
`SystemParameter` (all 12+ fields + `vSystemParameter` extras), `Template`,
`AppointmentDocumentType`, `Document`, `PackageDetail`, `Country`, `City`,
`ApplicationTimeZone`, `GlobalSetting`, enums `CustomFieldType`, `AccessType`,
`AvailableType`, `TemplateCode`, `TemplateType`.

OLD Angular read: `system-parameter/`, `custom-field/`, `template-management/`,
`doctor-management/` (doctors edit, appointment-types, locations, availabilities).

NEW read: Domain + Application for `AppointmentTypes`, `AppointmentStatuses`,
`AppointmentLanguages`, `Locations`, `WcabOffices`, `CustomFields`,
`AppointmentTypeFieldConfigs`, `SystemParameters`, `NotificationTemplates`,
`Doctors`, `DoctorAvailabilities`, `Documents`, `PackageDetails`,
`AppointmentDocuments`, `States`; Angular `appointment-types/`,
`appointment-statuses/`, `appointment-languages/`, `locations/`,
`wcab-offices/`, `states/`, `doctors/`, `doctor-availabilities/`, and the
`proxy/` clients for the un-UI'd services.

## Summary counts

| Class | Count |
|---|---|
| Missing behavior | 5 |
| Partial behavior | 4 |
| Intent deviation | 4 |
| Equivalent (different implementation) | 9 |
| OLD-bug (do not port) | 3 |

---

## Behavioral gaps

### G-07-01 -- No admin UI for System Parameters, Custom Fields, Notification Templates, Documents, Package Details, AppointmentTypeFieldConfigs

- **Class:** Missing behavior
- **OLD:** Angular screens `system-parameter/system-parameters/edit/`
  (`/system-parameters`), `custom-field/custom-fields/{list,add,edit}/`,
  `template-management/templates/{list,add,edit,delete}/`,
  `document-management/{documents,document-packages}/`.
- **NEW:** Backend complete (`SystemParametersAppService`,
  `CustomFieldsAppService`, `NotificationTemplatesAppService`,
  `DocumentsAppService`, `PackageDetailsAppService`,
  `AppointmentTypeFieldConfigsAppService`) and proxy clients generated
  (`angular/src/app/proxy/{system-parameters,custom-fields,notification-templates,package-details,appointment-type-field-configs,documents}/`),
  but NO Angular feature folder / component / route for any of them. Confirmed:
  the only non-proxy consumer is `appointments/sections/appointment-add-custom-fields.component.ts`
  (the booking-form renderer), not an admin editor.
- **What it is:** The IT-Admin-facing configuration screens.
- **Why it existed:** OLD IT Admin tuned booking gates (lead time, durations),
  defined custom intake fields, edited every email/SMS template body, and
  managed the document/package catalog -- all from the UI.
- **What it does + user impact:** Without these screens an admin cannot change
  any system parameter, add a custom field, edit a notification template, or
  curate the document catalog at runtime. Today only the data-seed defaults are
  reachable; any change requires a DB edit or new seed. This is the single
  largest admin-parity gap.
- **Plain-English:** The settings pages exist on the server but the actual
  web pages an admin would click on were never built for six config areas.
- **Keep in NEW?** Yes -- build the missing Angular screens (LeptonX/Material
  styling is fine; field set must match OLD).

### G-07-02 -- AppointmentType.ReEvalId dropped

- **Class:** Partial behavior
- **OLD:** `AppointmentType.ReEvalId` (`Nullable<int>`), `DbEntities/Models/AppointmentType.cs:36-37`.
  Pairs a base type (PQME) with its re-eval variant (PQME RE-EVAL).
- **NEW:** `AppointmentTypes/AppointmentType.cs` has only `Name` + `Description`;
  no `ReEvalId`. `grep -ri ReEval` across NEW source returns nothing (only
  unrelated `AttyCEPacketEmailHandler` text).
- **What it is:** Self-referential pointer marking the re-evaluation counterpart
  of an appointment type.
- **Why it existed:** OLD seeded 4 types (PQME / PQME RE-EVAL / AME / AME RE-EVAL)
  with the re-eval rows linked to their base via `ReEvalId`.
- **What it does + user impact:** Drives re-eval-aware logic (max-time selection
  uses `AppointmentMaxTimePQME/AME/OTHER`; the re-eval relationship lets the app
  distinguish first vs follow-up exams). Absent in NEW, any re-eval pairing is
  lost -- re-eval types become flat, unlinked rows.
- **Plain-English:** OLD knew "this PQME re-eval belongs to that PQME"; NEW
  forgot the link.
- **Keep in NEW?** Replicate-and-flag -- confirm whether re-eval pairing is still
  required; if so, add the nullable self-FK.

### G-07-03 -- Slot generation conflict: OLD blocks whole save, NEW skips conflicts and inserts the rest

- **Class:** Intent deviation
- **OLD:** `DoctorsAvailabilityDomain.AddValidation` (`DoctorsAvailabilityDomain.cs:46-99`)
  runs four conflict checks per generated slot (contained-in
  `FromTime > x && ToTime < x`, exact-duplicate `==`, same-location overlap via
  `TimeSlotValidation`, and booked/reserved match). If ANY slot conflicts it
  returns `TimeSlotExists` / `TimeSlotBooked` and the controller returns
  `BadRequest` -- the ENTIRE batch is rejected, nothing is saved.
- **NEW:** `DoctorAvailabilitiesAppService.CreateRangeAsync` (lines 314-357) +
  `GeneratePreviewAsync` (227-312) flag conflicts per-slot via a single overlap
  predicate (`x.FromTime < slot.ToTime && x.ToTime > slot.FromTime`, scoped to
  `LocationId`), then inserts ONLY the non-conflicting slots in one transactional
  UoW and returns `{InsertedCount, SkippedConflictCount, ConflictedSlots}`.
- **What it is:** Bulk slot-generation conflict policy.
- **Why it existed:** OLD treated any overlap as a hard error so the admin
  re-picked a clean range.
- **What it does + user impact:** OLD = all-or-nothing (admin sees one error,
  must fix and resubmit the full range). NEW = partial success (clean slots land,
  collisions are reported and skipped). Different observable outcome: a range
  spanning one occupied day succeeds in NEW but fails entirely in OLD.
- **Plain-English:** If you ask to add slots for a week and Tuesday already has
  some, OLD refuses the whole week; NEW adds Mon/Wed-Fri and tells you Tuesday
  was skipped.
- **Keep in NEW?** Likely keep (better UX) -- but flag for Adrian to confirm the
  partial-insert semantics are acceptable vs strict OLD parity.

### G-07-04 -- Slot conflict detection uses generic overlap, not OLD's 4 discrete checks

- **Class:** Intent deviation
- **OLD:** Three distinct "exists" conditions feed one `TimeSlotExists` message
  plus a separate booked/reserved condition feeding `TimeSlotBooked`. Critically,
  OLD's blocking `AddValidation` (`:59,65,77`) scopes by `LocationId`, but the
  display-only `GenerateDoctorsAvailabilityByDays` overlap pass (`:408-432`)
  does NOT filter by location, producing the message "TimeSlot Already Exist in
  the System for different location."
- **NEW:** Single half-open interval overlap test (`FromTime < ToTime && ToTime
  > FromTime`), always scoped to `LocationId`; distinguishes only
  Reserved-overlap vs other-overlap in the message
  (`GenerationConflictReserved` / `GenerationConflictExists`).
- **What it is:** The overlap math classifying a generated slot as conflicting.
- **Why it existed:** OLD accreted multiple narrow predicates over time (exact
  match, containment, range overlap) rather than one interval test.
- **What it does + user impact:** Outcome is largely equivalent for true overlaps
  but edge cases differ: OLD's exact-`==` and contained-in checks miss
  partial-edge overlaps that NEW catches; OLD's cross-location "exists" warning
  has no NEW analogue (NEW only warns same-location). Net: NEW is stricter and
  cleaner same-location, but drops OLD's cross-location advisory.
- **Plain-English:** OLD used several overlapping rules (one even warned about a
  clash at a different office); NEW uses one tidy overlap rule per office.
- **Keep in NEW?** Keep NEW's interval test; flag the dropped cross-location
  advisory message for Adrian (likely an OLD artifact, not a real requirement).

### G-07-05 -- Custom-field MultipleValues (picklist/radio options) not editable in OLD UI nor surfaced in NEW admin

- **Class:** Partial behavior
- **OLD:** `CustomField.MultipleValues` (`MaxLength(200)`) stores comma options
  for Picklist/Radio. But the OLD add/edit form
  (`custom-field-add.component.html`) exposes ONLY `fieldLabel`, `fieldTypeId`,
  `fieldLength`, `isMandatory` -- there is NO `MultipleValues` input and the
  `appointmentTypeId` select is commented out (lines 15-24). So OLD could store
  picklist options in the schema but the admin UI never let you enter them.
- **NEW:** Entity carries `MultipleValues` + `DefaultValue` + `AppointmentTypeId`
  and `CustomFieldsAppService` Create/Update accept them, but with no admin UI
  (see G-07-01) none of it is reachable. The booking renderer
  (`appointment-add-custom-fields.component.ts`) reads all 7 field types.
- **What it is:** Per-field dropdown/radio option list and default value.
- **Why it existed:** Needed to render Picklist (14) / Radio (17) custom fields.
- **What it does + user impact:** In OLD, picklist/radio custom fields were
  effectively un-configurable from the UI (latent schema). NEW exposes them in
  the API but, lacking the admin UI, they remain un-configurable in practice.
- **Plain-English:** Drop-down custom fields could be stored but never set up
  through the screen -- still true in NEW until the admin screen is built.
- **Keep in NEW?** Yes -- when building the custom-field admin screen (G-07-01),
  ADD the `MultipleValues` editor OLD lacked (parity-plus, matches NEW intent).

### G-07-06 -- Custom-field AvailableTypeId / FieldLength not modeled in NEW

- **Class:** Partial behavior
- **OLD:** `CustomField.AvailableTypeId` (`int`, FK to `AvailableType` enum;
  only value `Appointment = 11`) and `FieldLength` (`Nullable<int>`).
- **NEW:** `CustomField` has `FieldLength` (`int?`) but NO `AvailableTypeId`.
- **What it is:** `AvailableTypeId` scopes a custom field to a context
  ("where the field is available", only ever `Appointment`).
- **Why it existed:** OLD's generic field engine anticipated multiple host
  contexts; only Appointment was ever used.
- **What it does + user impact:** Effectively a constant in OLD (always 11), so
  dropping it has no functional impact -- noted as a deliberate simplification,
  not a behavioral loss. `FieldLength` IS preserved.
- **Plain-English:** OLD had a "where does this field live" knob that was always
  set to the same value; NEW removed the unused knob.
- **Keep in NEW?** Do not re-add `AvailableTypeId` -- vestigial. (Listed as
  Partial only because a column is absent; behavior is equivalent.)

### G-07-07 -- No AppointmentDocumentType master lookup in NEW (free-text instead)

- **Class:** Missing behavior
- **OLD:** `AppointmentDocumentType` entity (`spm.AppointmentDocumentTypes`:
  `AppointmentDocumentTypeId`, `DocumentTypeName`, `StatusId`) with a full CRUD
  controller (`AppointmentDocumentTypesController`) and domain. Uploaded
  appointment documents were categorized by FK to this lookup.
- **NEW:** No `AppointmentDocumentType` entity. `AppointmentDocument` uses a
  free-text `DocumentName` string and its own header comment lists
  "AppointmentDocumentType lookup (free-text DocumentName at MVP)" as a
  deliberate cut.
- **What it is:** Admin-managed catalog of document categories.
- **Why it existed:** Let OLD constrain/standardize document categories and
  filter uploads by type.
- **What it does + user impact:** NEW uploaders type any name; no controlled
  vocabulary, no per-category filtering, no admin curation of categories.
- **Plain-English:** OLD had a managed list of document categories to pick from;
  NEW just lets you type a name.
- **Keep in NEW?** Flag for Adrian -- decide whether controlled categories are
  required for the IME workflow before re-adding.

### G-07-08 -- Country / City / ApplicationTimeZone / GlobalSetting lookups absent

- **Class:** Intent deviation
- **OLD:** `Country` (currency/date/phone formats, `DefaultLanguageId`), `City`
  (FK to Country + State), `ApplicationTimeZone`, `GlobalSetting`/`LockRecord`
  (record-lock duration, 2FA flag, auto-translation, social auth, request
  logging) -- the i18n / global-config infrastructure.
- **NEW:** None present. `Location.City` and `WcabOffice.City` are free-text
  strings in BOTH OLD and NEW (OLD `Location.City` is a `string`, not an FK to
  `City`), so the City lookup was already unused for addresses. Timezone /
  global settings are handled by ABP framework settings + LeptonX.
- **What it is:** OLD's single-tenant globalization + global-toggle tables.
- **Why it existed:** OLD rolled its own i18n, record-locking, and feature flags.
- **What it does + user impact:** Equivalent capabilities now come from ABP
  (settings management, localization, feature system). The OLD tables were
  largely infrastructure, not user-facing master data; their absence does not
  change admin-visible behavior except that OLD's `GlobalSetting` toggles (2FA,
  auto-translation, record-lock) have no 1:1 admin screen yet.
- **Plain-English:** OLD's home-grown global-settings tables are replaced by the
  framework's built-in settings -- mostly equivalent, but the specific OLD
  toggles aren't surfaced.
- **Keep in NEW?** Mostly do-not-port (use ABP). Flag `GlobalSetting`'s
  record-lock + 2FA toggles for Adrian to confirm coverage via ABP settings.

### G-07-09 -- AccessType (View/Edit) field-permission model absent

- **Class:** Missing behavior
- **OLD:** `AccessType` enum (`View = 23`, `Edit = 24`) -- per-field/role access
  granularity used alongside `RoleAppointmentType`.
- **NEW:** `Enums/AccessType.cs` exists with identical values but is not wired
  into any config flow surfaced in this area. NEW's analogous capability is
  `AppointmentTypeFieldConfig` (`Hidden` / `ReadOnly` / `DefaultValue` per
  field per appointment type).
- **What it is:** Field-level View vs Edit access control.
- **Why it existed:** OLD gated whether a role could view or edit specific
  fields.
- **What it does + user impact:** NEW reframes this as the W2-5
  `AppointmentTypeFieldConfig` (read-only / hidden / default per field) rather
  than a role x field View/Edit matrix. Outcome overlaps but the granularity
  model differs and NEW's config has no admin UI (G-07-01).
- **Plain-English:** OLD controlled per-field view/edit rights; NEW does
  something similar per appointment type but the screen to manage it isn't built.
- **Keep in NEW?** Keep the `AppointmentTypeFieldConfig` reframing; flag whether
  role-scoped View/Edit granularity is still needed (defer detailed RBAC to area 06).

### G-07-10 -- System-parameter validation deviation: OLD never validated on edit; NEW re-validates Range(1, max)

- **Class:** Intent deviation
- **OLD:** `SystemParameterDomain.UpdateValidation` (`SystemParameterDomain.cs:44-48`)
  calls an EMPTY `CommonValidation` -- no range enforcement on update. The
  `[Range(1, int.MaxValue)]` attributes only fire via model binding on insert.
- **NEW:** `SystemParametersAppService.ValidatePositiveIntegers` (lines 121-134)
  re-applies `Check.Range(1, int.MaxValue)` to all 11 integer fields on EVERY
  update, plus a CcEmailIds length check and optimistic concurrency.
- **What it is:** Update-time validation of the parameter integers.
- **Why it existed:** OLD's update path simply didn't guard (a likely oversight).
- **What it does + user impact:** OLD could persist a 0/negative parameter via
  PUT; NEW rejects it. NEW is stricter and safer. Documented in NEW as the
  "OLD-bug-fix exception."
- **Plain-English:** OLD let you save a bad (zero) setting when editing; NEW
  stops you.
- **Keep in NEW?** Keep -- correct fix.

### G-07-11 -- Notification-template Create/Delete removed; OLD allowed both

- **Class:** Partial behavior
- **OLD:** `TemplatesController` + `TemplateDomain` expose full CRUD: POST
  (`Add` with duplicate-code+type guard), PUT, soft-DELETE (sets
  `StatusId = Delete`).
- **NEW:** `NotificationTemplatesAppService` exposes only `GetList`, `Get`,
  `GetByCode`, `GetTypeLookup`, `Update`. Create + Delete intentionally omitted
  (handlers resolve templates by a fixed code set; deleting would break
  `FindByCodeAsync`; disabling is via `IsActive`).
- **What it is:** Ability to add/remove notification templates.
- **Why it existed:** OLD let admins create new template codes ad hoc.
- **What it does + user impact:** NEW admins edit body/subject/active of the
  seeded code set but cannot create new codes or hard-delete. For a fixed-set
  notification system this is acceptable; if OLD relied on admin-created codes,
  that capability is lost.
- **Plain-English:** You can edit the message templates but not add brand-new
  ones or delete them; you turn them off instead.
- **Keep in NEW?** Keep the edit-only model; flag whether any deployment created
  custom template codes at runtime (likely no -- codes are hardcoded enums).

### G-07-12 -- Custom-field per-type cap math: OLD off-by-one global == 10; NEW per-type >= 10

- **Class:** Intent deviation (OLD half is an OLD bug -- see OLD-bugs)
- **OLD:** `CustomFieldDomain.AddValidation` (`:38-43`) counts ACTIVE custom
  fields GLOBALLY (with a broken predicate `customField.StatusId ==
  (int)Status.Active` comparing the input to itself), and blocks only when the
  count is EXACTLY `== 10`. Update path does not cap at all.
- **NEW:** `CustomFieldsAppService.EnsureUnderActiveCapAsync` counts active rows
  PER `AppointmentTypeId` and blocks at `>= 10`, on create AND on the relevant
  update paths (becoming active / moving type).
- **What it is:** The "max custom fields" rule.
- **Why it existed:** Limit how many extra fields render on the booking form.
- **What it does + user impact:** OLD = at most 10 across the whole system,
  enforced only at the exact boundary and only on add. NEW = at most 10 per
  appointment type, enforced robustly. NEW is the documented "OLD-bug-fix /
  spec-intent" correction (spec: "10 per type").
- **Plain-English:** OLD capped total custom fields at 10 (and even that was
  buggy); NEW caps 10 per appointment type and counts correctly.
- **Keep in NEW?** Keep NEW.

### G-07-13 -- Location/WcabOffice/AppointmentType StatusId enum collapsed to IsActive; no soft-delete state machine

- **Class:** Intent deviation
- **OLD:** These entities carry `StatusId` (the `Status` enum:
  Active/InActive/Delete). Delete sets `StatusId = Delete` for some
  (CustomField, Template, DoctorPreferredLocation) but HARD-deletes others
  (AppointmentType, Location, WcabOffice via `RegisterDeleted`).
- **NEW:** `Location`, `WcabOffice`, `AppointmentType` have a simple `IsActive`
  bool (or none -- `AppointmentType` has neither status nor IsActive) and use
  ABP `FullAuditedAggregateRoot` soft-delete (`IsDeleted`) instead of a
  tri-state status.
- **What it is:** The active/inactive/deleted lifecycle of master rows.
- **Why it existed:** OLD's `Status` enum tri-state.
- **What it does + user impact:** Outcome-equivalent for active/inactive
  filtering; ABP soft-delete replaces `StatusId = Delete`. Minor divergence:
  `AppointmentType` in NEW has NO active toggle at all (always shown), whereas
  OLD could deactivate a type.
- **Plain-English:** OLD had a 3-state status flag; NEW uses on/off + the
  framework's built-in delete. AppointmentType lost its on/off switch.
- **Keep in NEW?** Keep the IsActive model; flag adding IsActive to
  AppointmentType if deactivating a type is required.

---

## Equivalent -- different implementation

These are NOT gaps (outcome-equivalent on the modern stack):

1. **CRUD plumbing.** OLD thin `BaseController` -> `IDomain` with
   `AddValidation/Add/UpdateValidation/Update/DeleteValidation/Delete` +
   `HashSet<string>` messages, paging via stored procs (`spAppointmentTypes`,
   `spLocations`, `spTemplates`, `spDoctorsAvailabilities`). NEW: ABP AppService
   + Manager + EF repository + `PagedResultDto`. Same effect.
2. **AppointmentType CRUD + unique-name guard.** OLD `AppointmentTypeDomain`
   blocks duplicate `AppointmentTypeName`; NEW `AppointmentTypesAppService` does
   standard CRUD. (NEW drops the explicit uniqueness check -- noted as a minor
   relaxation, not a behavioral gap for parity intent; flag if uniqueness
   required.)
3. **Location CRUD with ParkingFee + AppointmentTypeId + State.** OLD
   `Location` (ParkingFee decimal, AppointmentTypeId int, State int) ->
   NEW `Location` (ParkingFee decimal, AppointmentTypeId Guid?, StateId Guid?).
   Field set preserved; NEW is host-scoped (not multi-tenant) which is correct
   for shared reference data.
4. **WcabOffice CRUD.** OLD fields (Name, Abbreviation, Address, City, StateId,
   ZipCode, StatusId) -> NEW (Name, Abbreviation, Address, City, ZipCode,
   StateId, IsActive). 1:1 on user-facing fields.
5. **AppointmentStatus / AppointmentLanguage lookups.** OLD entities ->
   NEW `AppointmentStatuses` + `AppointmentLanguages` WITH full Angular CRUD
   screens. Parity-complete.
6. **State lookup.** Present in NEW with full Angular CRUD.
7. **Document (master catalog) + PackageDetail.** OLD `Document`
   (DocumentName, DocumentFilePath, StatusId) + `PackageDetail` (PackageName,
   AppointmentTypeId, StatusId) -> NEW `Document` (Name, BlobName, ContentType,
   IsActive) + `PackageDetail` (PackageName, AppointmentTypeId, IsActive).
   OLD's file-path is replaced by blob storage; admin UI still missing (G-07-01).
8. **Notification-template TemplateCode int enum -> stable string code.** OLD
   `TemplateCode` int enum (1-18) -> NEW string `TemplateCode` (documented as
   "stable string code that survives migrations"). Same per-code resolution.
9. **DOCX -> PDF for reports** and **stored-proc paging -> ABP paging** are
   per-mission expected stack swaps, not gaps.

---

## OLD bugs (do not port)

1. **CustomField global cap with self-comparing predicate + exact `== 10`.**
   `CustomFieldDomain.cs:38` counts `x => x.StatusId == Active && customField.StatusId
   == Active` -- the second clause compares the INPUT to itself (constant), so
   the WHERE degenerates to "all active rows" (a global, not per-type, count).
   Line 40 blocks only when count is EXACTLY 10, so a concurrent insert reaching
   11 is never caught, and update never caps. NEW fixed (per-type, `>= 10`,
   create+update). Confirmed do-not-port; NEW behavior is correct.
2. **System-parameter update has no validation.** `SystemParameterDomain`
   `CommonValidation` is empty and `UpdateValidation` enforces nothing, allowing
   0/negative parameter values via PUT despite the `[Range(1,max)]` attributes.
   NEW re-validates. Do not port the gap (G-07-10).
3. **Two SystemParameter fields persisted but hidden in the OLD UI.**
   `AppointmentCancelTime` and `JointDeclarationUploadCutoffDays` exist on the
   `SystemParameter` table and view, but the OLD edit form comments them out
   (`system-parameter-edit.component.html:43-50`). So the OLD UI could never edit
   two real, consumed parameters. NEW keeps both fields on the entity + update
   DTO (so a future NEW admin screen CAN expose them) -- the correct fix is to
   SHOW them in the new admin screen (G-07-01), not to hide them. Flag for Adrian
   to confirm both should be editable.

---

## Open questions

1. **AppointmentType.ReEvalId (G-07-02):** is the re-eval pairing still a
   business requirement, or are PQME-REVAL / AME-REVAL now just independent types?
2. **Slot generation partial-insert (G-07-03):** confirm NEW's
   "insert clean, skip conflicts" UX is acceptable vs OLD's strict all-or-nothing.
3. **AppointmentDocumentType (G-07-07):** does the IME workflow need a controlled
   document-category vocabulary, or is free-text sufficient?
4. **GlobalSetting toggles (G-07-08):** are OLD's record-lock duration, 2FA,
   auto-translation, social-auth, request-logging toggles fully covered by ABP
   settings, and do any need a dedicated admin screen?
5. **Hidden system params (OLD-bug 3):** should `AppointmentCancelTime` and
   `JointDeclarationUploadCutoffDays` be editable in the new System Parameters
   screen?
6. **AppointmentType IsActive (G-07-13):** does an admin need to deactivate an
   appointment type (NEW currently has no such toggle)?
