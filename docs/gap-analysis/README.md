# Gap Analysis -- OLD Patient Portal vs NEW Patient Portal

> Master index for the MVP gap inventory. Every per-dimension doc is linked below. Use this page as the one-glance summary for management conversations.

## Executive summary

- **NEW is a substantial feature subset of OLD.** Foundation is solid (ABP Commercial 10 + Angular 20 + OpenIddict + multi-tenant SaaS), but feature coverage lags -- across every track (schema, domain, app services, API, frontend), NEW implements the 10-15 most central entities well and the remaining 40-50 OLD entities/services are either missing or scaffolded without business rules.
- **Single biggest MVP risk**: the 13-state appointment lifecycle enum is defined but unenforced in NEW. `AppointmentManager` is 60 lines with no state-machine guards; slot booking is one-way (create marks `Booked`, delete does not restore `Available`); no reschedule, cancel, check-in, bill, or change-request workflows. OLD's `AppointmentDomain.cs` carries 400+ lines of these rules.
- **Cross-cutting gaps**: email sending (`IEmailSender` replaced with `NullEmailSender` in debug), SMS (no provider), scheduled jobs (module wired, zero consumers), blob storage (module wired, zero consumers), and an accessor-auto-provisioning workflow.
- **NEW's architectural choices are sound and intentional**: ABP's permission system, OpenIddict OAuth2, row-level multi-tenancy, EF Core + LINQ (zero stored procedures), Mapperly source-gen, auto-generated Angular proxies. None of those are gaps; they're upgrades.
- **NEW has 5 security/quality issues that must be fixed before MVP cutover** (found in track 10, not track 1-9): `/appointments/view/:id` has no permission guard; AppService mutations don't enforce Create/Edit/Delete permissions (HTTP callers bypass UI gating); tenant-provisioning is non-transactional (orphans on partial failure); external signup creates Patient rows with junk defaults (hardcoded gender, today's DOB); no HSTS header. These are defects in NEW's own code -- not features to port -- and are MVP-blocking.
- **4 earlier claims were wrong** (track 10 errata): OLD doesn't actually generate PDFs server-side (smaller effort to port), OLD SMS is 100% disabled in transitions (scope shrinks), OLD scheduler has a hardcoded-`1` bug (likely silent production defect), OLD CustomField is fixed-type (simpler to replace via ABP ExtraProperties).

## Per-track documents

| # | Track | Doc | Focus |
|---|---|---|---|
| 1 | Database schema + seeds | [01-database-schema.md](01-database-schema.md) | Table/column/proc/view/seed inventory |
| 2 | Domain entities + services | [02-domain-entities-services.md](02-domain-entities-services.md) | DDD entities, Managers, state machines |
| 3 | Application services + DTOs | [03-application-services-dtos.md](03-application-services-dtos.md) | IAppService ops, DTO shapes, CRUD matrix |
| 4 | REST API endpoints | [04-rest-api-endpoints.md](04-rest-api-endpoints.md) | URL inventory, auth model, shape diffs |
| 5 | Auth + authorization | [05-auth-authorization.md](05-auth-authorization.md) | Login flow, roles, permission tree, multi-tenancy |
| 6 | Cross-cutting backend | [06-cross-cutting-backend.md](06-cross-cutting-backend.md) | Logging, email, SMS, files, jobs, cache, audit |
| 7 | Angular routes + modules | [07-angular-routes-modules.md](07-angular-routes-modules.md) | URL map, guards, lazy modules |
| 8 | Angular proxy, services, models | [08-angular-proxy-services-models.md](08-angular-proxy-services-models.md) | Service inventory, model shapes, shared UI |
| 9 | UI screens per role | [09-ui-screens.md](09-ui-screens.md) | Per-role landing page + nav capture |
| 10 | **Deep-dive findings** (follow-up, 2026-04-23) | [10-deep-dive-findings.md](10-deep-dive-findings.md) | Errata to tracks 1-9 + 7 new gaps + live API observations + applicable ABP research |

> **Read [10-deep-dive-findings.md](10-deep-dive-findings.md) before acting on tracks 1-9.** It corrects 4 material claims in the initial tracks (most notably: OLD does NOT generate PDFs server-side; OLD SMS is fully disabled; OLD scheduler has hardcoded bug; OLD CustomField is fixed-type, not dynamic) and adds 7 new gaps -- 5 MVP-blocking, 2 post-MVP -- focused on NEW's security + quality.

Planning artifacts:

