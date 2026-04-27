# 02 -- Domain Entities + Services: Gap Analysis OLD vs NEW

## Summary

OLD follows an anemic-entity + fat-domain-service pattern: entities are pure data bags generated against a database-first EF Core 2.x model, and 28 `*Domain.cs` classes under `P:\PatientPortalOld\PatientAppointment.Domain\**` carry all business rules, transaction orchestration, stored-procedure fanout, email/SMS side effects, and audit. NEW uses ABP DDD: rich entities with constructors and invariants, 11 slim `*Manager` domain services whose only work is create/update with length and null validation, and business logic pushed up to the AppService layer where possible.

OLD exposes 60+ transactional entities (Appointment + 11 appointment-child entities + full injury/claim/insurance/joint-declaration sub-graph + change-request/change-log audit + custom fields + templates + scheduler-driven reminder jobs). NEW implements 15 entities: the same Appointment + Patient + Doctor + DoctorAvailability + ApplicantAttorney + AppointmentAccessor + AppointmentApplicantAttorney + AppointmentEmployerDetail, plus 7 lookup entities.

The headline risk: NEW's `AppointmentManager` is 60 lines, sets no status or lifecycle hooks, never cascades to `DoctorAvailability.BookingStatusId`, and has no state-transition guard. OLD's `AppointmentDomain.Add` / `Update` / `UpdateDoctorAvailbilty` carry 400+ lines of embedded business rules around the 13-state lifecycle. The 13-state enum is identically defined, but only OLD drives it; NEW can set any status to any value with no transition constraint (confirmed in `Appointments\CLAUDE.md` line 58-67: "No enforced transitions in domain -- status set directly").

**MVP risk: High.** ~14 MVP-blocking gaps, mostly concentrated in appointment lifecycle enforcement, document handling, injury/claim tracking, and scheduled notifications.

## Method

