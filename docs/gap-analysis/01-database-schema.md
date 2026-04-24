# 01 -- Database schema + seeds: Gap Analysis OLD vs NEW

## Summary

OLD Patient Portal's DB surface is wide (64 base tables + 88 view-tables backing stored-procedure-heavy read paths) and dense with stored-proc-driven logic (40 procs stubbed for the local run). NEW's DB surface is narrow (17 business tables, zero procs) and delegates cross-cutting concerns (audit, permissions, files, jobs, settings, tenants) to ABP Framework system tables (49 `Abp*`/`OpenIddict*`/`Saas*`/`Fm*`/`Gdpr*` tables in the Initial migration). The delta is therefore dominated by **feature-level gaps** (documents, change-requests, notes, custom fields, attorneys, joint-declarations) rather than cross-cutting tables. MVP risk rating: **High** -- NEW cannot persist document uploads, change requests, or internal/accessor workflow state that OLD supports end-to-end. 18 MVP-blocking gaps identified; 11 non-MVP gaps; 7 seeded intentional architectural differences confirmed (not counted as gaps).

## Method

**OLD side:**
- Static inventory: `Grep "\[Table\(\"" -g "*.cs"` against `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\` -- 152 matches, 64 base + 88 view-tables (prefix `v`).
- Stored-proc inventory: count of `^CREATE PROCEDURE` in `P:\PatientPortalOld\_local\stub-procs.sql` = 39 + 1 replacement (`dbo.spPermissions`) applied via `P:\PatientPortalOld\_local\fix-permissions.sql` = 40 total installed.
- Column-level evidence: read entity classes directly (User, Role, Patient, Doctor, Appointment, AppointmentChangeRequest, AppointmentDocument, AppointmentStatus, Location, State, AppointmentType).
- Enum inventory: `P:\PatientPortalOld\PatientAppointment.DbEntities\Enums\` (9 enum files).
- Seed behavior: `P:\PatientPortalOld\PatientAppointment.Api\Program.cs:221-334` (`SeedUsers()` method -- 7 Roles via `SET IDENTITY_INSERT`, 1 `ApplicationTimeZone` row, 7 seeded users with password `Admin@123`).

**NEW side:**
- EF Core migration inventory: `Glob W:\patient-portal\development\src\HealthcareSupport.CaseEvaluation.EntityFrameworkCore\Migrations\*.cs` = 14 feature migrations + 1 Initial + designer/snapshot files.
- `Grep "migrationBuilder.CreateTable"` in Initial migration = 50 tables (ABP scaffolding baseline).
- Per-feature migration reads: 14 feature migrations from 2026-01-31 through 2026-03-02.
- Domain enum inventory: `W:\patient-portal\development\src\HealthcareSupport.CaseEvaluation.Domain.Shared\Enums\` -- 5 enums.
- Seed contributors: `Glob "*DataSeed*.cs"` in Domain project -- 4 contributors found.
- Schema reference: `W:\patient-portal\development\docs\database\SCHEMA-REFERENCE.md`.

Timestamp: 2026-04-23.

## OLD version state

### Schemas present

- `dbo` -- 18 base-table entities (Users, Roles, ApplicationModules, GlobalSettings, AuditRecords, etc.)
- `spm` -- 46 base-table entities (Appointments, AppointmentAccessors, AppointmentChangeLogs, AppointmentChangeRequests, AppointmentDocuments, AppointmentDocumentTypes, AppointmentEmployerDetails, AppointmentInjuryBodyPartDetails, AppointmentInjuryDetails, AppointmentJointDeclarations, AppointmentLanguages, AppointmentNewDocuments, AppointmentPatientAttorneys, AppointmentDefenseAttorneys, AppointmentClaimExaminers, AppointmentPrimaryInsurance, AppointmentRequestReports, AppointmentStatuses, AppointmentTypes, AppointmentWorkerCompensations, City, CustomFields, CustomFieldsValues, Doctors, DoctorPreferredLocations, DoctorsAppointmentTypes, DoctorsAvailabilities, Documents, DocumentPackages, DocumentStatuses, Locations, Notes, PackageDetails, Patients, RoleAppointmentTypes, RoleUserTypes, States, SystemParameters, Templates, TemplateTypes, UserQueries, WcabOffices)

**Total OLD base tables = 64**

### View-table classes

88 view-backed entity classes, all prefixed `v`. Per `P:\PatientPortalOld\_local\CHANGELOG.md:134-135`, these were created as **tables (not views)** during bring-up because the repo has no CREATE VIEW DDL.

### Stored procedures

40 stored procedures installed locally as stubs returning empty result sets:
- `dbo.*` (7): `spServerMessages`, `spCanDeleteRecord`, `spPermissions`, `spConfigurationContents`, `spWcabOffice`, `spLocations`, `spAppointmentTypes`, `spAppointmentDocumentTypes`, `spAppointmentChangeRequests`
- `spm.*` (33): numerous `sp*Notification`, `sp*Lookups`, search/report procs.

Source: `P:\PatientPortalOld\_local\stub-procs.sql` + `fix-permissions.sql`.

**Caveat:** the actual PROD schema likely has more procs. 40 is the local stub count, not production's.

### Seed data installed by bring-up

Per `Program.cs:221-334`: 7 Roles + 1 ApplicationTimeZone + 7 Users all with password `Admin@123`. No other seeds (Countries, States, Languages, Locations, AppointmentTypes, AppointmentStatuses, ApplicationObjects all empty).

### Enum inventory (compiled enum -> `ApplicationObjects` row IDs)

| Enum | Values -> ID |
|---|---|
| Status | Active=1, InActive=2, Delete=3 |
| Gender | Male=4, Female=5, Other=30 |
| UserType | InternalUser=6, ExternalUser=7 |
| BookingStatus | Available=8, Booked=9, Reserved=10 |
| RequestStatus | Pending=25, Accepted=26, Rejected=27 |
| AccessType | View=23, Edit=24 |
| PhoneNumberType, AvailableType, CustomFieldType (files present, not read) |

## NEW version state

### Schemas present

Single `dbo` schema (ABP/EF Core default). Business tables use `App` prefix.

### ABP Framework baseline (50 tables from Initial migration `20260131164316_Initial.cs`)

- Identity (9): AbpUsers, AbpRoles, AbpUserRoles, AbpRoleClaims, AbpUserClaims, AbpUserLogins, AbpUserTokens, AbpUserDelegations, AbpLinkUsers
- OpenIddict (4): OpenIddictApplications, OpenIddictScopes, OpenIddictTokens, OpenIddictAuthorizations
- SaaS (3): SaasTenants, SaasEditions, SaasTenantConnectionStrings
- PermissionManagement (3): AbpPermissionGrants, AbpPermissionGroups, AbpPermissions
- SettingManagement (2): AbpSettings, AbpSettingDefinitions
- AuditLogging (5): AbpAuditLogs, AbpAuditLogActions, AbpAuditLogExcelFiles, AbpEntityChanges, AbpEntityPropertyChanges
- BackgroundJobs (1): AbpBackgroundJobs
- FeatureManagement (3): AbpFeatureGroups, AbpFeatures, AbpFeatureValues
- Localization (4): AbpLanguages, AbpLanguageTexts, AbpLocalizationResources, AbpLocalizationTexts
- BlobStoring (2): AbpBlobs, AbpBlobContainers
- OrganizationUnits (3): AbpOrganizationUnits, AbpOrganizationUnitRoles, AbpUserOrganizationUnits
- ClaimTypes (1), Security (2: AbpSecurityLogs, AbpSessions), TextTemplate (3), FileManagement (2: FmDirectoryDescriptors, FmFileDescriptors), GDPR (2: GdprInfo, GdprRequests), AppBooks (1 template)

### Feature migrations (16 business tables)

- `AppStates`, `AppAppointmentTypes`, `AppAppointmentStatuses`, `AppAppointmentLanguages`, `AppLocations`, `AppWcabOffices`
- `AppDoctors` + `AppDoctorAppointmentType` + `AppDoctorLocation` (3 tables)
- `AppDoctorAvailabilities`, `AppPatients`, `AppAppointments`, `AppAppointmentEmployerDetails`, `AppAppointmentAccessors`, `AppApplicantAttorneys`, `AppAppointmentApplicantAttorneys`

**Total NEW business tables = 17** (16 feature + 1 AppBooks template).

### Stored procedures

**Zero.** NEW uses EF Core + LINQ + ABP repositories per `docs/decisions/001-mapperly-over-automapper.md`.

### Seed contributors

4 `IDataSeedContributor` implementations:
1. `BookStoreDataSeederContributor.cs` -- template scaffolding (2 books)
2. `ExternalUserRoleDataSeedContributor.cs` -- 4 external roles: Patient, Claim Examiner, Applicant Attorney, Defense Attorney
3. `OpenIddictDataSeedContributor.cs` -- OAuth clients + scopes
4. `SaasDataSeedContributor.cs` -- tenant + editions

**No** seed contributors for AppStates, AppAppointmentTypes, AppAppointmentStatuses, AppAppointmentLanguages, AppLocations, AppWcabOffices -- all lookup tables are empty by default.

### Enum inventory

| Enum | Values |
|---|---|
| Gender | Male=1, Female=2, Other=3 |
| BookingStatus | Available=8, Booked=9, Reserved=10 |
| AccessType | View=23, Edit=24 |
| PhoneNumberType | (file exists) |
| AppointmentStatusType | 13 states: Pending, Approved, Rejected, NoShow, CancelledNoBill, CancelledLate, RescheduledNoBill, RescheduledLate, CheckedIn, CheckedOut, Billed, RescheduleRequested, CancellationRequested |

## Delta

### MVP-blocking gaps (capability present in OLD, absent in NEW)

| gap-id | capability | evidence-old | evidence-new-absent | rough-effort-to-close |
|---|---|---|---|---|
| DB-01 | Document uploads attached to appointments (S3 paths, status) | `Models\AppointmentDocument.cs:12-133` + 6 companion tables | No Document* table in any NEW migration | L (8-12 story points) |
| DB-02 | Appointment change requests (reschedule / cancel workflow) | `Models\AppointmentChangeRequest.cs:12-112` + child tables | No `AppChangeRequest*` table in NEW | M (5-8 story points) |
| DB-03 | Appointment change log (audit trail of lifecycle transitions) | `Appointment.cs:162` collection + stub proc | ABP has generic `AbpAuditLogs` but no appointment-specific `AppAppointmentChangeLog` | M (3-8 story points depending on reuse) |
| DB-04 | Joint declarations | `Models\AppointmentJointDeclaration.cs` + 3 stub procs | No `AppJointDeclaration*` table | M (5-8 story points) |
| DB-05 | Defense attorney per-appointment linkage | `Models\AppointmentDefenseAttorney.cs` + companion record | NEW has Applicant Attorney only | M (5 story points) |
| DB-06 | Patient attorney per-appointment linkage (distinct) | `Models\AppointmentPatientAttorney.cs` | NEW has only ApplicantAttorney -- combined/renamed? | S-M (verify) |
| DB-07 | Claim examiner linkage | `Models\AppointmentClaimExaminer.cs` | No Claim Examiner table in NEW | M (5 story points) |
| DB-08 | Primary insurance on appointment | `Models\AppointmentPrimaryInsurance.cs` | Not in NEW | S (3 story points) |
| DB-09 | Injury details + body parts | `Models\AppointmentInjuryDetail.cs` + `AppointmentInjuryBodyPartDetail.cs` | Not in NEW | M (5-8 story points) |
| DB-10 | Notes on appointments | `Models\Note.cs` + `Appointment.cs:155` | Not in NEW | S (3 story points) |
| DB-11 | Custom fields per appointment type | `Models\CustomField.cs` + `CustomFieldsValue.cs` + 7 companion view-tables | Not in NEW | L (8-13 story points if in MVP) |
| DB-12 | Templates (pre-fill form defaults) | `Models\Template.cs` + `TemplateType.cs` | Not in NEW | M if MVP |
| DB-13 | Appointment document packages | `Models\DocumentPackage.cs` + `PackageDetail.cs` | Not in NEW | M if MVP |
| DB-14 | Audit records (PHI-sensitive ops audit) | `Models\AuditRecord.cs` + `AuditRecordDetail.cs` + `AuditRequest.cs` | Not in NEW. HIPAA consideration | M (3-5 if ABP suffices) |
| DB-15 | Lookup seed rows (States, Languages, AppointmentTypes, Statuses, Locations, WcabOffices) | PROD-only | No `IDataSeedContributor` for any -- every dropdown shows "No data" | S (3 story points). **Blocker for user-facing testing.** |
| DB-16 | Role seeds for internal users (ItAdmin, StaffSupervisor, ClinicStaff, Adjuster) | `Program.cs:235-244` seeds 7 roles | Only 4 external roles seeded (Patient, Claim Examiner, Applicant Attorney, Defense Attorney) | S (2 story points) |
| DB-17 | Global settings, SMTP, system parameters, FAQ content | `Models\GlobalSetting.cs`, `SMTPConfiguration.cs`, `SystemParameter.cs`, `ConfigurationContent.cs` | ABP `AbpSettings` + `AbpTextTemplateContents` probably sufficient. Intentional arch diff candidate. | S -- just configure |
| DB-18 | User-query table (saved search filters per user) | `Models\UserQuery.cs` | Not in NEW. Non-MVP likely. | Defer |

**MVP-blocking gap count: 18.**

### Non-MVP gaps (nice-to-have)

| gap-id | capability | note |
|---|---|---|
| DB-N1 | Countries + City lookup tables | Fold into States or 3rd-party validator |
| DB-N2 | Application exception log table | ABP `AbpAuditLogs.Exceptions` column. Intentional diff |
| DB-N3 | Application user tokens | ABP `AbpUserTokens`. Intentional diff |
| DB-N4 | Application module/object type tables | ABP `AbpPermissionGroups` + `AbpLocalizationTexts`. Intentional diff |
| DB-N5 | Role permission table | ABP `AbpPermissionGrants`. Intentional diff |
| DB-N6 | Role user-type table | ABP tenant scoping. Intentional |
| DB-N7 | Role appointment-type allowlist | Derive from permissions declaratively |
| DB-N8 | Doctor preferred locations | NEW `AppDoctorLocation` may cover -- verify |
| DB-N9 | 88 view-tables for read-side | NEW uses LINQ + Mapperly. Intentional |
| DB-N10 | Email sender view-tables | Intentional (ABP Emailing) |
| DB-N11 | Lock records (pessimistic-lock) | ABP `ConcurrencyStamp`. Intentional |

**Non-MVP gap count: 11.**

### Intentional architectural differences (NOT gaps)

| Topic | OLD approach | NEW approach | Why different |
|---|---|---|---|
| Multi-tenancy | Tenant-per-database | Row-level `IMultiTenant` | Per docs/decisions/004 |
| Primary keys | `int IDENTITY` | `Guid` via `FullAuditedAggregateRoot<Guid>` | ABP default |
| Auth tables | Custom `Users`, `Roles`, `RolePermissions`, `spPermissions` | ABP `AbpUsers`, `AbpRoles`, `OpenIddict*` | ABP/OIDC standard |
| Soft delete | `StatusId` Active/InActive/Delete | `IsDeleted`, `DeleterId`, `DeletionTime` via `ISoftDelete` | ABP default |
| Audit columns | `CreatedBy/CreatedOn/ModifiedBy/ModifiedOn` (inconsistent names) | `CreationTime/CreatorId/LastModificationTime/LastModifierId` + `ExtraProperties` + `ConcurrencyStamp` | ABP base class |
| Data access | 40+ stored procs + view-tables | LINQ + Mapperly | Per ADR 001 |
| Enum storage | `ApplicationObjects` lookup table | First-class C# enums stored as int | Simpler |
| File storage | S3 paths in string columns | ABP `AbpBlobs` + `FmFileDescriptors` | Pluggable backend |
| Exception logging | `dbo.ApplicationExceptionLogs` | `AbpAuditLogs.Exceptions` | Consolidated |
| Localization | `dbo.Languages` + `LanguageContents` | `AbpLanguages` + `AbpLocalizationTexts` | ABP default |

### Extras in NEW (not in OLD)

- GDPR data-request workflow (`GdprRequests`, `GdprInfo`)
- SaaS tenant management (`SaasTenants`, `SaasEditions`, `SaasTenantConnectionStrings`)
- OpenIddict OAuth2 authorization server with refresh tokens
- Background job queue (`AbpBackgroundJobs`)
- Feature flags (`AbpFeatures`, `AbpFeatureValues`)
- Entity-level change tracking (`AbpEntityChanges` + `AbpEntityPropertyChanges`)
- Organization unit hierarchy
- User delegation (act-as)
- Text templates with versioning
- Concurrency stamps on every aggregate
- 13-state appointment lifecycle enum (vs OLD's 3-state `RequestStatus`)
- `DoctorAvailability` time-boxed with `TimeOnly` type + `BookingStatusId` enum

## Open questions

1. **DB-01 / DB-04 Documents + Joint Declarations:** MVP or deferred? If MVP, confirm ABP `AbpBlobStoring` backend vs S3.
2. **DB-05 / DB-06 Attorney model:** Does NEW's `ApplicantAttorney` subsume both OLD `PatientAttorney` AND `DefenseAttorney`? Seeds imply two roles but only one entity.
3. **DB-03 Change log vs ABP audit:** Is `AbpEntityChanges` enough, or do you need bespoke `AppAppointmentChangeLog`?
4. **DB-11 / DB-12 / DB-13 Custom fields, Templates, Packages:** MVP or deferred?
5. **Redundant enum column:** `AppAppointments.AppointmentStatus` (int enum) AND `AppointmentStatusId` (Guid FK) both exist -- intent?
6. **DB-15 Seeds:** Write `IDataSeedContributor` classes, import PROD snapshot, or defer?
7. **DB-16 Internal roles:** Just `admin`, or reproduce OLD's ItAdmin/StaffSupervisor/ClinicStaff?
8. **Cascade delete strategy:** Written ADR for `SetNull` vs `Cascade` defaults?
9. **PROD schema parity:** Need PROD `sys.tables` + `sys.procedures` dump to confirm no additional tables beyond the 64 documented here.
10. **Tenancy on lookup tables:** `AppStates`, `AppAppointmentTypes`, `AppAppointmentStatuses`, `AppAppointmentLanguages` -- migrations include nullable TenantId but plan says "host-scoped". Reconcile.