- [00-PLAN.md](00-PLAN.md) -- the plan that drove this analysis
- [00-KICKOFF-PROMPT.md](00-KICKOFF-PROMPT.md) -- the cold-start prompt (for reruns / follow-up)

## Aggregated MVP-blocking gap table

Every row links to the source track for full context.

### Data + Domain

| gap-id | capability | track | severity | source |
|---|---|---|---|---|
| DB-01 | Document uploads attached to appointments (S3 paths, status, rejection notes) | 01 | MVP-blocking | [01 / DB-01](01-database-schema.md) |
| DB-02 | Appointment change requests (reschedule/cancel workflow) | 01 | MVP-blocking | [01 / DB-02](01-database-schema.md) |
| DB-03 | Appointment change log (audit trail of lifecycle transitions) | 01 | MVP-blocking (maybe ABP audit covers) | [01 / DB-03](01-database-schema.md) |
| DB-04 | Joint declarations | 01 | MVP-blocking | [01 / DB-04](01-database-schema.md) |
| DB-05 | Defense attorney per-appointment linkage | 01 | MVP-blocking | [01 / DB-05](01-database-schema.md) |
| DB-06 | Patient attorney per-appointment linkage (distinct from Applicant) | 01 | needs-decision | [01 / DB-06](01-database-schema.md) |
| DB-07 | Claim examiner linkage | 01 | MVP-blocking | [01 / DB-07](01-database-schema.md) |
| DB-08 | Primary insurance on appointment | 01 | MVP-blocking | [01 / DB-08](01-database-schema.md) |
| DB-09 | Injury details + body parts | 01 | MVP-blocking | [01 / DB-09](01-database-schema.md) |
| DB-10 | Notes on appointments | 01 | MVP-blocking | [01 / DB-10](01-database-schema.md) |
| DB-11 | Custom fields per appointment type | 01 | needs-decision | [01 / DB-11](01-database-schema.md) |
| DB-12 | Templates (pre-fill form defaults) | 01 | needs-decision | [01 / DB-12](01-database-schema.md) |
| DB-13 | Appointment document packages | 01 | needs-decision | [01 / DB-13](01-database-schema.md) |
| DB-14 | Audit records (PHI-sensitive ops) | 01 | MVP-blocking (HIPAA) | [01 / DB-14](01-database-schema.md) |
| DB-15 | Lookup seed rows (States, Languages, AppointmentTypes, Statuses, Locations, WcabOffices) | 01 | MVP-blocking (testing blocker) | [01 / DB-15](01-database-schema.md) |
| DB-16 | Role seeds for internal users (ItAdmin / StaffSupervisor / ClinicStaff / Adjuster) | 01 | MVP-blocking | [01 / DB-16](01-database-schema.md) |
| DB-17 | Global settings / SMTP config / system parameters / FAQ | 01 | maybe intentional (ABP Settings) | [01 / DB-17](01-database-schema.md) |
| G2-01 | Appointment state-transition enforcement | 02 | MVP-blocking | [02 / G2-01](02-domain-entities-services.md) |
| G2-02 | DoctorAvailability booking cascade on reschedule/cancel/delete | 02 | MVP-blocking | [02 / G2-02](02-domain-entities-services.md) |
| G2-03 | AppointmentLeadTime / MaxTimePQME / MaxTimeAME / MaxTimeOTHER | 02 | MVP-blocking | [02 / G2-03](02-domain-entities-services.md) |
| G2-04 | Patient auto-match (3-of-6 column fuzzy match) | 02 | MVP-blocking | [02 / G2-04](02-domain-entities-services.md) |
| G2-05 | AppointmentAccessor auto-user-provisioning | 02 | MVP-blocking | [02 / G2-05](02-domain-entities-services.md) |
| G2-06 | AppointmentChangeRequest entity + domain service | 02 | MVP-blocking | [02 / G2-06](02-domain-entities-services.md) |
| G2-07 | AppointmentInjuryDetail + 3 sub-entities (Body Parts + Claim Examiners + Primary Insurance) | 02 | MVP-blocking | [02 / G2-07](02-domain-entities-services.md) |
| G2-08 | AppointmentDocument upload with S3 + verification code | 02 | MVP-blocking | [02 / G2-08](02-domain-entities-services.md) |
| G2-09 | AppointmentPatientAttorney + AppointmentDefenseAttorney separate entities | 02 | needs-decision | [02 / G2-09](02-domain-entities-services.md) |
| G2-10 | Appointment full-field snapshot (CancelledById, RejectedById, reasons, etc.) | 02 | MVP-blocking | [02 / G2-10](02-domain-entities-services.md) |
| G2-11 | Scheduled notification jobs (9 types) | 02 | MVP-blocking | [02 / G2-11](02-domain-entities-services.md) |
| G2-12 | RoleAppointmentType gate | 02 | MVP-blocking | [02 / G2-12](02-domain-entities-services.md) |
| G2-13 | AppointmentChangeLog field-level diff audit | 02 | MVP-blocking (HIPAA) | [02 / G2-13](02-domain-entities-services.md) |
| G2-14 | AppointmentJointDeclaration upload + rejection flow | 02 | MVP-blocking | [02 / G2-14](02-domain-entities-services.md) |