Read the following OLD sources at `P:\PatientPortalOld\`:

- All 28 `*Domain.cs` files under `PatientAppointment.Domain\**`
- Base DbEntity classes under `PatientAppointment.DbEntities\Models\` (63 non-view entity classes)
- Enum definitions under `PatientAppointment.DbEntities\Enums\` + `PatientAppointment.Models\Enums\` (18 files)

Read the following NEW sources at `W:\patient-portal\development\`:

- `src\HealthcareSupport.CaseEvaluation.Domain\**` -- 15 feature folders
- `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Enums\` -- 5 enum files
- Generated docs: `docs\backend\DOMAIN-MODEL.md`, `docs\backend\DOMAIN-SERVICES.md`, `docs\business-domain\APPOINTMENT-LIFECYCLE.md`, `docs\business-domain\USER-ROLES-AND-ACTORS.md`, and per-feature `CLAUDE.md` files (Appointments, AppointmentAccessors, DoctorAvailabilities examined in depth)
- `src\HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs` (to cross-check rules)

Timestamp: 2026-04-23.

## OLD version state

### Domain-service inventory (28 classes)

| Domain service | File (P:\PatientPortalOld\PatientAppointment.Domain) | Responsibilities |
|---|---|---|
| AppointmentDomain | `AppointmentRequestModule\AppointmentDomain.cs:28` | 400-line validation/orchestration for 13-state lifecycle; patient auto-match; slot cascade; email/SMS |
| AppointmentAccessorDomain | `AppointmentRequestModule\AppointmentAccessorDomain.cs:28` | Auto-create `User` accounts for accessors; email credentials; per-field change-log |
| AppointmentInjuryDetailDomain | `AppointmentRequestModule\AppointmentInjuryDetailDomain.cs:19` | Injury + body parts + claim examiners + primary insurance |
| AppointmentDocumentDomain | `AppointmentRequestModule\AppointmentDocumentDomain.cs:32` | S3 upload; due-date gating; verification-code validation |
| AppointmentJointDeclarationDomain | `AppointmentRequestModule\AppointmentJointDeclarationDomain.cs` | Joint declaration PDF + status |
| AppointmentNewDocumentDomain | `AppointmentRequestModule\AppointmentNewDocumentDomain.cs` | Post-approval document handling |
| AppointmentChangeRequestDomain | `AppointmentChangeRequestModule\AppointmentChangeRequestDomain.cs:15` | Reschedule/cancel workflow |
| AppointmentChangeLogDomain | `AppointmentChangeLogModule\AppointmentChangeLogDomain.cs` | Reflection-based field-diff logging; emails |
| SchedulerDomain | `Core\SchedulerDomain.cs:18` | 9 reminder-notification dispatchers |
| UserAuthenticationDomain | `Core\UserAuthenticationDomain.cs:23` | JWT login, role/user-type lookup |
| UserDomain | `UserModule\UserDomain.cs` | User CRUD with password hash, email-verification |
| UserQueryDomain | `UserQueryModule\UserQueryDomain.cs` | Contact us flow |
| DoctorDomain | `DoctorManagementModule\DoctorDomain.cs` | Doctor CRUD |
| DoctorsAvailabilityDomain | `DoctorManagementModule\DoctorsAvailabilityDomain.cs:18` | Bulk-generate slots with duration stride + overlap validation |
| DoctorPreferredLocationDomain | `DoctorManagementModule\DoctorPreferredLocationDomain.cs` | Doctor<->Location M2M |
| DoctorsAppointmentTypeDomain | `DoctorManagementModule\DoctorsAppointmentTypeDomain.cs` | Doctor<->Type M2M |
| AppointmentTypeDomain, LocationDomain, WcabOfficeDomain | DoctorManagement | Standard CRUD |
| AppointmentDocumentTypeDomain | `DocumentModule\AppointmentDocumentTypeDomain.cs` | Document type lookup |
| DocumentPackageDomain, PackageDetailDomain, DocumentDomain | DocumentManagement | Document bundles |
| NoteDomain | `NoteModule\NoteDomain.cs` | Note thread with parent/child |
| CustomFieldDomain | `CustomFieldModule\CustomFieldDomain.cs` | Dynamic custom fields |
| SystemParameterDomain | `SystemParameterModule\SystemParameterDomain.cs` | Tenant config |
| TemplateDomain | `TemplateManagementModule\TemplateDomain.cs` | Email/SMS templates |

### Entity inventory (63 transactional)

**Appointment core + satellites (13):** Appointment, AppointmentAccessor, AppointmentChangeRequest, AppointmentChangeRequestDocument, AppointmentChangeLog, AppointmentDocument, AppointmentEmployerDetail, AppointmentInjuryDetail, AppointmentInjuryBodyPartDetail, AppointmentJointDeclaration, AppointmentNewDocument, AppointmentRequestReport, Note.

**Attorney/legal (4):** AppointmentPatientAttorney, AppointmentDefenseAttorney, AppointmentClaimExaminer, AppointmentPrimaryInsurance.

**Workers-comp specific (1):** AppointmentWorkerCompensation.

**Doctor (4):** Doctor, DoctorPreferredLocation, DoctorsAppointmentType, DoctorsAvailability.

**Lookup / config (12+):** AppointmentStatus, AppointmentType, AppointmentLanguage, AppointmentDocumentType, DocumentStatus, DocumentPackage, Document, PackageDetail, Language, LanguageContent, ConfigurationContent, Country, State, City, Location, WcabOffice.

**User / security (10):** User, Role, RolePermission, RoleAppointmentType, RoleUserType, ApplicationUserToken, ApplicationTimeZone, ApplicationModule, ApplicationObject, ApplicationObjectType.

**Audit/exception (4):** ApplicationExceptionLog, AuditRecord, AuditRecordDetail, AuditRequest, LockRecord, RequestLog, ModuleMaster, ModuleContent.

**Other:** CustomField, CustomFieldsValue, Template, TemplateType, SystemParameter, GlobalSetting, SMTPConfiguration, UserQuery.

### OLD enum inventory

| Enum | Values |
|---|---|
| AppointmentStatusType | 13 values: Pending=1 ... CancellationRequested=13 |
| AppointmentType | PQME=1, PQMEREEVAL=2, AME=3, AMEREEVAL=4, ALL=5, OTHER=6 |
| Roles | ItAdmin=1, StaffSupervisor=2, ClinicStaff=3, Patient=4, Adjuster=5, PatientAttorney=6, DefenseAttorney=7 |
| BookingStatus | Available=8, Booked=9, Reserved=10 |
| RequestStatus | Pending=25, Accepted=26, Rejected=27 |
| AccessType | View=23, Edit=24 |
| Gender | Male=4, Female=5, Other=30 |
| Status | Active=1, InActive=2, Delete=3 |
| UserType | InternalUser=6, ExternalUser=7 |
| ReminderTypes | 9 values driving 9 scheduler methods |
| DocumentStatuses | Uploaded=1, Accepted=2, Rejected=3, Pending=4, Deleted=5 |
| TemplateCode | 18 notification-template identifiers |
| UserTypesForEmail | 8 routing targets |
| TemplateTypeEnums | Email=1, SMS=2 |

### OLD business rules embedded in `*Domain.cs`

1. **Slot booking cascade** (`AppointmentDomain.cs:199-310`, `:586-611`): External user -> Reserved/Pending, Internal user -> Booked/Approved. Status changes cascade to DoctorAvailability.BookingStatusId.
2. **Appointment lead-time validation** (`AppointmentDomain.cs:119-157`): external users cannot book within `systemParameters.AppointmentLeadTime` days; per-type max horizons (PQME/AME/OTHER).
3. **Patient auto-match** (`AppointmentDomain.cs:732-780`): 3-of-6 column heuristic (last name, phone, SSN, DOB, email, claim number) to reuse existing Patient.
4. **Re-eval flow** (`AppointmentDomain.cs:162-184`, `:242-254`): `IsRevolutionForm` / `IsReRequestForm` flags gate re-booking; flips old appointment to Rejected.
5. **Update freezes on Approved** (`AppointmentDomain.cs:396-402`): most fields frozen after Approved; status-only updates go through separate branch with idempotency.
6. **Role-AppointmentType gate** (`AppointmentDomain.cs:640-643`): user's role must be linked to appointment type in `RoleAppointmentType` table.
7. **Injury/DOB validation** (`AppointmentDomain.cs:645-705`): date of injury not future; DOB strictly before today; DOB < any DateOfInjury; claim number + injury date unique.
8. **Accessor auto-provisioning** (`AppointmentAccessorDomain.cs:263-354`): auto-creates User with random password, emails credentials.
9. **Accessor cannot collide with existing role** (`AppointmentAccessorDomain.cs:131-139`): if email maps to a different RoleId, fails.
10. **Slot generation** (`DoctorsAvailabilityDomain.cs:290-449`): `AppointmentDurationTime` per slot; walks date range; checks overlapping/already-booked.
11. **Slot-delete guards** (`DoctorsAvailabilityDomain.cs:141-177`): cannot delete if any slot is Booked or Reserved.
12. **Change-request audit** (`AppointmentDomain.cs:537-549`): `CancelledNoBill` auto-creates AppointmentChangeRequest row.
13. **Field-level change logs** (`AppointmentChangeLogDomain.cs`): reflection-based per-field diffs written to AppointmentChangeLog.
14. **9 notification schedulers** (`SchedulerDomain.cs:37-70`): stored proc + email/SMS per type.
15. **RequestConfirmationNumber** (`AppointmentDomain.cs:273`): formatted from int id, e.g. `A00042`.
16. **Email + SMS side effects** (`AppointmentDomain.cs:839-1050`): per-status switch driving email + SMS templates.
17. **Document due-date gate** (`AppointmentDocumentDomain.cs:94-104`): cannot upload after Approved/RescheduleRequested if `DueDate < Now`.
18. **S3 storage** (`AppointmentDocumentDomain.cs:33-45`): `IAmazonBlobStorage`; `DocumentAwsFilePath`.
19. **Verification-code gating** (`AppointmentDocumentDomain.cs:64-74`): anonymous document view by VerificationCode Guid.
20. **Patient registration fallback**: external signup creates unverified User with VerificationCode Guid.

## NEW version state

### Manager inventory (11 classes, all slim)

| Manager | File:line | Business rules enforced |
|---|---|---|
| AppointmentManager | `Appointments\AppointmentManager.cs:14` (60 lines) | Null checks on Guid FKs; Check.Length for RequestConfirmationNumber and PanelNumber; concurrency stamp |
| DoctorManager | `Doctors\DoctorManager.cs:16` (111 lines) | Length validation; M2M sync for AppointmentTypes/Locations with RemoveAll/AddAppointmentType helpers |
| DoctorAvailabilityManager | `DoctorAvailabilities\DoctorAvailabilityManager.cs:14` (47 lines) | Null checks only; no overlap validation, no bulk generate |
| PatientManager | `Patients\PatientManager.cs:14` (101 lines) | Length checks for 15 string fields; null checks |
| LocationManager | `Locations\LocationManager.cs` | Length validation only |
| ApplicantAttorneyManager | `ApplicantAttorneys\ApplicantAttorneyManager.cs:13` (66 lines) | Length validation only |
| WcabOfficeManager | `WcabOffices\WcabOfficeManager.cs` | Length validation only |
| StateManager, AppointmentTypeManager, AppointmentLanguageManager, AppointmentStatusManager | | Length + null only |
| AppointmentAccessorManager | `AppointmentAccessors\AppointmentAccessorManager.cs:13` (41 lines) | Null checks; **no user auto-provisioning, no email, no change-log** |
| AppointmentApplicantAttorneyManager | `AppointmentApplicantAttorneys\AppointmentApplicantAttorneyManager.cs:13` (42 lines) | Null checks on Guid FKs |
| AppointmentEmployerDetailManager | `AppointmentEmployerDetails\AppointmentEmployerDetailManager.cs:13` (81 lines) | Length validation |

**No `*Manager` exists for:** scheduling, notifications, auditing, change logs, change requests, documents, injury tracking, joint declarations, custom fields, templates, system parameters, user authentication. Most of those entities do not exist.

### Entity inventory (15)

| Entity | File | Base class | Multi-tenant |
|---|---|---|---|
| Appointment | `Appointments\Appointment.cs:19` | `FullAuditedAggregateRoot<Guid>` | IMultiTenant |
| Doctor | `Doctors\Doctor.cs:15` | `FullAuditedAggregateRoot<Guid>` | IMultiTenant |
| DoctorAvailability | `DoctorAvailabilities\DoctorAvailability.cs:16` | `FullAuditedAggregateRoot<Guid>` | IMultiTenant |
| Patient | `Patients\Patient.cs:18` | `FullAuditedAggregateRoot<Guid>` | TenantId prop only (not interface) |
| ApplicantAttorney | `ApplicantAttorneys\ApplicantAttorney.cs:15` | `FullAuditedAggregateRoot<Guid>` | IMultiTenant |
| AppointmentAccessor | `AppointmentAccessors\AppointmentAccessor.cs:16` | `FullAuditedEntity<Guid>` | IMultiTenant |
| AppointmentApplicantAttorney | `AppointmentApplicantAttorneys\AppointmentApplicantAttorney.cs:16` | `FullAuditedAggregateRoot<Guid>` | IMultiTenant |
| AppointmentEmployerDetail | `AppointmentEmployerDetails\AppointmentEmployerDetail.cs:15` | `FullAuditedAggregateRoot<Guid>` | IMultiTenant |
| Location, State, WcabOffice | | `FullAuditedAggregateRoot<Guid>` | Host |
| AppointmentType, AppointmentLanguage, AppointmentStatus | | `FullAuditedEntity<Guid>` | Host |
| Book | `Books\Book.cs` | `AuditedAggregateRoot<Guid>` | Host (ABP demo) |

### NEW enum inventory

| Enum | Values |
|---|---|
| AppointmentStatusType | 13 values -- identical to OLD |
| Gender | Male=1, Female=2, Other=3 (renumbered from OLD 4/5/30) |
| BookingStatus | Available=8, Booked=9, Reserved=10 -- identical |
| AccessType | View=23, Edit=24 -- identical |
| PhoneNumberType | Work=28, Home=29 |

### NEW rules enforced in the Appointment flow

Most logic lives in `AppointmentsAppService.cs`, not the Manager:

1. **Slot validation on create** (`:235-262`): slot must be Available, match LocationId, AppointmentTypeId, date, and time inside [FromTime, ToTime).
2. **Slot marked Booked** (`:201-202`): one-way. `DeleteAsync` does NOT release slot back. Per `Appointments\CLAUDE.md:126-128`.
3. **Confirmation number auto-generation** (`:264-292`): scans max existing `A#####`, increments, caps at A99999.
4. **Existence checks for Guid FKs** (`:166-194`): FindAsync + UserFriendlyException for Patient/IdentityUser/AppointmentType/Location/DoctorAvailability.
5. **No transition guard** (per `Appointments\CLAUDE.md:67`): any caller can set any status.
6. **Update freezes key fields** (per `Appointments\CLAUDE.md:132`): AppointmentStatus, InternalUserComments, AppointmentApproveDate, IsPatientAlreadyExist unchangeable after create.
7. **Lookup endpoints filter through Doctor relations** (`:112-141`): `GetAppointmentTypeLookupAsync` returns only types linked to doctors.
8. **Applicant-attorney upsert** (`:400-463`): atomic create-or-update-and-link.

