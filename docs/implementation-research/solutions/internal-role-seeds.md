# Internal role seeds + structure (ItAdmin / StaffSupervisor / ClinicStaff / Adjuster)

## Source gap IDs

- [DB-16](../../gap-analysis/01-database-schema.md) -- Role seeds for internal users (ItAdmin / StaffSupervisor / ClinicStaff / Adjuster)
- [5-G01](../../gap-analysis/05-auth-authorization.md) -- Role: Adjuster (Claim Examiner variant) with assigned permissions
- [5-G02](../../gap-analysis/05-auth-authorization.md) -- Role: StaffSupervisor
- [5-G03](../../gap-analysis/05-auth-authorization.md) -- Role: ClinicStaff
- [5-G04](../../gap-analysis/05-auth-authorization.md) -- Role: ITAdmin distinct from StaffSupervisor

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs:25-28` seeds 4 external roles (`Patient`, `Claim Examiner`, `Applicant Attorney`, `Defense Attorney`) by calling `EnsureRoleAsync`, which calls `IdentityRoleManager.FindByNameAsync` then `CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, _currentTenant.Id))`. Idempotent.
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs:23` wraps seeding in `using (_currentTenant.Change(context?.TenantId)) { ... }`, so these roles are created per tenant, not host. ABP's built-in `admin` role is seeded at the host level by `Volo.Abp.Identity.IdentityDataSeedContributor`.
- `src/HealthcareSupport.CaseEvaluation.Domain/BookStoreDataSeederContributor.cs:10-47` shows the template-scaffolded `IDataSeedContributor` pattern (root namespace, `[ITransientDependency]`, single `SeedAsync(DataSeedContext)`), which is what DB-16's internal-role contributor will mirror.
- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDbMigrationService.cs:104-114` wires `IDataSeeder.SeedAsync` with host admin email/password properties, then loops tenants at `:58-89` and re-seeds each one with `using (_currentTenant.Change(tenant.Id))`. DbMigrator picks up every `IDataSeedContributor` registered via DI; no list edit required when adding a new one.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs:12-74` registers 62 permission strings under one group `CaseEvaluation` (16 nested classes -- 2 Dashboard host/tenant + 15 entity groups of 4 each). No `.Default` for Dashboard -- Host/Tenant are top-level. These are the only permissions available to grant to any role today.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:1-133` defines the permission string constants. Reading code that wants to bulk-grant permissions will reference, e.g., `CaseEvaluationPermissions.Appointments.Create`.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/MultiTenancy/MultiTenancyConsts.cs:9` sets `IsEnabled = true` -- confirms the tenant loop in `MigrateAsync` will actually execute and re-invoke external seeds per tenant.
- No seed contributor for internal roles exists anywhere in `src/HealthcareSupport.CaseEvaluation.Domain/**/*DataSeed*.cs`. Only 4 files exist: `BookStoreDataSeederContributor`, `ExternalUserRoleDataSeedContributor`, `OpenIddict/OpenIddictDataSeedContributor`, `Saas/SaasDataSeedContributor`.
- `docs/backend/PERMISSIONS.md:197-221` documents the current policy: admin gets ALL; 4 external roles are "empty shells" with permissions "Configured at runtime" via the Permission Management UI. This is the current stated intent -- Q22 revisits it.

## Live probes