### Application Services + APIs

| gap-id | capability | track | severity | source |
|---|---|---|---|---|
| 03-G01 | AppointmentDocument CRUD + SendDocumentEmail | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G02 | AppointmentDocumentType lookup + CRUD | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G03 | AppointmentNewDocument S3/local upload | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G04 | AppointmentInjuryDetail service + sub-collections | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G05 | AppointmentChangeRequest workflow | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G06 | AppointmentJointDeclaration + approve email | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G07 | AppointmentRequestReport entity service | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G08 | Dashboard counters service | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G09 | Scheduler + 9 notification types | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G10 | Note CRUD | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G11 | CSV/Excel/PDF export (generic) | 03 | MVP-blocking | [03](03-application-services-dtos.md) |
| 03-G12 | CustomField CRUD | 03 | needs-decision | [03](03-application-services-dtos.md) |
| 03-G13 | Template CRUD | 03 | needs-decision | [03](03-application-services-dtos.md) |
| 03-G14 | UserQuery (contact-us) | 03 | non-MVP | [03](03-application-services-dtos.md) |
| G-API-01..21 | 54 OLD endpoints with no NEW counterpart | 04 | MVP-blocking (group) | [04](04-rest-api-endpoints.md) |

### Auth + Permissions

| gap-id | capability | track | severity | source |
|---|---|---|---|---|
| 5-G01 | Role: Adjuster (or Claim Examiner variant) with assigned permissions | 05 | MVP-blocking | [05](05-auth-authorization.md) |
| 5-G02 | Role: StaffSupervisor | 05 | needs-decision | [05](05-auth-authorization.md) |
| 5-G03 | Role: ClinicStaff | 05 | needs-decision | [05](05-auth-authorization.md) |
| 5-G04 | Role: ITAdmin distinct from StaffSupervisor | 05 | needs-decision | [05](05-auth-authorization.md) |
| 5-G05 | Permission group: AppointmentDocuments | 05 | MVP-blocking | [05](05-auth-authorization.md) |
| 5-G06 | Permission group: AppointmentChangeLogs | 05 | MVP-blocking | [05](05-auth-authorization.md) |
| 5-G07 | Permission group: AllAppointmentRequest | 05 | MVP-blocking | [05](05-auth-authorization.md) |
| 5-G08 | Permission group: Reports | 05 | MVP-blocking | [05](05-auth-authorization.md) |
| 5-G09 | Permission group: Users management | 05 | covered by ABP Identity | [05](05-auth-authorization.md) |
| 5-G10 | Permission group: CustomFields | 05 | needs-decision | [05](05-auth-authorization.md) |
| 5-G11 | Permission group: SystemParameters | 05 | maybe ABP Settings covers | [05](05-auth-authorization.md) |
| 5-G12 | Permission group: NotificationTemplates | 05 | needs-decision | [05](05-auth-authorization.md) |
| 5-G13 | Self-service email verification endpoint | 05 | MVP-blocking (ABP covers, verify wired) | [05](05-auth-authorization.md) |
| 5-G14 | Self-service forgot-password flow | 05 | MVP-blocking (ABP covers, verify wired) | [05](05-auth-authorization.md) |

### Cross-cutting

| gap-id | capability | track | severity | source |
|---|---|---|---|---|
| CC-01 | Send appointment reminder emails | 06 | MVP-blocking | [06](06-cross-cutting-backend.md) |
| CC-02 | Send appointment reminder SMS | 06 | MVP-blocking | [06](06-cross-cutting-backend.md) |
| CC-03 | Background notification jobs | 06 | MVP-blocking | [06](06-cross-cutting-backend.md) |
| CC-04 | Blob storage for packet documents | 06 | MVP-blocking | [06](06-cross-cutting-backend.md) |

### Frontend routes + services + screens