## Delta

### MVP-blocking gaps (capability present in OLD, absent in NEW)

| Gap ID | Capability | Evidence in OLD | Evidence of absence in NEW | Rough effort |
|---|---|---|---|---|
| G2-01 | Appointment state-transition enforcement | `AppointmentDomain.cs:314-344`, `:453-456` | No state machine in `AppointmentManager.cs`; `Appointments\CLAUDE.md:67` says "No domain methods enforce valid transitions" | M (2-3 days) |
| G2-02 | DoctorAvailability booking cascade on reschedule/cancel/delete | `AppointmentDomain.cs:431-448`, `:586-611` | `AppointmentsAppService.CreateAsync` sets Booked but `DeleteAsync` doesn't restore | M (1-2 days) |
| G2-03 | AppointmentLeadTime / MaxTimePQME / MaxTimeAME / MaxTimeOTHER | `AppointmentDomain.cs:119-157` + `SystemParameter.cs` | No SystemParameter entity in NEW | M (3 days) |
| G2-04 | Patient auto-match (3-of-6 column fuzzy match) | `AppointmentDomain.cs:732-780` | No match logic in PatientManager or AppointmentsAppService | M-L (3 days) |
| G2-05 | AppointmentAccessor auto-user-provisioning | `AppointmentAccessorDomain.cs:263-354` | `AppointmentAccessorManager.CreateAsync:22-29` only takes pre-existing identityUserId | L (4 days) |
| G2-06 | AppointmentChangeRequest entity + domain service | `AppointmentChangeRequest.cs:14`, `AppointmentChangeRequestDomain.cs:15` | No entity folder in Domain | M-L (5 days) |
| G2-07 | AppointmentInjuryDetail entity + sub-graph (Body Parts + Claim Examiners + Primary Insurance) | `AppointmentInjuryDetail.cs:14` + 3 child tables + domain service | Nothing in NEW | L (7+ days) |
| G2-08 | AppointmentDocument upload with S3 + verification code | `AppointmentDocument.cs:14`, `AppointmentDocumentDomain.cs:32` | No entity; no blob storage integration | L (5+ days) |
| G2-09 | AppointmentPatientAttorney + AppointmentDefenseAttorney as separate stakeholders | `AppointmentPatientAttorney.cs:14`, `AppointmentDefenseAttorney.cs:14` | NEW has unified `ApplicantAttorney` only | M (2 days) |
| G2-10 | Appointment full-field snapshot (AppointmentApproveDate, CancelledById, RejectedById, ReScheduledById, CancellationReason, RejectionNotes, ReScheduleReason, PrimaryResponsibleUserId, OriginalAppointmentId) | `Appointment.cs:14-107` | NEW `Appointment.cs:19-72` has minimal subset | S-M (1-2 days) |
| G2-11 | Scheduled notification jobs (9 types) | `SchedulerDomain.cs:18-70` | No scheduler/background job infrastructure | L (5+ days) |
| G2-12 | RoleAppointmentType gate | `AppointmentDomain.cs:640-643` | No entity, no permission logic | S (1 day) |
| G2-13 | AppointmentChangeLog field-level diff audit | `AppointmentChangeLogDomain.cs` | ABP `FullAuditedAggregateRoot` tracks meta but not per-field diffs | M (3 days) |
| G2-14 | AppointmentJointDeclaration upload + rejection flow | `AppointmentJointDeclaration.cs:14` | Not present | M (3 days) |