- Token + roles list: `POST https://localhost:44368/connect/token` (password grant, admin/1q2w3E*) -> access_token; `GET https://localhost:44327/api/identity/roles` with Bearer header. Proves exactly 5 roles seeded: `admin` + 4 externals. Timestamp 2026-04-24T1255. See [`../probes/internal-role-seeds-2026-04-24T1255.md`](../probes/internal-role-seeds-2026-04-24T1255.md).
- Permission grants for external role: `GET https://localhost:44327/api/permission-management/permissions?providerName=R&providerKey=Patient`. Proves zero granted permissions for the `Patient` role (empty shell). Same log file.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Api/Program.cs:235-244` seeds 7 roles via `SET IDENTITY_INSERT`: (1) ItAdmin, (2) StaffSupervisor, (3) ClinicStaff (Internal tier); (4) Patient, (5) Adjuster, (6) PatientAttorney, (7) DefenseAttorney (External tier). 7 users seeded with password `Admin@123`.
- `P:/PatientPortalOld/patientappointment-portal/src/app/enums/role.ts:2-8` holds the same enum for frontend. `RoleId` is `int`, stored in the custom JWT `Role` claim.
- `P:/PatientPortalOld/PatientAppointment.Infrastructure/Authorization/UserAuthorization.cs` + `patientappointment-portal/src/app/domain/access-permission.service.ts:17-90` enumerate per-role module visibility. The 3 internal roles (ItAdmin, StaffSupervisor, ClinicStaff) have distinct sidebar permission matrices; the 4 external roles collapse into a single `ExternalUserModules` list.
- Track-10 erratum applies: OLD's `dbo.spPermissions` is stubbed to grant-everything in the local bring-up (`P:/PatientPortalOld/_local/fix-permissions.sql`), so the actual PROD-seed role-permission matrix cannot be reconstructed from the local repo. We have role names but not authoritative per-role permission grants. Cited from gap README errata and `05-auth-authorization.md` "Permission resolution (OLD, runtime)" block.
- Renames visible in NEW seeds: OLD `Adjuster` -> NEW `Claim Examiner`; OLD `PatientAttorney` -> NEW `Applicant Attorney` (per WCAB terminology). Cited from `05-auth-authorization.md` "Role mapping" table, lines 288-300.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. Roles are `Volo.Abp.Identity.IdentityRole` entities stored in `AbpRoles`, keyed by `Guid` (not int).
- Row-level `IMultiTenant` (ADR-004): external roles are tenant-scoped (seeded inside `_currentTenant.Change`), `admin` is host-scoped (ABP default). Internal roles, if added as tenant-scoped, will be re-created per tenant as each new doctor-tenant is provisioned.
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003): none of these apply -- seed contributors live in Domain and use `IdentityRoleManager` / `IPermissionManager` directly, bypassing the AppService/Controller layer.
- HIPAA: role names and role IDs are not PHI. Role-permission grants gate PHI access downstream. Misgranting a role (e.g., giving Patient access to AllAppointmentRequest) is a compliance risk, so Q22 policy must be explicitly chosen.
- Capability-specific: the OLD permission matrix is not recoverable from the local bring-up (Track-10 erratum). Any seeded grants are a forward-looking policy, not a port. Q22 wording confirms: "baseline grants out-of-the-box, or rely on admin to assign."

## Research sources consulted

1. ABP Identity Module docs -- `IdentityRoleManager` exposes `CreateAsync`, `FindByNameAsync`, the current seed contributor pattern. URL: `https://abp.io/docs/latest/modules/identity`. Accessed 2026-04-24. Confidence: MEDIUM (page did not expand beyond high-level; source is the canonical docs site).
2. ABP Permission Management Module docs -- `IPermissionManager.SetForRoleAsync(string roleName, string permissionName, bool isGranted)` is the supported convenience API; provider strings `"R"` (role) and `"U"` (user) back the lower-level `SetAsync(permissionName, providerName, providerKey, isGranted)`. URL: `https://abp.io/docs/latest/modules/permission-management`. Accessed 2026-04-24. Confidence: HIGH (both method and provider strings confirmed in the docs).
3. ABP community article on seed contributors -- confirms `IDataSeedContributor` + `ITransientDependency` pattern with tenant-aware `using (_currentTenant.Change(context?.TenantId))` block. URL: `https://abp.io/community/articles/how-to-seed-initial-data-with-data-seed-contributor-7a2y3ev7`. Accessed 2026-04-24. Confidence: MEDIUM (article list surface shown; the in-repo `ExternalUserRoleDataSeedContributor.cs` is the concrete example that matters).
4. Repo evidence: `ExternalUserRoleDataSeedContributor.cs`, `CaseEvaluationDbMigrationService.cs`, `CaseEvaluationPermissionDefinitionProvider.cs`, `docs/backend/PERMISSIONS.md`. Confidence: HIGH (direct source reads).