| gap-id | capability | track | severity | source |
|---|---|---|---|---|
| R-01..R-10 | 10 OLD Angular routes absent in NEW (rescheduled-requests, cancel-requests, change-logs, appointment-documents, new-documents, document-search, joint-declarations-search, report, upload-documents magic-link, pending-request detail) | 07 | MVP-blocking | [07](07-angular-routes-modules.md) |
| A8-01..A8-13 | 13 Angular client services missing (Notes, Users admin, CustomFields, AppointmentAccessors, AppointmentDocuments both variants, AppointmentNewDocuments S3, InjuryDetails, JointDeclarations both variants, ChangeRequests, ChangeLogs, Dashboard, login account flows, RequestReport PDF) | 08 | MVP-blocking | [08](08-angular-proxy-services-models.md) |
| UI-01..UI-17 | 17 OLD UI screens absent in NEW (audit log, doc search, doc types, reschedule queue, cancel queue, pending detail, appointment search, custom fields, system params, users custom UI, reports, new-documents, anonymous upload, joint-decl search, templates, external home, notes) | 09 | MVP-blocking | [09](09-ui-screens.md) |

**Rough MVP-blocking total**: 100+ individual findings across 9 tracks + 7 new findings in track 10. Once de-duplicated by underlying capability (since each capability surfaces in multiple tracks -- a missing `AppointmentDocument` entity produces gaps in 01, 02, 03, 04, 07, 08, 09), the count collapses to roughly **30-35 MVP-blocking capabilities** plus the 5 new security/quality gaps from track 10.

### Security + quality gaps from track 10 (NEW-side issues)

These are distinct from feature gaps -- they are defects in the NEW code that must be fixed before production, not capabilities to port from OLD.

| gap-id | severity | summary | source |
|---|---|---|---|
| NEW-SEC-01 | MVP-blocking | `/appointments/view/:id` + `/appointments/add` routes only apply `authGuard`, not `permissionGuard`. Any authenticated user (incl. external patients) can access any appointment by crafting the URL. Cross-tenant leak vector. | [10 / NEW-SEC-01](10-deep-dive-findings.md) |
| NEW-SEC-02 | MVP-blocking | Most AppService mutating methods lack method-level `[Authorize(...Create/Edit/Delete)]`. HTTP callers with only `.Default` permission can POST/PUT/DELETE because enforcement is UI-only. | [10 / NEW-SEC-02](10-deep-dive-findings.md) |
| NEW-SEC-03 | MVP-blocking | `DoctorTenantAppService.CreateAsync` runs with `isTransactional: false`. Partial failures after the SaasTenant row is committed leave orphaned tenants + require manual DB cleanup. Tenant-onboarding demo risk. | [10 / NEW-SEC-03](10-deep-dive-findings.md) |
| NEW-SEC-04 | MVP-blocking | `ExternalSignupAppService.RegisterAsync` creates Patient rows with hardcoded `Gender.Male`, `DateOfBirth = today`, `PhoneNumberType.Home`. Data-quality + legal issue for medical data. | [10 / NEW-SEC-04](10-deep-dive-findings.md) |
| NEW-SEC-05 | MVP-blocking | NEW omits `Strict-Transport-Security` header; OLD sends it. HTTPS downgrade vulnerability in production. | [10 / NEW-SEC-05](10-deep-dive-findings.md) |
| NEW-QUAL-01 | MVP-blocking | Zero test coverage for tenant provisioning, permission enforcement, external signup, multi-tenancy filter, or state machines. Silent regression risk between builds. | [10 / NEW-QUAL-01](10-deep-dive-findings.md) |
| NEW-QUAL-02 | non-MVP | `console.log` debug statement in `appointment-add` component. Code-quality sweep item. | [10 / NEW-QUAL-02](10-deep-dive-findings.md) |

### Errata downgrading earlier claims

Track 10 also revised 4 earlier findings. Net effect: 2 gaps shrink in scope, 1 drops to "needs-decision," 1 gets a smaller effort estimate.