### Non-MVP gaps

| Gap ID | Capability | Effort |
|---|---|---|
| G2-N1 | Note thread per appointment | S (2 days) |
| G2-N2 | CustomField + CustomFieldsValue per AppointmentType | M (4 days) |
| G2-N3 | Email + SMS template CRUD | M (4 days) |
| G2-N4 | UserQuery "contact us" flow | S (1 day) |
| G2-N5 | Adjuster-as-appointment-stakeholder (role 5 in OLD) | S-M (1-2 days) |
| G2-N6 | Working change-log email dispatch on intake-form change | S (follows G2-13) |
| G2-N7 | Document package / package-detail lookup | S (follows G2-08) |
| G2-N8 | UserQuery contact-us form | S (1 day) |
| G2-N9 | AppointmentNewDocument (post-approval file uploads) | S (1 day) |
| G2-N10 | AppointmentWorkerCompensation enum + row | S (1 day) |
| G2-N11 | TimeSlotGenerateTypes bulk slot generation | M (3 days) |
| G2-N12 | OriginalAppointmentId link | S (1 day + migration) |
| G2-N13 | AppointmentRequestReport entity | S |

### Intentional architectural differences (NOT gaps)

| Topic | OLD | NEW | Why |
|---|---|---|---|
| Entity base | Plain partial classes, manual audit columns | `FullAuditedAggregateRoot<Guid>` with soft-delete, concurrency, audit | ABP convention |
| Primary keys | `int` identity | `Guid` everywhere | Multi-tenant safety |
| Schema namespace | `dbo` + `spm` two-schema split | Single `dbo` | Simpler |
| Business logic distribution | Fat `*Domain.cs` (60-1100+ lines) | Slim `*Manager.cs` + logic in AppService | ABP convention; testable |
| Role model | `Role.cs` entity + 7 fixed roles enum | ABP Identity `IdentityUser`/`IdentityRole`; 4 seeded external roles + admin | ABP/OIDC |
| Attorney model | Two distinct entities (PatientAttorney + DefenseAttorney) | Single ApplicantAttorney + join | ABP DDD consolidation (may be intentional gap -- verify) |
| Gender enum | 4/5/30 in unified `ApplicationObjects` | Clean C# enums 1/2/3 | Simpler |
| Slot validation location | `DoctorsAvailabilityDomain` pre-insert overlap + duration | `AppointmentsAppService` at booking time only | Defers overlap-at-generation to TBD |
| Mapping | Manual property copy | Riok.Mapperly `[Mapper]` source-gen | Per ADR 001 |
| Multi-tenancy runtime | Tenant-per-DB via `MainSqlDbContext` + `DbContextManager` | Row-level `IMultiTenant` with auto-filter | Per plan seed |
| Email + SMS | AWS SES + Twilio wired into domain services | Not yet integrated | Track 6 covers |