## Alternatives considered

1. **A1. Admin-only (collapse all internals into `admin`)** -- keep the current 5-role seed; do not add internal role tiers. Document that every internal user gets admin. Minimal code change (zero contributors added). Matches `docs/backend/PERMISSIONS.md:184-193` current-stated intent. **Tag: conditional**. Viable if Adrian answers Q21 "one admin role is acceptable for MVP" -- then this capability reduces to zero work beyond flipping the brief status.
2. **A2. Three-internal-tier split (ItAdmin, StaffSupervisor, ClinicStaff) with empty shells + admin assigns at runtime** -- add an `InternalUserRoleDataSeedContributor` mirroring the external one: seed host-scoped roles, no permission grants. Admin fills in via the Permission Management UI. **Tag: chosen (conditional on Q21=3)**. Matches the pattern already proven in NEW (external contributor). Keeps Q22 a separate decision.
3. **A3. Three-internal-tier split with explicit baseline permission grants seeded** -- same as A2 but also calls `IPermissionManager.SetForRoleAsync(roleName, permissionName, true)` for a curated baseline per tier (e.g., ClinicStaff gets Appointments.*, Patients.*; StaffSupervisor adds Doctors.*, DoctorAvailabilities.*; ItAdmin adds Users, Settings). Same pattern extended to external roles for Q22. **Tag: conditional (requires Adrian choice on Q22 "seeded grants" + per-role permission matrix)**. Effort jumps from S to M-L because the matrix has to be designed, not ported (OLD matrix is not recoverable; track-10 erratum).
4. **A4. Single "internal" role + per-user permission grants** -- one `internal` role, rely on ABP's per-user grant overrides via `IPermissionManager.SetForUserAsync`. **Tag: rejected**. Drifts from ABP's role-centric model and makes per-tier access reviews much harder; no evidence OLD did this.
5. **A5. Delegate entirely to ABP `OrganizationUnits`** -- seed organization units (IT, Supervisors, Staff) instead of roles; assign permissions to OUs. **Tag: rejected**. Over-engineers a foundation-layer concern; OU is a cross-cutting hierarchy, not a role-tier replacement. ABP docs recommend roles for access levels, OUs for org structure. Adds migration + UI work outside MVP scope.

## Recommended solution for this MVP

Branch on Q21.

- **Q21=1 (admin only):** ship current state. No code change. `internal-role-seeds` brief closes as "intentionally collapsed into admin" and Track 5-G02/5-G03/5-G04 are annotated as "absorbed into admin for MVP" in the final wave plan. DB-16 closes to "done -- admin + 4 externals is the MVP role set."

- **Q21=3 (ItAdmin / StaffSupervisor / ClinicStaff):** add ONE new seed contributor: `InternalUserRoleDataSeedContributor.cs` in `src/HealthcareSupport.CaseEvaluation.Domain/Identity/`, implementing `IDataSeedContributor, ITransientDependency`. Inject `IdentityRoleManager` and `ICurrentTenant`. Call three `EnsureRoleAsync` (same shape as external contributor) for `ItAdmin`, `StaffSupervisor`, `ClinicStaff`. Do NOT wrap in `_currentTenant.Change(context?.TenantId)` -- these are internal, host-scoped roles (unlike externals which are per-tenant). No UI change; no migration; no AppService/controller. DbMigrator auto-discovers the contributor via DI and runs it on the host pass of `CaseEvaluationDbMigrationService.MigrateAsync()`.