| Earlier gap | Correction | Impact |
|---|---|---|
| `G-API-13` / export (CSV/PDF/XLSX) | OLD doesn't actually generate PDFs server-side -- `iTextSharp` is wired but never called. Returns HTML; client uses `window.print()`. Only XLSX is real. | Effort drops from M-L to S-M. Server-side PDF (QuestPDF) becomes optional, not a port requirement. |
| `CC-02` / SMS send | OLD's status-transition SMS is 100% commented out; only 6 of 9 scheduler jobs invoke Twilio and `isSMSEnable: false` in the deployed config. | Severity drops from MVP-blocking to **needs-decision** (confirm with the clinic whether any deployment actually has SMS on). |
| `G2-11` / 9 scheduler jobs | OLD hardcodes `AppointmentId=1` / `UserId=1` in all 9 job dispatches -- likely a pre-existing bug. | Spec the jobs from the stored-proc body, not from caller behavior. |
| `G2-N2` / `03-G12` / CustomField | OLD's custom fields are a fixed 7-type schema, not a dynamic form builder. ABP's `ExtraProperties` + `ObjectExtensionManager.MapEfCoreProperty<T, TProperty>()` is the native replacement. | Effort drops to ~1 day. |

## Post-MVP deferred gaps

These are real gaps -- functionality NEW is missing compared to OLD -- but Adrian has explicitly sequenced them AFTER the ~100 MVP-blocking gaps are closed (decision 2026-04-23). Kept here so they don't get lost.

### Per-tenant branding (BRAND-*)

OLD ships every clinic with a rebranded portal: logo, clinic name, header/footer copy, support email, phone/fax numbers, email-template placeholders all substituted from `PatientAppointment.Api\server-settings.json` + `wwwroot\EmailTemplates\*.html` at deploy time. The frontend reads the same bag via `GET /api/applicationconfigurations/{languageName}` (`ApplicationConfigurationsController.cs:27-51` -> `EXEC dbo.spConfigurationContents @ColumnName`) and populates the login page, navbar, and footer chrome. NEW uses ABP's LeptonX theme with no per-tenant branding hook wired. Note: NEW's tenancy model is row-level `IMultiTenant`, so unlike OLD's "tenant-per-deployment" approach, the branding mechanism needs to be runtime (not build-time).

| gap-id | capability | track | severity | source | rough-effort |
|---|---|---|---|---|---|
| BRAND-01 | Tenant-level branding config (logo, clinic name, phone/fax, support email, header/footer copy) | 05, 06, 09 | post-MVP | [05](05-auth-authorization.md), [06](06-cross-cutting-backend.md), [09](09-ui-screens.md) | M (2-3 days -- ABP `ISettingProvider` per-tenant + admin page) |
| BRAND-02 | Angular theme honors tenant branding at bootstrap (login page, navbar, footer, dashboard header) | 07, 09 | post-MVP | [07](07-angular-routes-modules.md), [09](09-ui-screens.md) | M (2-3 days -- `APP_INITIALIZER` + LeptonX theme slots) |
| BRAND-03 | Email template branding via `AbpTextTemplateContents` + tenant-scoped placeholder substitution | 06 | post-MVP | [06](06-cross-cutting-backend.md) | S (1-2 days, after CC-01 email sending is in) |

Total: roughly 5-7 days of work after the underlying infrastructure (ABP Settings, email sending, distributed cache) is in. Not a heavy lift -- a missed port, not a missed architecture.

## Intentional architectural differences summary

These are NOT gaps. They are design upgrades in NEW. The per-track docs flag them as "intentional differences" so they don't pollute MVP scope.

