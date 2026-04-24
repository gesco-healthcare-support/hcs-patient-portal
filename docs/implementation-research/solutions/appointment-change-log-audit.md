# Appointment change log + PHI audit trail

## Source gap IDs

- DB-03 -- Appointment change log (audit trail of lifecycle transitions). See `../gap-analysis/01-database-schema.md:126`.
- DB-14 -- Audit records (PHI-sensitive ops, HIPAA). See `../gap-analysis/01-database-schema.md:137`.
- G2-13 -- AppointmentChangeLog field-level diff audit. See `../gap-analysis/02-domain-entities-services.md:197`.
- 5-G06 -- Permission group: `AppointmentChangeLogs`. See `../gap-analysis/05-auth-authorization.md:194`.
- A8-10 -- Angular proxy service: `AppointmentChangeLogs`. See `../gap-analysis/08-angular-proxy-services-models.md:210`.
- R-03 -- Angular route: `/appointment-change-logs`. See `../gap-analysis/07-angular-routes-modules.md:138`.
- UI-01 -- Angular screen: `/appointment-change-logs` audit log viewer. See `../gap-analysis/09-ui-screens.md:139`.
- G-API-09 -- Appointment change logs + search (5 endpoints). See `../gap-analysis/04-rest-api-endpoints.md:130`.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:19` declares the aggregate as
  `FullAuditedAggregateRoot<Guid>, IMultiTenant`. `FullAuditedAggregateRoot<T>` supplies the standard
  creator/modifier/deleter columns (`CreationTime`, `CreatorId`, `LastModificationTime`,
  `LastModifierId`, `IsDeleted`, `DeleterId`, `DeletionTime`, `ExtraProperties`, `ConcurrencyStamp`).
  This satisfies "who last touched the row" but NOT per-field before/after diffs on its own.
- `Appointment.cs` has no `[Audited]` class attribute and no `[DisableAuditing]` on individual
  properties. Without the entity being tagged `[Audited]` (or being registered via
  `AbpAuditingOptions.EntityHistorySelectors`), ABP's entity-history machinery will NOT write
  `AbpEntityChanges` / `AbpEntityPropertyChanges` rows for Appointment mutations. Repo-wide grep for
  `Audited`, `DisableAuditing`, `EntitySelectors` returns no matches in the NEW `src/` tree.
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:175` does
  `Configure<AbpAuditingOptions>(options => options.ApplicationName = "AuthServer");`. That just
  stamps the `ApplicationName` column on `AbpAuditLogs`; it does not enable any entity-history
  selectors. The `HttpApi.Host` module has no equivalent call -- default `ApplicationName` is blank.
- Migration `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260131164316_Initial.cs`
  creates the three ABP audit tables during the initial schema build:
  - `AbpAuditLogs` (line 30) -- request-level envelope: `ApplicationName`, `UserId`, `UserName`,
    `TenantId`, `ExecutionTime`, `HttpMethod`, `Url`, `HttpStatusCode`, `Exceptions`, `ClientIpAddress`.
  - `AbpEntityChanges` (line 748) -- per-entity row: `AuditLogId` (FK), `TenantId`, `ChangeTime`,
    `ChangeType` (tinyint, 0=Created / 1=Updated / 2=Deleted), `EntityTenantId`, `EntityId`
    (nvarchar(128)), `EntityTypeFullName` (nvarchar(128)).
  - `AbpEntityPropertyChanges` (line 1070) -- per-property row: `EntityChangeId` (FK), `NewValue`
    (nvarchar(512)), `OriginalValue` (nvarchar(512)), `PropertyName` (nvarchar(128)),
    `PropertyTypeFullName` (nvarchar(64)). FK cascade-deletes with parent `AbpEntityChanges`.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/packages.lock.json` pulls in the full ABP
  AuditLogging module stack (`Volo.Abp.AuditLogging.Application`, `*.Application.Contracts`,
  `*.Domain`, `*.Domain.Shared`, `*.EntityFrameworkCore`, `*.HttpApi`, all at `10.0.2`). Means the
  read-side `AuditLogAppService` and its Swagger endpoints are wired without any custom code in NEW.
- Angular side: `angular/src/app/app.routes.ts:50-52` already lazy-loads
  `@volo/abp.ng.audit-logging` at route `audit-logs`. `angular/src/app/app.config.ts:22` registers
  `provideAuditLoggingConfig()`. `angular/package.json:37` pins `@volo/abp.ng.audit-logging@~10.0.2`
  and `yarn.lock:2666` resolves it. That route renders ABP's generic audit viewer (entity-type +
  entity-id filter + per-request detail), not an appointment-scoped view.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`
  has no `AppointmentChangeLogs` static nested class (header inventory stops at `Appointments`,
  `AppointmentEmployerDetails`, `AppointmentAccessors`, `ApplicantAttorneys`,
  `AppointmentApplicantAttorneys`). 5-G06 confirmed absent.
- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDbMigrationService.cs` has zero
  references to audit entities or selectors; it only orchestrates migrations and seeders.
- Repo-wide search for `appointment-change-log` in `angular/src/app/**` returns no component,
  service, route, or proxy file. A8-10 (client service) and R-03 (Angular route) confirmed absent.
- No custom `AppointmentChangeLog` or `AuditRecord` entity exists in
  `src/HealthcareSupport.CaseEvaluation.Domain/**`. DB-03 and DB-14 confirmed absent at the
  entity/migration layer.

## Live probes

- Probe 1 (HTTP 200): `GET /api/audit-logging/audit-logs?MaxResultCount=3` with host admin bearer
  returns `{"totalCount":1302,"items":[...]}`. Response shape carries `userId`, `userName`,
  `tenantId`, `executionTime`, `httpMethod`, `url`, `httpStatusCode`, `applicationName`,
  `correlationId`, `entityChanges`, `actions`, `id`. Proves the ABP AuditLogAppService is live and
  has been capturing request envelopes since first service start. Full log in
  `../probes/appointment-change-log-audit-2026-04-24T22-30-00.md`.
- Probe 2 (HTTP 200): `GET /api/audit-logging/audit-logs/entity-changes?MaxResultCount=3` returns
  `{"totalCount":0,"items":[]}`. Endpoint exists and returns the documented shape, but zero rows.
  Proves what the NEW-code read above predicted: no entity is tagged `[Audited]`, so no entity
  changes have ever been recorded, even across 1302 captured requests.
- Probe 3 (HTTP 404): `GET /api/app/appointment-change-logs` returns 404 with the host admin bearer.
  Confirms no appointment-specific audit endpoint exists. Matches the broader Swagger scan
  (`grep -ioE '/api/app/appointment-change[^"]*'` against `/swagger/v1/swagger.json` returns zero
  matches across the 317 total paths).

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentChangeLog.cs:12-75` defines
  `[Table("AppointmentChangeLogs", Schema = "spm")]` with columns `AppointmentChangeLogId` (PK),
  `ChangedById` (nullable), `ChangedDate`, `FieldName` (nvarchar 50, required), `IsInternalUserUpdate`
  (nullable bool), `IsMailSent` (nullable bool), `NewValue` (nvarchar 100), `OldValue` (nvarchar 100),
  `TableName` (nvarchar 50), `AppointmentId` (FK to `Appointments`). One row per field per edit --
  per-field diff granularity matching ABP's `AbpEntityPropertyChanges` shape.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AuditRecord.cs:12-75` defines
  `[Table("AuditRecords", Schema = "dbo")]` with columns `AuditRecordId` (PK), `EventType`
  (nvarchar 9, required), `NewValue`, `OldValue`, `RecordId` (nvarchar 100, required), `RecordName`,
  `TableName`, `AuditRequestId` (FK to `AuditRequests`), `AuditRecordDetails` collection.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AuditRecordDetail.cs:12-60` defines the
  child row: `AuditRecordDetailId` (PK), `ColumnName` (nvarchar 50, required), `NewValue`, `OldValue`,
  `ReferenceTableName`, `AuditRecordId` (FK). Per-column before/after at a finer grain than
  `AppointmentChangeLog`.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AuditRequest.cs` provides the
  per-request envelope (user + timestamp) that parents a batch of `AuditRecord` rows.
- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentChangeLogModule/AppointmentChangeLogDomain.cs:37-47`
  implements `Get` via `EXEC spm.spAppointmentChangeLogs @OrderByColumn, @SortOrder, @PageIndex,
  @RowCount, @UserId`. A stored proc -- an intentional architectural difference in NEW (ADR-001 +
  ADR-003 reject stored procs in favour of LINQ).
- `P:/PatientPortalOld/PatientAppointment.Api/Controllers/Api/AppointmentChangeLog/`
  hosts `AppointmentChangeLogsController.cs` and `AppointmentChangeLogsSearchController.cs`. OLD's
  Angular consumes these at `/appointment-change-logs` (`patientappointment-portal/src/app/
  components/appointment-change-log/appointment-change-logs/appointment-change-logs.routing.ts`).
- Track-10 errata review: no errata apply to this capability. (Track 10 errata cover PDF export,
  SMS, scheduler bug, and CustomField -- not change-log audit.)

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict 44368 / HttpApi 44327 (HTTPS dev cert per
  `../probes/service-status.md`).
- Row-level `IMultiTenant` with doctor-per-tenant (ADR-004). Appointment is tenant-scoped, so any
  audit records written against it inherit the tenant boundary automatically -- `AbpEntityChanges`
  stores `TenantId` AND `EntityTenantId` for that reason.
- Riok.Mapperly (ADR-001), manual controllers + `[RemoteService(IsEnabled = false)]` (ADR-002),
  dual DbContext (ADR-003), no `ng serve` (ADR-005). No stored procs.
- HIPAA applicability: Appointment field values include PHI-adjacent data
  (`PatientId`, `AppointmentDate`, `PanelNumber`, `InternalUserComments`, `AppointmentStatus`).
  HIPAA 45 CFR 164.312(b) requires audit controls that "record and examine activity in information
  systems that contain or use electronic protected health information" -- per-field diff records
  satisfy this directly; ABP's built-in audit chain captures user, timestamp, field name, old
  value, and new value once entities are tagged `[Audited]`.
- Angular audit-logging module already loaded: `@volo/abp.ng.audit-logging@~10.0.2` at route
  `/audit-logs`. No second dependency needed.
- Tenant isolation under doctor-per-tenant: every `AbpEntityChanges` row already carries both the
  audit-log `TenantId` and the `EntityTenantId`, so a cross-tenant viewer leak is blocked by the
  same ABP data filter that protects `Appointment` itself.
- Per-field value column length is 512 (`AbpEntityPropertyChanges.NewValue` / `.OriginalValue`).
  `Appointment.InternalUserComments` is capped at 250; `PanelNumber` at 50; `RequestConfirmationNumber`
  at 50. All well under 512. No truncation risk for the current column set.

## Research sources consulted

- ABP docs -- "Entity History" (permanent article on how `[Audited]` / selectors populate
  `AbpEntityChanges` + `AbpEntityPropertyChanges`): `https://abp.io/docs/latest/framework/infrastructure/audit-logging#entity-history`
  (accessed 2026-04-24). Confirms the opt-in model and the property-level diff semantics used in
  the recommended solution.
- ABP docs -- "Audit Logging" module overview (tables, `AbpAuditingOptions`, selectors,
  `DisableAuditing`): `https://abp.io/docs/latest/framework/infrastructure/audit-logging`
  (accessed 2026-04-24). Confirms the three-table schema, tenant columns, and that audit data is
  written via `IAuditingStore` without requiring new migrations.
- ABP Commercial -- "Audit Logging Module UI" (Angular audit-log route `/audit-logs`, entity-change
  detail modal, permissions `AbpAuditLogging.AuditLogs.*`): `https://abp.io/docs/latest/modules/audit-logging`
  (accessed 2026-04-24). Confirms the Angular route that NEW already has registered at
  `angular/src/app/app.routes.ts:50`.
- HHS / HIPAA Security Rule -- 45 CFR 164.312(b) Audit Controls: `https://www.hhs.gov/hipaa/
  for-professionals/security/laws-regulations/index.html` (accessed 2026-04-24). The regulation
  requires audit hardware/software/procedural mechanisms that record and examine activity in
  systems with ePHI; it does NOT mandate a particular schema, only that activity is recorded and
  examinable. ABP's entity-change table satisfies the rule on its own when Appointment is tagged.
- ABP blog -- "Tracking Entity History with Audit Logging" (worked example showing `[Audited]`
  plus the viewer route): `https://blog.abp.io/abp/tracking-entity-history-in-abp` (accessed
  2026-04-24). Supports alternative A below.
- Microsoft Learn -- Data annotations catalogue (class-level attribute discovery): `https://learn.microsoft.com/
  en-us/dotnet/api/system.attribute` (accessed 2026-04-24). Baseline reference for how the
  `[Audited]` attribute is read reflectively at entity-scan time by ABP's `AbpAuditingOptions`.

## Alternatives considered

- **Alternative A -- Rely on ABP `AbpEntityChanges` + `AbpEntityPropertyChanges` (native audit) and
  reuse the existing `/audit-logs` Angular module, filtered by entity type / entity id -- chosen.**
  Tag `Appointment` and each related child entity with `[Audited]`, bind a permission
  `CaseEvaluation.AppointmentChangeLogs.Default` that gates an `/appointment-change-logs/:id`
  Angular wrapper, and lean on the ABP module already loaded. Zero new entities, zero new
  migrations, zero new server-side endpoints, three-table schema that HIPAA requires.
- **Alternative B -- Port OLD's `AppointmentChangeLogs` + `AuditRecord` + `AuditRecordDetail` +
  `AuditRequest` tables verbatim -- rejected.** Re-creates ABP's capability with a custom schema
  and a custom `DomainService`. Doubles the audit footprint (ABP writes `AbpEntityChanges`
  regardless once `[Audited]` is on -- the custom table would either duplicate or leave ABP disabled
  and then miss non-Appointment PHI). Also reintroduces stored-proc dependency (OLD uses
  `spm.spAppointmentChangeLogs`) which violates ADR-001.
- **Alternative C -- No audit at all for MVP, rely solely on `FullAuditedAggregateRoot` columns --
  rejected.** `FullAuditedAggregateRoot` captures who-last-modified-the-row meta but NOT per-field
  before/after diffs. HIPAA 45 CFR 164.312(b) expects mechanisms that record AND examine activity
  in ePHI systems. Row-level modification stamps alone do not survive a minimum security review.
- **Alternative D -- Custom audit entity that reuses ABP's `IAuditingStore` as the writer --
  rejected.** Technically possible (implement `IEntityChangeHandler` or extend `AuditingStore`),
  but effort explodes while the deliverable is a strict subset of what ABP's built-in pipeline
  already produces once `[Audited]` is applied.
- **Alternative E -- Redact PHI values at store time via `DisableAuditing` on sensitive properties,
  keeping field names only -- conditional.** Worth considering for fields like
  `InternalUserComments` if the stored comment text is ever clinical. Keep in reserve; apply only
  when the clinic reviews the exact comment guidelines.

## Recommended solution for this MVP

- Tag `Appointment` with `[Volo.Abp.Auditing.Audited]` in
  `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs`. Repeat on every
  related transactional entity expected to carry PHI-adjacent data: `AppointmentApplicantAttorney`,
  `AppointmentAccessor`, `AppointmentEmployerDetail`, and the change-request / injury-detail /
  document / note entities once those capabilities land. No migration is required -- the schema is
  already in place from `20260131164316_Initial.cs`.
- Declare a new permission group in
  `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`:
  ```csharp
  public static class AppointmentChangeLogs
  {
      public const string Default = GroupName + ".AppointmentChangeLogs";
  }
  ```
  Register under `CaseEvaluationPermissionDefinitionProvider.cs` with no create / edit / delete
  children because audit records are read-only by design. Satisfies 5-G06.
- Reuse the existing `@volo/abp.ng.audit-logging` module (already at
  `angular/src/app/app.routes.ts:50`) for the generic viewer. For the appointment-specific workflow
  asked for by R-03 / UI-01, add a thin Angular component at
  `angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts` that
  takes an `appointmentId` and calls the existing proxy
  `GET /api/audit-logging/audit-logs/entity-changes?EntityTypeFullName=HealthcareSupport.CaseEvaluation.Appointments.Appointment&EntityId=<guid>`.
  Register the route at `/appointments/view/:id/change-log` (guarded by
  `permissionGuard('CaseEvaluation.AppointmentChangeLogs')`) and surface a link on the
  `appointment-view.component.ts` page.
- No custom AppService, no new controller, no new migration, no new Angular proxy. The only
  server-side code change is the attribute + permission entries. Everything else is an Angular
  feature component plus a router registration.

## Why this solution beats the alternatives

- It satisfies the HIPAA Audit Controls requirement (45 CFR 164.312(b)) with ABP's native pipeline,
  which is already wired and capturing request envelopes today (probe 1 shows 1302 rows).
- It uses zero stored procs and zero hand-rolled SQL, honouring ADR-001 (Mapperly / LINQ over sprocs)
  and ADR-003 (dual DbContext). The audit tables live in both host and tenant DbContexts without
  special configuration because they are ABP infrastructure tables.
- It respects doctor-per-tenant isolation (ADR-004). `AbpEntityChanges.TenantId` stamps the audit
  row with the tenant that mutated the entity; the audit read API honours the same ABP data filter,
  so a doctor-tenant user can never see another tenant's change log.
- Effort drops from inventory's M (3 days) to S-M (2 days) because we skip entity, migration,
  repository, AppService, mapper, controller, and proxy generation. Only attribute wiring,
  permission registration, one Angular component, and one route remain.

## Effort (sanity-check vs inventory estimate)

Inventory says M (3 days -- `../gap-analysis/02-domain-entities-services.md:197`). Analysis
adjusts to **S-M (2 days)**: half-day to tag entities and register permission; one day for the
Angular per-appointment wrapper component, route guard, and link on `appointment-view.component.ts`;
half-day for docs + Swagger proxy regeneration + a smoke test that `AbpEntityChanges` rows appear
after an Appointment update. One-line rationale: zero new entities / controllers / migrations
collapses most of the usual feature scaffolding; the work reduces to attribute wiring + a
consumer-side wrapper.

## Dependencies

- Blocks: nothing. The capability is read-side; no other capability has to wait for it.
- Blocked by: `appointment-state-machine` (only so there is a meaningful stream of transitions to
  audit -- the audit pipeline works today against ad-hoc edits, but G2-13's "lifecycle transitions"
  rely on G2-01's state machine to produce those transitions);
  `internal-role-seeds` (the new `AppointmentChangeLogs` permission needs a role to grant it to --
  typically `ItAdmin`, `StaffSupervisor`, and optionally `ClinicStaff`).
- Blocked by open question: `AppointmentChangeLog custom audit: compliance blocker (HIPAA), or can
  we rely on ABP's generic AbpEntityChanges?` (verbatim `../gap-analysis/README.md:244`). Answer
  proposed here is **rely on ABP's generic AbpEntityChanges**; awaiting Adrian confirmation.

## Risk and rollback

- Blast radius: bounded. Adding `[Audited]` switches an entity from "no entity-history" to "one
  `AbpEntityChanges` row plus N `AbpEntityPropertyChanges` rows per mutation". It writes in the
  same unit-of-work as the entity change via `IAuditingStore`, so a write failure rolls back with
  the transaction. Adding the permission entry without granting it to any role leaves the viewer
  page invisible but the audit stream still writing -- a safe default.
- Rollback: remove the `[Audited]` attribute and re-deploy. `AbpEntityChanges` rows written during
  the ramp remain (correctly -- HIPAA artefacts should not be deleted); historical queries keep
  working. If the permission definition needs to be withdrawn, delete the permission constant and
  the provider registration and re-run `abp generate-proxy`. No migration down step. Feature flag
  alternative: wrap the `[Audited]` attribute roll-out behind a per-entity batch so problematic
  entities can be untagged independently.

## Open sub-questions surfaced by research

- Should `InternalUserComments` on `Appointment` be `[DisableAuditing]` if those comments can
  contain clinical free text? Default: leave auditing ON (the 250-char cap keeps each diff under
  the 512-char `NewValue` column) pending a clinical review. Flag as an Alternative E follow-up.
- Should the per-appointment viewer embed in `appointment-view.component.ts` as a tab or remain a
  dedicated route? Router-first keeps permissions simpler (permissionGuard on a route vs
  `*abpPermission` on a tab), but embedding saves clicks for staff.
- Is a cross-tenant "admin view" of appointment audit rows needed for Gesco support staff? ABP
  allows a host-side user with `AbpAuditLogging.AuditLogs.ViewOtherTenants` permission to read
  across tenants; turning this on bypasses the tenant filter for support -- requires explicit
  sign-off (doctor-per-tenant HIPAA exposure).