### Extras in NEW (not in OLD)

| Extra | Evidence | Value |
|---|---|---|
| Concurrency stamp on every update | All managers call `SetConcurrencyStampIfNotNull` | Optimistic concurrency |
| Doctor M2M on-entity methods | `Doctor.cs:58-132` | Aggregate encapsulation |
| ExternalSignupAppService + ExternalUserType enum | `ExternalSignupAppService.cs` | Tenant-scoped external-user registration |
| GetApplicantAttorneyDetailsForBookingAsync (email fallback) | `AppointmentsAppService.cs:327-367` | Pre-populate attorney info |
| ABP `[RemoteService(IsEnabled = false)]` + manual controllers | Per ADR 002 | Explicit routing |
| Books entity (ABP scaffolding) | `Books\Book.cs` | Demo for tests |
| Appointment.IdentityUserId separate from Patient.IdentityUserId | `Appointment.cs:44` + `Patient.cs:75` | Supports attorney-books-for-patient |

### Appointment lifecycle 13-state transition matrix

OLD enforces 29 of the 30 documented transitions. NEW enforces 0 (besides initial create).

| Transition | OLD | NEW | Gap |
|---|---|---|---|
| [*] -> Pending | External-user create (`AppointmentDomain.cs:225-231`) | Any caller passes status | G2-01 |
| [*] -> Approved | Internal-user create (`:233-239`) | Any caller | G2-01, G2-12 |
| Pending -> Approved | `:314-344` idempotency + approve date + slot Booked | Not implemented | G2-01, G2-02 |
| Pending -> Rejected | `:323-325` + slot Available | Not implemented | G2-01, G2-02 |
| Pending -> CancelledNoBill | `:537-550` auto-creates ChangeRequest + slot Available | Not implemented | G2-01, G2-02, G2-06 |
| Pending -> CancelledLate | Slot cascade | Not implemented | G2-01, G2-02 |
| Pending -> RescheduledNoBill | Slot swap | Not implemented | G2-01, G2-02 |
| Pending -> RescheduledLate | Slot swap | Not implemented | G2-01, G2-02 |
| Pending -> RescheduleRequested | Creates change-request; status flips | Not implemented | G2-01, G2-06 |
| Pending -> CancellationRequested | Creates change-request | Not implemented | G2-01, G2-06 |
| Approved -> CheckedIn | `:319-321` + email | Not implemented | G2-01 |
| Approved -> CheckedOut | `:327-329` + email | Not implemented | G2-01 |
| Approved -> NoShow | `SendEmail:1016-1027` routes | Not implemented | G2-01 |
| Approved -> CancelledNoBill/CancelledLate/etc | Same paths as Pending | Not implemented | G2-01 |
| CheckedIn -> CheckedOut | `:331-333` | Not implemented | G2-01 |
| CheckedOut -> Billed | `:335-337` | Not implemented | G2-01 |
| RescheduleRequested -> Approved/etc | Admin acts on change-request | Not implemented | G2-01, G2-06 |
| CancellationRequested -> Cancelled/etc | Same | Not implemented | G2-01, G2-06 |
| Billed -> [*] | Terminal | Enum allows but no path | G2-01 |