| Topic | OLD | NEW | Why NEW is different |
|---|---|---|---|
| Multi-tenancy | Tenant-per-database; DbServer + CompanyName in JWT claims | Row-level `IMultiTenant` with auto-filter | Operational simplicity (per ADR 004) |
| Auth server | Custom JWT via `Rx.Core.Security.Jwt.dll` | OpenIddict on port 44368, OAuth 2.0 / OIDC | Industry-standard OIDC |
| Permissions | `dbo.spPermissions` returns hand-crafted JSON tree | ABP `PermissionDefinitionProvider` + `[Authorize]` attributes | Declarative, code-first |
| Data access | 40+ stored procs + `spm.v*` view-tables | ABP `IRepository<T>` + LINQ + Riok.Mapperly source-gen | Testable, refactorable (per ADR 001) |
| Frontend build | Angular 7 with `ng serve` + in-tree `@rx/*` monorepo | Angular 20 standalone + `ng build` + `npx serve` + auto-generated proxy | Modern tooling (per ADR 005) |
| Controller wiring | Manual MVC controllers embed Uow + Domain directly | Manual controllers + `[RemoteService(IsEnabled = false)]` on each AppService | Per ADR 002 -- explicit routing |
| Mapping | Manual property copies in domain services | Riok.Mapperly `[Mapper]` source-generated | Compile-time errors (per ADR 001) |
| Primary keys | `int` identity columns | `Guid` via `FullAuditedAggregateRoot<Guid>` | Multi-tenant merges, offline scenarios |
| Soft delete | `StatusId` Active/InActive/Delete enum | `IsDeleted` + `DeleterId` + `DeletionTime` (`ISoftDelete`) | ABP default |
| Audit columns | `CreatedById`, `CreatedDate`, `ModifiedById`, `ModifiedDate` (inconsistent names) | `CreationTime`, `CreatorId`, `LastModificationTime`, `LastModifierId`, `DeleterId`, `DeletionTime`, `ExtraProperties`, `ConcurrencyStamp` | ABP base class |
| Localization | `dbo.Languages` + `LanguageContents` + `spConfigurationContents` | `AbpLanguages` + `AbpLocalizationTexts` + `/abp/application-localization` | ABP standard |
| File storage | S3 paths stored in string columns | ABP BlobStoring + FileManagement modules | Pluggable backends |
| Attorney model | Two entities (PatientAttorney + DefenseAttorney) + 17 firm-detail cols each | Unified `ApplicantAttorney` + `AppointmentApplicantAttorney` join | Potentially intentional consolidation -- confirm |
| Schema namespace | `dbo` (identity/lookups) + `spm` (business data) | Single `dbo` | Simpler ops |
| Lookup shape | `IQueryable<v*LookUp>` view rows per entity | Uniform `LookupDto<Guid> { Id, DisplayName }` across all lookups | Consistent dropdown pattern |
| View projections | 88 `v*` TS classes mirroring SQL views/stored procs | `<Entity>WithNavigationPropertiesDto` via Mapperly | Stored-procs gone |
| Exception logging | `dbo.ApplicationExceptionLogs` bespoke table | `AbpAuditLogs.Exceptions` column + Serilog | Consolidated logging |

## Extras in NEW (not in OLD)

Summary of genuinely NEW capabilities:

- **GDPR workflow**: data-request + privacy/cookie consent pages (`/gdpr`, `/gdpr-cookie-consent/*`)
- **SaaS tenant management**: `SaasTenants`, `SaasEditions`, `SaasTenantConnectionStrings`; `/saas/**` admin UI
- **OpenIddict OAuth2**: refresh tokens, PKCE, `/connect/token`, `/connect/authorize`, `/connect/endsession`, `/connect/userinfo`, discovery doc, Swagger OAuth2 integration, impersonation + link-login grants, client-credentials grant
- **ABP platform modules**: FeatureManagement, AuditLogging (generic), FileManagement, TextTemplateManagement, LanguageManagement, SettingManagement, OrganizationUnits, Identity, Account
- **Health checks** (`/health-status`, `/health-ui`)
- **Data protection** via Redis in non-dev
- **Distributed locking** via Medallion.Threading.Redis
- **Dashboard.Host / Dashboard.Tenant permission split**
- **WCAB office CRUD with Excel export + download-token CSRF pattern**
- **Doctor bulk slot generation with preview** (`/doctor-availabilities/preview`, `/generate`, `/add`)
- **Patient self-service profile** (`/doctor-management/patients/my-profile`)
- **Appointment `view/:id` separate from edit** (OLD conflates)
- **External signup with tenant selection** (ExternalSignupController, `/api/public/external-signup/*`)
- **DoctorTenantAppService.CreateAsync**: provisions tenant + admin user + Doctor entity in a single atomic call
- **UserExtendedAppService**: syncs Doctor entity when admin edits IdentityUser
- **UpsertApplicantAttorneyForAppointmentAsync**: atomic create-or-update-and-link on booking
- **Concurrency stamps** on every aggregate
- **Appointment.IdentityUserId** separate from Patient.IdentityUserId (supports attorney-books-for-patient)
- **All with-navigation-properties/:id variants** (eager-load in one round-trip)
- **Bulk delete endpoints** on AppointmentStatuses, Locations, WcabOffices (`/all` + collection DELETE)
- **Proper TS enums** in `proxy/enums/*.enum.ts` (OLD used const objects)

## Consolidated open questions for Adrian

Every track produced questions that need Adrian (or management) input before MVP scope can be finalized. Grouped here:

### Feature-scope decisions (Adrian needs to answer before implementation planning)

