---
feature: internal-roles-access-model
date: 2026-06-16
status: in-progress
base-branch: feat/internal-user-pages
related-issues: []
supersedes-confusion: live-test findings on feat/redesign-internal-admin (2026-06-16)
---

## Goal

Make the three internal roles -- IT Admin, Staff Supervisor, Intake Staff -- have a
single, clear, conflict-free permission + tenancy model: two Gesco-side host roles that
switch into any clinic (IT Admin = technical/all, Staff Supervisor = business) and one
clinic-local operational role (Intake Staff), with the cross-tenant switch actually wired
and working.

## Context

- During live-testing the merged Prompt 16 work we hit "complex conflicts": pages 403'd or
  hid inconsistently. Root cause (confirmed in code, not assumed): the seed
  (`InternalUserRoleDataSeedContributor`) grants the custom roles only `CaseEvaluation.*`
  permissions and never the ABP framework permissions, AND IT Admin is host-scoped while
  Staff Supervisor + Intake are per-tenant roles. So "Supervisor switches between clinics"
  did not fit the box Supervisor was built into, and IT Admin could not reach Roles / Audit
  / Files / Languages.
- Decided model (Adrian, 2026-06-16, via stepwise Q&A): IT Admin + Staff Supervisor are
  Gesco-side **host** roles that switch into clinics; Intake Staff is **clinic-local**.
  IT Admin = everything (technical). Staff Supervisor = all business + System Parameters
  edit + Audit read-only. Intake = operations only, no config screens (keeps the
  behind-the-scenes reads the booking flow needs).
- Tenancy reality (verified): row-level isolation is real -- Patients, Doctors,
  Appointments and all case data are `IMultiTenant` (private per clinic). Some lookup
  masters (Locations, and similar) are host-shared (NOT `IMultiTenant`) -- a deliberate
  shared-master pattern, flagged for the phased multi-tenant work, not this phase.
- The "switch into clinic" button today only navigates to the `{slug}.localhost`
  subdomain; it is NOT real impersonation. ABP tenant impersonation IS configured
  (`SaasHostPermissions.Tenants.Impersonation`, `IdentityPermissions.Users.Impersonation`
  in the AuthServer module) but not wired to the button. Making the switch actually work is
  in scope.
- Constraints: ABP rejects host-scoped grants for `MultiTenancySides.Tenant`-only
  permissions (e.g. `Dashboard.Tenant`); most operational permissions are `Both` and so are
  grantable at host scope. HIPAA: patient data stays isolated (already true). Never break
  the booking flow for Intake. The seed must stay idempotent.

## Decided access model (authoritative reference)