- **Q22=yes (seed baseline permission grants):** extend the contributor to inject `IPermissionManager` and call `SetForRoleAsync(roleName, permissionName, true)` per baseline cell. Scope per role (proposed baseline):
  - ItAdmin: all CaseEvaluation.* permissions (effectively admin-equivalent, distinct from built-in admin only in that it is explicitly manageable per-tenant).
  - StaffSupervisor: Dashboard.Tenant + all entity `.Default` + `.Create` + `.Edit` (no `.Delete`) across appointment / patient / doctor / doctorAvailability / location / wcaboffice / appointmentaccessor / applicantattorney / appointmentapplicantattorney / appointmentemployerdetail.
  - ClinicStaff: Dashboard.Tenant + Appointments.Default/Create/Edit + Patients.Default/Create/Edit + DoctorAvailabilities.Default (read-only doctor view).
  - Defense Attorney / Applicant Attorney / Claim Examiner / Patient: per the existing matrix in `docs/backend/PERMISSIONS.md:201-219` (read the "Configured at runtime" cells, convert them to explicit `SetForRoleAsync` calls). Patient gets Appointments.Default + Patients.Default only (self-service scope).

- **Q22=no (rely on admin assignment):** leave external roles as empty shells (current). If Q21=3, leave the three new internal roles empty shells as well. Admin configures via Permission Management UI post-DbMigrator.

Effective shape for Q21=3 + Q22=yes path (M effort):

- Entity: none (roles live in ABP `AbpRoles`).
- Domain: new `InternalUserRoleDataSeedContributor.cs` (single file, host-scoped, ~60 lines).
- Application / Controller / Proxy / Angular: no change.
- Migration: none (AbpRoles already exists).
- DbMigrator: auto-discovers the contributor; no wiring change.

No code block over 20 lines. Reference implementation for every pattern is already in-repo at `ExternalUserRoleDataSeedContributor.cs` -- the internal variant differs only in role names and the omission of the `_currentTenant.Change` wrapper.

## Why this solution beats the alternatives

- A1 (admin-only) closes DB-16/5-G01..5-G04 without adding debt only if Adrian confirms Q21=1. If later scope expands, splitting admin users across three roles retroactively is a permission-audit cost. Keeps the door open only if MVP truly doesn't need tiering.
- A2/A3 (chosen) reuse the exact contributor pattern already running in production for externals -- zero new architecture; one new file. Passes the cold-reader test (any dev can read `ExternalUserRoleDataSeedContributor.cs` as template).
- A3 over A2 only if Adrian answers Q22=yes. Effort gap is real: A2 is S (~0.5 day); A3 is M (~2-3 days) because Adrian must author the baseline matrix from scratch (OLD matrix is not recoverable per Track-10 erratum).
- A4 / A5 rejected because they drift from ABP's idioms and add work with no MVP payoff.

## Effort (sanity-check vs inventory estimate)

Inventory says S. Analysis confirms S under Q21=1 or Q21=3+Q22=no (one file, ~60 lines, no migration, no UI). Under Q21=3+Q22=yes the effort rises to M (one file but ~200 lines with the per-role permission matrix authoring + hand-verification of 5 roles x ~50 permission cells). Caller's sanity-check line in the briefing acknowledged this uplift.

## Dependencies

- Blocks:
  - `users-admin-management` (5-G09) -- admin UI shows role dropdowns that need > 1 internal role to be meaningful. If Q21=1, this block is moot.
  - Per-role UI testing across every capability brief (every feature that depends on role-scoped access: appointments, documents, change requests, reports, dashboards). Without seeded roles, the only way to test a non-admin path is to create roles manually in the UI per session.
  - `external-user-home` (UI-16) -- landing-page routing keys off role. If Q21=3 introduces internal landing differentiation (Q22=yes with Dashboard.Host vs Dashboard.Tenant split per internal tier), `external-user-home` consumes the roles.
  - `appointment-request-report-export` and `dashboard-counters` -- both cite permission groups (5-G08 Reports, 03-G08 Dashboard) whose per-role visibility depends on the internal role matrix. Only blocked if the capability adds new permission groups + needs them pre-granted; otherwise safe.