1. **Defense Attorney as first-class entity?** OLD has it, NEW has only ApplicantAttorney + the role name. If yes, add `AppointmentDefenseAttorney` entity + service + UI. (Tracks 2, 3, 9)
2. **Patient Attorney vs Applicant Attorney:** same concept renamed, or two distinct roles OLD had that NEW conflates? (Tracks 2, 3)
3. **Claim Examiner sub-entity on Appointment:** required for workers-comp tracking? (Track 2)
4. **Primary Insurance, Injury details (incl. body parts), WorkerCompensation:** required for workers-comp IME? (Track 2)
5. **Is MVP meant to enforce the 13-state lifecycle** or leave it advisory (current NEW state)? (Tracks 2, 3)
6. **CustomField dynamic form builder**: port from OLD, or drop? (Tracks 1, 2, 3, 9)
7. **Template management**: port from OLD or use ABP TextTemplateManagement? (Tracks 1, 2, 3, 6, 9)
8. **SystemParameter**: keep as entity or delegate to ABP Settings Management? (Tracks 1, 3, 9)
9. **Document Packages + Package Details**: required for MVP? (Tracks 1, 2, 3, 4, 9)
10. **Notes on appointments (parent/child thread)**: required? (Tracks 1, 2, 3, 4, 9)
11. **UserQuery / contact-us**: required? (Tracks 2, 3, 4, 9)
12. **Report search page + PDF/Excel export**: required? Which formats? Per-entity (ABP pattern) or generic (OLD pattern)? (Tracks 3, 4, 9)
13. **Dashboard counters**: which of OLD's 13 cards are needed, per role? (Tracks 2, 3, 4, 9)
14. **AppointmentChangeLog custom audit**: compliance blocker (HIPAA), or can we rely on ABP's generic `AbpEntityChanges`? (Tracks 1, 2, 3, 9)
15. **Anonymous upload via magic link** (OLD `/upload-documents/:id/:type`): required for external attorneys/patients to submit documents without login? (Tracks 4, 7, 9)
16. **Email verification + forgot password self-service**: confirm ABP Account Module is wired and tested. (Track 5)

### Architecture decisions

17. **Storage provider for blobs**: DB BLOB (ABP default, works now) or S3 (OLD parity, needs creds)? (Tracks 1, 2, 3, 6)
18. **Background jobs**: Hangfire/Quartz add-on vs ABP's one-shot `IAsyncBackgroundJob`? (Tracks 2, 3, 6)
19. **Token lifetime**: 12h OLD vs 1h access + 14d refresh (OIDC default) -- management preference? (Track 5)
20. **Single-device login**: OLD enforces by deleting all prior tokens on login. Required for MVP? (Track 5)
21. **Internal role structure**: one `admin` role, or three distinct (ItAdmin / StaffSupervisor / ClinicStaff)? (Track 5)
22. **External role default permissions**: add a seed contributor so the 4 external roles have baseline grants out-of-the-box, or rely on admin to assign? (Track 5)
23. **Seed data for lookup tables**: write `IDataSeedContributor` classes for States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, Locations, WcabOffices, or import PROD snapshot? (Track 1)
24. **Cascade delete ADR**: write it, to document `SetNull` for optional parents + `Cascade` on M2M joins? (Track 1)

### Security / compliance

25. **Anonymous endpoints in OLD** (DocumentUpload/Download/CsvExport/Dashboard/Scheduler): intentional or legacy bug? (Track 4)
26. **Secrets in `server-settings.json`** (AWS + Twilio): rotate before anything touches production. (Adrian already acknowledged)
27. **OLD `DocumentDownloadController` path-traversal** (weak `..` filter at line 33): do NOT replicate this pattern. (Track 3)

### Process / confirmation

28. **PATCH verb parity**: does Angular 7 client actively use PATCH? Grep before deciding to drop. (Track 4)
29. **PROD schema parity**: can Adrian provide a PROD `sys.tables` + `sys.procedures` list to confirm no OLD tables/procs are missing from this analysis (local bring-up had no proc scripts so we inferred from usage). (Track 1)
30. **Live Swagger reachability**: confirm `http://localhost:44327/swagger/v1/swagger.json` is stable so follow-up passes can use it. (Track 4)
31. **Track 9 full coverage**: a follow-up capture run is needed to get every reachable screen per role; the nav inventory in [09-ui-screens.md](09-ui-screens.md) is the navigation map.
32. **Book demo feature** (ABP scaffolding `/api/app/book/*` + related UI): safe to remove from NEW? (Tracks 1, 4)

## Reproduction cheat sheet

Every claim in the per-track docs cites `file:line` or an explicit command. Use these to re-run specific parts of the analysis.

### OLD schema + DB inspection