Legend: Full = view/create/edit/delete (+ that group's special actions); View+ = view plus
the noted actions; read(booking) = silent read the booking flow needs, no management
screen; None.

### Business permissions (`CaseEvaluation` group)

| Permission group | IT Admin | Staff Supervisor | Intake Staff |
| --- | --- | --- | --- |
| Dashboard | Full (host) | Full | View (tenant) |
| Appointments (+Approve/Reject) | Full | Full | View+Create+Edit+Approve+Reject |
| Appointment sub-records (employer, accessors, AA/DA links, injuries, body parts, examiners, insurances) | Full | Full | View + booking creates |
| Change Requests (+Approve/Reject) | Full | Full | View only |
| Change Logs (read-only) | View | View | View |
| Patients (+Reveal SSN) | Full | Full | View+Create+Edit + Reveal SSN |
| Applicant/Defense Attorneys, Claim Examiners | Full | Full | View (+create via booking) |
| Doctor Availabilities (slot grid) | Full | Full | Full |
| Doctors, Locations, WCAB Offices | Full | Full | read(booking) |
| Appointment Documents (+Approve) | Full | Full | View+Approve |
| Packets (+Regenerate) | View+Regenerate | View+Regenerate | View+Regenerate |
| Reports (+Export) | Full | Full | Full |
| Config lookups: Appt Types, Statuses, Doc Types, Languages, States | Full | Full | read(booking) |
| Custom field config | Full | View+Create+Edit | read(booking) |
| Notification Templates (+Edit) | Full | Full | None |
| System Parameters (+Edit) | Full | Full (edit) | read(booking)* |
| Invite external users | Full | Full | Full |
| Create internal users | Full | Full | None |
| User signatures (own) | Full | Full | Full |
| Document/Package master catalog | Full | None | None |
| `Books` (leftover ABP sample entity) | remove | remove | remove |

\* Intake gets no System Parameters screen; if booking validation needs the values at
runtime, that read happens through the booking AppService, not a granted UI permission --
verified in T6.

### Technical / framework permissions

| Permission | IT Admin | Staff Supervisor | Intake Staff |
| --- | --- | --- | --- |
| Switch into clinic (`Saas.Tenants.Impersonation`) | Yes | Yes | No |
| Roles / Permission Matrix (`AbpIdentity.Roles` + ManagePermissions) | Full | None | None |
| Manage users (`AbpIdentity.Users`) | Full | via Internal Users only | None |
| Audit Logs (`AuditLogging.AuditLogs`) | Full | Read-only | None |
| File Management (`FileManagement.*`) | Full | None | None |
| Language Management (`LanguageManagement.Languages`) | Full | None | None |
| Tenant provisioning (`Saas.Tenants` create/edit) | Full | None | None |
| Editions / Features (`Saas` / `FeatureManagement`) | Full | None | None |

### Deltas from today
- IT Admin: ADD `AbpIdentity.Roles`(+ManagePermissions), `AuditLogging.AuditLogs`,
  `FileManagement.*`, `LanguageManagement.Languages` (currently missing -> caused the 403s).
- Staff Supervisor: MOVE to host scope; ADD `SystemParameters.Edit`, `AuditLogging`
  (read-only), `Saas.Tenants.Impersonation`.
- Intake Staff: REMOVE config-management, Notification Templates, System Parameters screen,
  internal-user create; keep operational set + booking reads.

## Approach

- Chosen: two host roles (IT Admin, Staff Supervisor) + one tenant role (Intake), with
  cross-tenant access via an ABP-native switch. This matches Gesco's structure (Gesco staff
  manage multiple client clinics; clinic front-desk is local) and the ABP host/tenant split.
- The one genuine unknown is HOW a host Staff Supervisor's permissions resolve when they
  enter a clinic. Three candidate mechanisms, to be settled by the T1 spike:
  - alpha. ABP tenant impersonation (`Saas.Tenants.Impersonation`): logs the host user into
    the clinic as the clinic's admin -> FULL tenant access. Breaks the business/technical
    split for Supervisor. Acceptable only for IT Admin.
  - beta. ABP user impersonation (`Identity.Users.Impersonation`) of a per-clinic
    role-holder: preserves the split but needs a target user per clinic.
  - gamma. Host role carrying `Both`-sided business permissions + a tenant selector that
    sets the active tenant context for the host user's session (no re-login). Preserves the
    split cleanly; `Tenant`-only permissions (e.g. `Dashboard.Tenant`) do not apply, so use
    `Dashboard.Host` or a host-side equivalent. Likely cleanest for the split.
- Rejected: "Supervisor gets a separate account in each clinic" (Adrian rejected -- no
  single identity, duplicate users). Rejected: leaving the seed role-only (the status quo
  that produced the conflicts).
- Spike-first: T1 verifies ABP's actual behavior against docs + source and picks alpha /
  beta / gamma BEFORE the switch-wiring + Supervisor-scope tasks, because the choice changes
  T3/T4. Plan tasks below are written for the likely outcome (IT Admin = alpha full
  impersonation; Supervisor = gamma or beta to keep business-only) and will be adjusted if
  the spike says otherwise.

## T1 spike outcome (2026-06-16) -- mechanism decided

Verified against decompiled ABP 10.0.2 source + official docs + the repo (spike
`wf_3bdc4d2a-5e0`; adversarial verdict: go-with-changes). Findings:

- IT Admin clinic-switch = ABP **tenant impersonation** (`Saas.Tenants.Impersonation`,
  stock `AbpAccountPublicWebImpersonationModule`, Angular `ImpersonationService
  .impersonateTenant(tenantId, 'admin')`). Logs in AS the tenant `admin` user = full
  access = correct for IT Admin. The grant must be ADDED (IT Admin has `Saas.Tenants` +
  `.Create`, not `.Impersonation`). SOUND -- ship.
- Staff Supervisor CANNOT be a host role with a business-only clinic-switch via stock ABP:
  tenant impersonation is all-or-nothing (full tenant-admin); user impersonation is
  within-tenant (a host user cannot one-hop into a tenant user -- confirmed in decompiled
  `ImpersonateUserModel` + ABP support #2765). The only real options are (A) host-role +
  per-AppService `ICurrentTenant.Change` wrapping (broad refactor, silent-403 risk) or (B)
  a custom token-minting endpoint (non-stock, privilege-escalation surface). Both heavy.
- DECISION (Adrian, 2026-06-16): **DEFER** Supervisor cross-clinic switching. Supervisor
  STAYS per-tenant (already business-only) with two grant additions (`SystemParameters.Edit`
  + `AuditLogging.AuditLogs` read); fully functional within its clinic. Cross-clinic
  switching becomes a dedicated, security-reviewed follow-up (Option A preferred per the
  adversarial review). Supervisor keeps `InternalUsers.Create/.Edit` (confirmed:
  business-admin-manages-staff).
- Verified framework permission names: `AbpIdentity.Roles`,
  `AbpIdentity.Roles.ManagePermissions`, `AuditLogging.AuditLogs`,
  `FileManagement.FileDescriptor`, `LanguageManagement.Languages`,
  `Saas.Tenants.Impersonation`, `Saas.Tenants.Create`.

### Revised Phase-1 scope (supersedes the Supervisor host-move below)
- T2 -- seed: IT Admin += {AbpIdentity.Roles, .ManagePermissions, AuditLogging.AuditLogs,
  FileManagement.FileDescriptor, LanguageManagement.Languages, Saas.Tenants.Impersonation};
  Staff Supervisor += {SystemParameters.Edit, AuditLogging.AuditLogs}. Idempotent. Unit-test
  the grant sets.
- T3 -- DROPPED this phase (no Supervisor host-scope move).
- T4 -- IT Admin only: wire `ImpersonationService.impersonateTenant(tenantId, 'admin')` into
  the Tenants-hub switch (replaces subdomain navigation).
- T5 -- nav/gating mostly auto-resolves once IT Admin holds the framework grants + Supervisor
  holds Audit; verify + minor tweaks.
- T6 -- Intake: verify current grants already match the matrix (operational + functional
  reads; keep `SystemParameters.Default` as a behind-the-scenes read, no screen via nav).
- T7 -- `Books` sample-entity cleanup.
- NEW follow-up plan (separate): "Supervisor cross-clinic switching" via Option A.

## Phase 1 implemented + live-verified (2026-06-16)

Branch `feat/internal-roles-access` off `feat/internal-user-pages`. Commits:
- `e4d1d39` feat(identity): IT Admin framework grants + Supervisor sysparams/audit.
- `9faa2d6` feat(users): IT Admin clinic-switch via ABP tenant impersonation.
- `95d1dd4` fix(identity): IT Admin full File + Language management children
  (DirectoryDescriptor + action children -- found via live verification).
- `6dac3fb` fix(internal-shell): scope-aware admin hub (hide tenant-scoped sections
  at host) + reactive host scope (nav follows impersonation).
- Grant sets pinned by 34 Domain.Tests; reseed clean twice (no side-mismatch).

Live-verified as IT Admin (admin.localhost):
- Users & Roles (Permission Matrix, 213 perms), Audit Logs, File Management, Languages
  all reachable (the framework grants fixed the 403s).
- "Switch into clinic" -> ABP tenant impersonation: token scoped to Falkinstein
  (`tenant=b854b96f-...`), signed in as the tenant `admin`, `impersonatorUserId` set
  (audit trail). After switch, the shell flips to the full tenant nav and tenant data
  (System Parameters) loads. Zero console errors.

Pinned by unit tests + reseed (live 3-role confirmation folded into the final hardening
pass, per precedent): Staff Supervisor gains System Parameters edit + Audit read-only,
keeps business set, no technical powers; Intake keeps operations + booking reads, loses
config screens.

### Follow-ups (out of this phase)
- Exit-impersonation control ("Back to my account") in the custom shell -- ABP's stock
  navbar item is not rendered, so IT Admin currently logs out to return to host.
- T7 `Books` sample-entity cleanup -- deferred (tangential cleanup; its own chore).
- Live confirm Supervisor (sysparams edit + audit read) and Intake (booking) as their
  own users in the final hardening pass.
- Supervisor cross-clinic switching (the deferred host/Option-A effort).
- Multi-tenant hardening backlog (blob isolation, branding, per-tenant DB/email, lookup
  isolation) -- separate plans.

## Tasks

- T1: Spike -- verify ABP cross-tenant permission resolution. DONE (see outcome above). Determine, against ABP
  Commercial v10 docs + the Volo Saas / Account source, whether tenant impersonation logs in
  as tenant-admin (full) and whether user impersonation or a host-role + tenant-selector can
  preserve a host Supervisor's business-only permission set. Pick the mechanism for IT Admin
  (full) and for Supervisor (business-only). Record the decision + rationale in this plan
  (update the Approach section) before T3/T4.
  - approach: code (research spike; no test)
  - files-touched: [docs/plans/2026-06-16-internal-roles-access-model.md]
  - acceptance: a single chosen mechanism per role is documented with source citations; T3
    and T4 designs are finalized from it.

- T2: Rewrite role grants in `InternalUserRoleDataSeedContributor` to the matrix above.
  Extract each role's grant set into a testable shape; IT Admin gains the framework grants;
  Supervisor gains SystemParameters.Edit + AuditLogging(read) + impersonation; Intake is
  trimmed. Keep idempotent. Framework permission strings are hardcoded literals per the
  existing file convention (Domain cannot reference the framework permission constants);
  cross-check each against its module.
  - approach: test-after (unit test asserting each role's expected granted-permission set)
  - files-touched: [src/...Domain/Identity/InternalUserRoleDataSeedContributor.cs, test/...Domain.Tests/Identity/*]
  - acceptance: after reseed, querying each role's grants returns exactly the matrix set; no
    grant of a `Tenant`-only permission to a host role (no ABP rejection on seed).

- T3: Move Staff Supervisor to host scope. Seed the Supervisor role at host (TenantId null)
  like IT Admin; design + run a data migration for any existing per-tenant Supervisor roles
  + user assignments (reassign to the host role, or recreate). Resolve the `Dashboard.Tenant`
  (Tenant-only) gap per the T1 mechanism (host dashboard vs in-clinic dashboard).
  - approach: test-after
  - files-touched: [src/...Domain/Identity/InternalUserRoleDataSeedContributor.cs, src/...Domain/Data/* (migration/backfill), src/...Domain.Shared/* if a const moves]
  - acceptance: a Supervisor user authenticates at the host (admin.localhost) and lands on a
    working host shell; no orphaned per-tenant Supervisor role/user rows.

- T4: Wire the real "switch into clinic" per the T1 mechanism. Replace the Tenants-hub
  subdomain-navigation with the chosen impersonation / tenant-selector flow; gate by
  `Saas.Tenants.Impersonation`; ensure IT Admin enters with full access and Supervisor with
  business-only. Regenerate proxies if a new endpoint is added.
  - approach: test-after
  - files-touched: [angular/src/app/users/internal-users-hub.component.ts (switchTenant), angular/src/app/users/users-section.gateway.ts, src/...HttpApi/Controllers/* or AuthServer wiring as needed, angular/src/app/proxy/*]
  - acceptance: as IT Admin, switch into Falkinstein -> full access; as Supervisor, switch in
    -> business surfaces only (no Roles/Files/Languages/tenant-provisioning); switching back
    to host works; the action is audited.

- T5: Align nav + route gating to the matrix. Supervisor host nav shows Audit (read-only)
  and the business admin items; IT Admin shows all; Intake nav trimmed. Admin-hub rail shows
  Audit to Supervisor read-only. Route `requiredPolicy` values match the matrix so
  visibility == access (no click-into-403).
  - approach: code
  - files-touched: [angular/src/app/shared/components/internal-shell/internal-nav.config.ts, angular/src/app/app.routes.ts, angular/src/app/admin/* gating]
  - acceptance: each of the 3 roles sees exactly its matrix surfaces; no visible 403s.

- T6: Intake tightening + booking-read safety. Remove Intake's config-management /
  notification-template / system-parameter / internal-user-create grants; confirm the
  booking flow still loads (appointment types, languages, locations, doc types, field
  config, system-parameter validation) -- either via retained `Default` reads or via the
  booking AppService's own (ungated/internal) reads. Add a regression check that an Intake
  user can complete a booking.
  - approach: test-after
  - files-touched: [src/...Domain/Identity/InternalUserRoleDataSeedContributor.cs (Intake grants), test/* booking-as-intake check]
  - acceptance: Intake sees no config screens but completes an end-to-end booking with no 403.

- T7: Remove the `Books` ABP sample entity (entity, permissions, seed references, proxy,
  any routes). Pure cleanup.
  - approach: code
  - files-touched: [src/... Books entity + repo + appservice + permissions refs, angular/src/app/proxy/books/*, app.routes if present]
  - acceptance: solution builds clean; no `Books` references remain; proxies regenerated.

## Out of scope -- phased multi-tenant hardening backlog (separate plans)

Recorded now so it is not lost; each becomes its own research/design/build cycle later
(Adrian: "roles + switch now, rest phased"):
1. Blob-storage isolation (SECURITY): per-tenant MinIO container/prefix + verify document
   queries filter by tenant (today all clinics share one bucket).
2. Lookup-master isolation: decide per-clinic vs shared for Locations + similar host-shared
   masters; add `IMultiTenant` + migration if per-clinic.
3. Per-tenant branding: logo, theme colors, favicon, AuthServer login-page branding
   (BrandingAppService + storage).
4. Per-tenant email: per-clinic SMTP host + from-address (today one global mailbox).
5. Per-tenant databases: generate tenant-side migrations + auto-assign/provision connection
   strings on tenant create (plumbing already wired; not activated).
6. Real domains: env-driven base domain + custom-domain support (today `localhost`
   hardcoded in `tenant-bootstrap.ts`).
7. Editions / features: define tiers + gate features (infrastructure present, unused).

## Risk / Rollback

- Blast radius: T2/T3 touch authentication-adjacent seeding + role scope for ALL internal
  users -- the highest-risk change. T4 touches the tenant-switch / impersonation path. T5/T6
  are gating only. T7 is isolated cleanup.
- The Supervisor host-scope move (T3) is the riskiest: existing Supervisor users must not be
  locked out. Mitigation: design the migration to be reversible; test on the seeded
  Falkinstein supervisor before broad rollout; keep the per-tenant role definition until the
  host role is verified.
- Rollback: revert the seed + migration commit and reseed; revert the switch-wiring commit
  to restore the (cosmetic) subdomain navigation. Each task is an atomic commit.

## Verification

After build, on the running stack (reseed via db-migrator), live-test all three roles:
- IT Admin (host, admin.localhost): reaches every surface incl. Roles, Audit (full), File
  Management, Languages, Tenant provisioning; switches into Falkinstein with full access.
- Staff Supervisor (host): reaches all business surfaces + System Parameters edit + Audit
  read-only; does NOT see Roles/Files/Languages/tenant-provisioning; switches into a clinic
  and is business-only there; cannot see the technical surfaces while switched in.
- Intake Staff (Falkinstein): operations only, no config screens, completes a full booking;
  no visible 403s.
Confirm: reseed is idempotent (run twice), no orphaned roles, ng build clean, karma green
for new utils/tests, .NET build + Domain tests green, no console errors.