- Blocked by: none. This capability is a foundation-layer brick. Seed contributors run at DbMigrator time before any AppService exists.
- Blocked by open question:
  - Q21: "Internal role structure: one `admin` role, or three distinct (ItAdmin / StaffSupervisor / ClinicStaff)?" (verbatim from `docs/gap-analysis/README.md:254`)
  - Q22: "External role default permissions: add a seed contributor so the 4 external roles have baseline grants out-of-the-box, or rely on admin to assign?" (verbatim from `docs/gap-analysis/README.md:255`)

## Risk and rollback

- Blast radius:
  - Misnaming a role: breaks any `[Authorize(Roles = "StaffSupervisor")]` check downstream. ABP typically uses permission-name authorization, not role-name authorization, so blast is confined to explicit role-name references. Current NEW code (Domain + Application + HttpApi) has zero `[Authorize(Roles = ...)]` attributes -- only permission strings. Low risk.
  - Over-granting a permission at seed time (Q22=yes path): a role may receive access to a tenant's PHI-adjacent data. Catch with a PR-review checklist cross-referencing the permission matrix against the proposed grant. HIPAA risk: MEDIUM until reviewed.
  - Tenant seeding side effects: internal roles seeded at host scope will not duplicate per tenant. If accidentally wrapped in `_currentTenant.Change(context?.TenantId)`, they will duplicate per tenant and every new doctor-tenant creates its own `ItAdmin` role row, which may confuse permission management UIs. The mitigation is explicit: omit the tenant wrapper (opposite of `ExternalUserRoleDataSeedContributor.cs:23`).
- Rollback:
  - Revert the new contributor file on the feature branch; DbMigrator no longer creates the role.
  - Existing rows in `AbpRoles` are not auto-deleted on contributor removal. Manual cleanup via the Identity UI (delete ItAdmin, StaffSupervisor, ClinicStaff) or a targeted SQL script against `AbpRoles` on the dev DB.
  - Permission grants (if seeded under Q22=yes) are in `AbpPermissionGrants` keyed by `(ProviderName='R', ProviderKey=<roleName>)`. If a role is deleted without first deleting its grants, the grants orphan. Rollback script: `DELETE FROM AbpPermissionGrants WHERE ProviderName='R' AND ProviderKey IN ('ItAdmin','StaffSupervisor','ClinicStaff')` -- then delete the roles.
  - No migration to reverse.
  - No data loss: seeded roles hold no PHI.

## Open sub-questions surfaced by research

- Under Q21=3, does "ItAdmin distinct from StaffSupervisor" (5-G04) mean they have identical permissions but different names (audit/provenance), or different permission tiers? OLD had different tiers; NEW has no documented tiering for internals. Needs Adrian call.
- Should the new internal contributor be host-scoped or tenant-scoped? OLD used a single-DB model (tenant-per-DB), so "internal" roles were implicitly per-deployment. NEW's row-level multi-tenancy makes the question live: an ItAdmin seeded at the host level cannot be assigned to a tenant user directly unless the role is explicitly cross-tenant-assignable, which needs an ABP setting toggle. Recommend: seed host-scoped + verify that tenant users can be assigned host roles in the ABP Commercial 10.0.2 Identity UI (HIGH-confidence research needed before implementation; a live probe of the role-assignment dropdown on a tenant user in the NEW UI will confirm in < 5 min).
- Defense Attorney vs Applicant Attorney default permissions differ (Defense gets AppointmentAccessors write, Applicant doesn't, per `docs/backend/PERMISSIONS.md:215-217`). The Q22=yes path therefore needs per-external-role matrices too, not just internal. Scope of this capability brief should stay "internal + Q22 policy"; the per-external matrix belongs in `users-admin-management` if we choose to ship it.