## Open questions

1. **Is `AppointmentDefenseAttorney` consolidated into `ApplicantAttorney` intentionally?** OLD tracks both as separate entities with 17 firm-detail columns each. NEW has only ApplicantAttorney. Defense Attorney role seeded but no entity. Gap G2-09.
2. **Is the 13-state lifecycle meant to be enforced, or just advisory?** `Appointments\CLAUDE.md:67` says "No domain methods enforce valid transitions." Is enforcement required for MVP?
3. **Is S3/blob storage for AppointmentDocument in scope for MVP?** OLD depends on `IAmazonBlobStorage`. NEW has no equivalent.
4. **Scheduler jobs: needed for MVP or deferred?** OLD has 9 recurring notification jobs. NEW has none.
5. **Is patient auto-matching (3-of-6 columns) replaced or missing?** OLD at `AppointmentDomain.cs:732-780`.
6. **CustomField / per-tenant dynamic forms: MVP?** OLD has full implementation; NEW has none.
7. **AppointmentInjuryDetail + Claim Examiner + Primary Insurance sub-graph:** central to workers-comp IME tracking. Gap G2-07.
8. **RoleAppointmentType gate (which roles can book which types):** still business-relevant?
9. **AppointmentChangeLog field-level audit:** HIPAA compliance blocker?
10. **Seeded AppointmentType values:** OLD has PQME/PQMEREEVAL/AME/AMEREEVAL/ALL/OTHER. Confirm master list + per-type max-time rules.