```bash
# List all OLD tables
sqlcmd -S "(localdb)\MSSQLLocalDB" -d PatientPortalOld_Main \
  -Q "SELECT s.name+'.'+t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id ORDER BY s.name, t.name" -h -1

# List all OLD stored procs
sqlcmd -S "(localdb)\MSSQLLocalDB" -d PatientPortalOld_Main \
  -Q "SELECT s.name+'.'+p.name FROM sys.procedures p JOIN sys.schemas s ON p.schema_id=s.schema_id ORDER BY s.name, p.name" -h -1

# List databases on LocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT name FROM sys.databases" -h -1

# OLD entity inventory
grep -rEn "\[Table\(\"" P:/PatientPortalOld/PatientAppointment.DbEntities/Models/*.cs
```

### NEW API introspection

```bash
# NEW Swagger JSON
curl -s http://localhost:44327/swagger/v1/swagger.json > /tmp/new-swagger.json

# NEW OIDC discovery
curl -s http://localhost:44368/.well-known/openid-configuration

# NEW auth endpoint (login probe)
curl -s -X POST http://localhost:44368/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=admin&password=1q2w3E*&client_id=CaseEvaluation_App&scope=CaseEvaluation openid offline_access"
```

### OLD auth + login probe

```bash
curl -s -X POST http://localhost:59741/api/userauthentication/login \
  -H "Content-Type: application/json" \
  -d '{"emailId":"admin@local.test","password":"Admin@123"}'
```

### File-system inventory

```bash
# OLD controllers
find P:/PatientPortalOld/PatientAppointment.Api/Controllers -name "*.cs" | wc -l
# OLD domain services
find P:/PatientPortalOld/PatientAppointment.Domain -name "*Domain.cs" | wc -l
# NEW app services
find W:/patient-portal/development/src/HealthcareSupport.CaseEvaluation.Application -name "*AppService.cs" | wc -l
# NEW managers
find W:/patient-portal/development/src/HealthcareSupport.CaseEvaluation.Domain -name "*Manager.cs" | wc -l
# NEW migrations
ls W:/patient-portal/development/src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/*.cs | grep -v Designer | grep -v Snapshot
```

### Browser state for Track 9 capture

```bash
# URLs
open http://localhost:4201/        # OLD Angular (port 4201)
open http://localhost:4200/        # NEW Angular (port 4200)
open http://localhost:59741/       # OLD API (HTTP)
open http://localhost:44327/       # NEW API (HTTP despite cert config)
open http://localhost:44368/.well-known/openid-configuration   # NEW OIDC

# OLD test credentials (all Admin@123)
# admin@local.test, supervisor@local.test, staff@local.test,
# patient@local.test, adjuster@local.test, patatty@local.test, defatty@local.test

# NEW test credentials
# admin / 1q2w3E*
```

### Running state

All 5 services are running on this machine:

```
OLD Angular      http://localhost:4201        HTTP 200
OLD API          http://localhost:59741       HTTP 406 (JWT required, live)
NEW Angular      http://localhost:4200        HTTP 200
NEW API Host     http://localhost:44327       HTTP 200 on /swagger/index.html
NEW AuthServer   http://localhost:44368       HTTP 200 on /.well-known/openid-configuration
```

LocalDB databases on `(localdb)\MSSQLLocalDB`:
- `PatientPortalOld_Main` (48 tables + 40 stored procs + 7 seeded users)
- `PatientPortalOld_Log`
- `PatientPortalOld_Cache`
- Plus a NEW DB likely named `CaseEvaluation` or similar -- probe with `SELECT name FROM sys.databases`.

## Next steps (suggested order)

1. **Adrian reviews this README + consolidated open questions** (section above).
2. **Adrian batches answers to the 32 open questions** in a single pass -- that turns "needs-decision" findings into "MVP" or "defer".
3. **Per-gap research sessions**: for each MVP-confirmed gap, Adrian opens a targeted session with the per-track doc as context and writes an implementation plan. The per-track docs are intentionally written so each gap row has enough evidence for that session to start without re-doing the analysis.
4. **Seeder work first**: gap DB-15 (lookup seeds) and DB-16 (internal roles) should be the first implementation items -- they unblock walkable-UI and per-role testing for subsequent gaps.
5. **State-machine work next**: G2-01 + G2-02 (13-state lifecycle enforcement + slot cascade) is the biggest architectural piece and gates most other appointment workflows (change requests, reschedule, cancel, check-in, bill).

---

Generated 2026-04-23 as part of the OLD-vs-NEW gap analysis. See `00-PLAN.md` for the plan that drove this analysis, `00-KICKOFF-PROMPT.md` for the cold-start prompt (for re-runs or continuing sessions).
