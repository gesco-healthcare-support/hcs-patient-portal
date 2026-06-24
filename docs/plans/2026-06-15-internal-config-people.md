---
status: in-progress
date: 2026-06-15
slug: internal-config-people
branch: feat/redesign-internal-config-people
parent-branch: feat/internal-user-pages
surface: internal Configuration hub (5 lookups + field config) + People hub (patients/attorneys/examiners) + a shared responsive content-container fix
prompt: Prompt 15 (design_handoff_appointment_portal/PROMPTS.md row 15)
backend: YES -- B1 field-config (item 9: Required + batch upsert), B2 usage counts + lock/delete guards (item 32, lookups + People), B3 people portal status + appts-per-person
decision: "Adrian 2026-06-15: D1=A build field-config editor now (enforcement deferred); D2=A add usage counts + lock/delete guards now; D4=A add appts-per-person + portal-status (incl. invited) endpoints now. Execution mode TBD when build starts."
---

# Plan: Internal Configuration + People redesign (Prompt 15)

Re-skin nine internal lookup/people pages into two unified hubs (each a left
sub-nav rail + shared table), using the Prompt 9-14 pattern (standalone + OnPush
+ signals + shared `ia-*`/`af-btn`/`ra-*`/`ad-*` SCSS). Reuse the existing ABP
engines/proxies. Fix the shared shell content container so wide screens stop
wasting side gutter (the hardcoded-padding issue). Leave clean seams for the
deferred date/time plan (DOB stays a bare date).

## Surfaces

### Configuration hub (rail: Types / Statuses / Document Types / Languages / States)
- Shared CRUD table per section (name + section-specific columns), `ra-modal` form.
- Per-row **usage count** ("128 appointments") + **lock chips** (System / Required)
  and **delete guards** (system rows + in-use rows cannot be deleted).
- **Appointment Types**: expandable per-row **Field Configuration** panel -- per
  booking-form field, three segmented toggles (Visible/Hidden, Editable/Read-only,
  Required/Optional) + a pre-fill default value, grouped by booking-form step
  (Schedule / Patient / Parties / Claim / Documents), saved as one batch per type.
- **Document Types**: extra "Required by default" switch + an Appointment-Type link.

### People hub (rail: Patients / Applicant Attorneys / Defense Attorneys / Claim Examiners)
- Patients: curated table + search + filters (gender, DOB range, city, state,
  language, portal) + **column chooser** (`rp-cols`) + a full **detail view**
  (header w/ avatar + portal chip + Invite-to-portal + Edit; cards Personal /
  Contact / Address / Preferences; Appointments table) + edit modal.
- Attorneys / Examiners: table + modal (first, last, firm, firm address, email,
  phone, fax).
- **Portal-account chip** (linked / invited / none) + **Invite to portal**
  (deep-link to the existing `/users/invite` InviteExternalUser flow).
- Delete guard: cannot delete a person linked to appointments.

## Permissions (verified against the backend seed -- matches the nav)

| Surface | Permission group | Supervisor | Intake | IT Admin (host) |
| --- | --- | --- | --- | --- |
| All 5 Configuration lookups | `CaseEvaluation.{AppointmentTypes,AppointmentStatuses,AppointmentDocumentTypes,AppointmentLanguages,States}` | Default/Create/Edit (DocumentTypes Delete = IT-Admin only) | none | full |
| Patients | `CaseEvaluation.Patients` (+ `RevealSsn`) | full | Default/Create/Edit (no Delete) | -- |
| Applicant/Defense Attorneys, Claim Examiners | `CaseEvaluation.{ApplicantAttorneys,DefenseAttorneys,ClaimExaminers}` | full | none | -- |

All routes already gate via `permissionGuard` + `requiredPolicy`; the redesigned
routes keep the same guards. No nav change needed.

## Research grounding (verified 2026-06-15)

- **Routes**: all 5 config routes are children (app.routes.ts ~108-112); all 4
  people routes are children (~149-152). NO explicit shadow routes (unlike the
  Scheduling generate/add case). Each route loads a legacy ABP CRUD component
  (datatable + abp-modal + abstract list/detail services + proxy).
- **Field config**: `AppointmentTypeFieldConfig` entity EXISTS (TenantId,
  AppointmentTypeId, FieldName, Hidden, ReadOnly, DefaultValue; composite-unique
  per type+field) + `IAppointmentTypeFieldConfigsAppService`
  (GetByAppointmentTypeIdAsync + single CRUD). MISSING: a `Required` column and a
  batch-upsert; no Angular proxy/UI today.
- **Usage/locks (item 32)**: only `AppointmentDocumentType` has IsSystem +
  `EnsureNotInUseAsync`. Others (AppointmentType/Status/Language/State) have no
  IsSystem, no usage count, no delete guard. No lookup DTO carries a usage count.
- **People**: Patient (full demographics; SSN masked via `SsnVisibility`, full
  value only via `GetFullSsnAsync` + `Patients.RevealSsn`); attorneys/examiners
  (firm/contact + nullable `IdentityUserId`). `GetListAsync` ->
  `PatientWithNavigationPropertiesDto`. Portal status, appts-per-person, and
  delete guards are all MISSING. `PatientProfileRedesignComponent` (external
  my-profile) is the card-layout pattern reference; `InviteExternalUserComponent`
  (`/users/invite`) is the Invite-to-portal target.
- **SCSS**: reuse `ad-card/ad-dl/ad-field/ad-table` (_ad-detail), `rp-cols/rp-colmenu`
  (_rp-report), `ia-search/ia-filters/ia-field/ia-scroll/ia-pt/ia-conf/ia-clickaway/ia-empty`
  (_in-appts), `ra-note/ra-switch/ra-modal` (_ra-wizard). NEW partials needed:
  `_in-config.scss` (cf/cf-rail/cf-fc/cf-lock/cf-usage/cf-req), `_in-people.scss`
  (pp-portal/pd-head/pd-grid/pd-back).

## Decisions (ADR-style)

### D1 -- Field Configuration scope (CHOSEN: A, Adrian 2026-06-15)
The entity exists but lacks the `Required` toggle + a batch save.
- **(A) Build the editor now (CHOSEN):** add `Required` (bool) to the entity
  + Create/Update/Dto + a migration; add a batch-upsert endpoint
  (`SaveForAppointmentTypeAsync(typeId, items[])`, replace-set semantics); add the
  Angular proxy; build the expandable Field Configuration panel fully wired.
  Stored config is ready for the booking form to honor later. **Booking-form
  ENFORCEMENT (the wizard reading hidden/readonly/required/default) is OUT OF
  SCOPE here** -- that is a separate change to the Prompt 12 wizard.
- **(B) Defer field config:** ship Configuration lookups only; render the panel
  hidden / "coming soon". No backend change.

### D2 -- Usage counts + lock/delete guards (item 32) (CHOSEN: A, Adrian 2026-06-15)

**B2 concrete shape (Adrian 2026-06-15, after the reference-map investigation):**
- **System lock = FULL FIDELITY.** Add an `IsSystem` bool to `AppointmentType`,
  `AppointmentStatus`, `AppointmentLanguage`, `State` (entity + DTO + EF + one
  migration); seed the canonical rows as system (the 5 statuses; the default
  language English; default state CA; the seeded appointment types per the
  data seeders). Block deleting system rows (mirror `AppointmentDocumentType`'s
  `EnsureNotSystem`). `AppointmentDocumentType` already has IsSystem -- reuse.
- **Usage counts (real FK only):** `AppointmentType` -> Appointment count by
  type; `AppointmentLanguage` -> Patient count by language; `State` -> sum across
  its referrers (Location/WcabOffice/Patient/ApplicantAttorney/DefenseAttorney/
  ClaimExaminer/AppointmentClaimExaminer/AppointmentEmployerDetail/
  AppointmentPrimaryInsurance); `AppointmentDocumentType` -> AppointmentDocument
  count. People: Patient -> Appointment.PatientId; ApplicantAttorney ->
  AppointmentApplicantAttorney; DefenseAttorney -> AppointmentDefenseAttorney.
- **Non-FK lookups (NOT tracked):** `AppointmentStatus` lookup entity is NOT
  FK-referenced (appointments use the `AppointmentStatusType` enum) and
  `ClaimExaminer` master is NOT FK-referenced (AppointmentClaimExaminer is
  free-text). So: no usage count (UI shows "--" / "not tracked"), and no in-use
  delete guard for those two (their delete is gated only by IsSystem for
  statuses; ClaimExaminer has neither -> freely deletable).
- Surface `usageCount` (int, nullable for not-tracked) + `isSystem` on the list DTOs.

- **(A) Add backend now (CHOSEN):** add a `usageCount` to each
  lookup's list DTO (count of referencing rows) + a delete guard that blocks
  system rows and in-use rows with a localized message (reuse the
  `AppointmentDocumentType` pattern; treat the seeded statuses as system). Add the
  same in-use delete guard to the four People services (block when linked to an
  appointment). Surface counts + lock chips in the UI.
- **(B) Defer:** no backend; hide usage counts; attempt delete and surface the
  raw backend/FK error as a toast. Simpler, but the design's counts + lock chips
  are unmet.

### D3 -- Hub structure (decided; latitude granted)
Keep the 9 routes + 9 nav items unchanged. Build TWO standalone hub components
(`InternalConfigurationComponent`, `InternalPeopleComponent`); each existing route
loads the matching hub scoped to its section via route `data` (e.g. `section:
'types'`). The rail items are `routerLink`s to the sibling routes, so the rail
mirrors the nav and deep-links keep working. Reuse the existing detail-form
services + proxies; render via the new ra-modal forms. Legacy components stay
(not deleted).

### D4 -- Patient detail data (CHOSEN: A, Adrian 2026-06-15)
The detail view needs an Appointments table + a portal-account chip.
- **(A) Add endpoints now (CHOSEN):** add a per-person appointments query
  (count + list, by `patientId`) + a server-derived `portalStatus`
  (linked / invited / none). **PRE-BUILD UNKNOWN:** the "invited" sub-state needs
  an invite-state source -- the first build step must locate where the
  `/api/app/external-users/invite` flow persists a pending invite (a pending
  IdentityUser, an unconfirmed-email flag, or an invite/token row). If no durable
  invite record exists, B3 also adds the minimal persistence to derive "invited";
  if that proves large, fall back to linked/none and flag invited deferred (note
  it in the build, do not silently drop scope). Attorney/examiner detail = modal
  only (no full page).
- (B) Reuse what exists (not chosen): appointments via the existing list filtered
  by `patientId`; portal chip = Linked (IdentityUserId set) / None; no "invited".

### D5 -- Responsive content container (decided; the padding/whitespace fix)
Replace the shared `.in-content` fixed gutters + low cap with fluid gutters and a
higher cap, so wide/ultrawide screens fill instead of leaving a dead band:
```
.in-content {
  flex: 1;
  padding: 24px clamp(16px, 3vw, 64px) 64px;   /* was 26px 28px 60px */
  width: 100%;
  max-width: 2240px;                            /* was 1760px */
  margin: 0 auto;
}
```
Principle (applies to every internal page): page gutters use fluid units
(`clamp`/`vw`), never a fixed px that strands whitespace as the viewport grows;
inner hub grids already use fluid `1fr` columns. Verify live at 1366 / 1920 /
2560 widths. This is a shared-shell change -> smoke-test the already-merged pages
(dashboard, appointments, scheduling) for regressions.

### D7 -- Permission-gated sidebar nav (Adrian 2026-06-15: hide what the user can't open)
Problem: the shell nav currently filters items only by the coarse `roleKey`
(`filterNavGroups` checks `item.roles.includes(roleKey)`), which is a
hand-maintained approximation of the real ABP permission grants. The route
guards (`permissionGuard` + `requiredPolicy`) + backend `[Authorize]` are the
true gate, so the nav can drift and show a link that 403s on click -- exactly the
bad UX Adrian flagged (e.g. a Supervisor who had `States` revoked, or any future
custom role).
Fix (single-sourced, defense-in-depth on top of the guards):
- Add an optional `requiredPolicy?: string` to `InternalNavItem`, set to the SAME
  ABP policy string the item's route guard uses (e.g.
  `CaseEvaluation.AppointmentTypes`, `CaseEvaluation.Patients`,
  `CaseEvaluation.ApplicantAttorneys`, ... -- from the verified permission table
  above + each existing route's `requiredPolicy`). Items with no ABP policy
  (e.g. Dashboard) leave it unset.
- In the shell's nav resolution, after the role filter, drop any item whose
  `requiredPolicy` is set AND not granted, using ABP's `PermissionService`
  (`getGrantedPolicy(policy)`; superuser/`admin` returns true for all). Keep the
  `roles` filter as a coarse first pass (cheap) but treat the permission check as
  authoritative for gated items. Result: nav item visible <=> guard allows ->
  never click into a 403.
- This is shell-wide (benefits every nav item, host + tenant), not just
  Config/People; it is the right place to land the fix while we are in the shell.

### D6 -- New SCSS partials
`_in-config.scss` (cf, cf-rail, cf-railitem, cf-fc + rows/segments, cf-lock,
cf-usage, cf-req) and `_in-people.scss` (pp-portal, pd-head, pd-grid, pd-back),
registered in styles.scss. The cf-rail is shared by both hubs (People reuses it).

## Build progress (2026-06-15) -- DONE vs TODO

DONE + committed + verified on `feat/redesign-internal-config-people` (off
`feat/internal-user-pages`):
- T0 responsive `.in-content` (fluid clamp gutters + 2240 cap) -- `bcb4fe3`
- TN permission-gated nav (requiredPolicy + PermissionService) -- `bd32d81`, nav spec 10/10
- T1 SCSS partials `_in-config.scss` + `_in-people.scss` -- `48d9370`
- B1 field config: Required + batch SaveForAppointmentTypeAsync + FieldConfigReconciler
  (7 tests) + migration + proxy -- `6075eeb`
- B2 system locks + usage counts + delete guards (IsSystem on 4 lookups + migration
  `Add_Lookup_IsSystem` + seed + usage counts + people in-use guards + 400/409
  status map + proxy DTOs) -- `1f782b1`. Backend `dotnet build` clean.
- B3 people portal-status: invite-state source CONFIRMED -- `Invitation` aggregate is
  persisted (`InviteExternalUserAsync` -> `InvitationManager.IssueAsync`; accepted on
  register). So all 3 chips are derivable client-side: Linked = `identityUserId` set
  (already on the People DTOs/proxies, auto-mapped, no Mapperly change); Invited =
  email in the active-invitation set; None = neither. Added one lean endpoint
  `GetActiveInvitedEmailsAsync(emails)` on `IExternalSignupAppService` (GET,
  permission-gated `UserManagement.InviteExternalUser` -- same trust boundary as the
  invite action) returning the subset with an active (not accepted/expired/deleted),
  tenant-scoped invitation; injected `IInvitationRepository`; hand-added the Angular
  proxy `getActiveInvitedEmails`. Appointments-per-person is FREE (appointments proxy
  already filters by `patientId`). Backend `dotnet build` clean. NOTE: no dedicated
  unit test -- declarative ABP `[Authorize]` gate (same pattern as the existing invite
  method) + a tenant-scoped EF query carry no custom security logic; behavioral
  coverage is the T5 live "Invited" chip check (the real surface). Commit: (pending).

TODO (next session):
- T2 Configuration hub component (rail routerLinks + shared section table w/ usage +
  System/Required lock chips + guarded delete + ra-modal CRUD reusing the 5 detail
  services; Appointment Types Field Configuration panel using the field-config proxy
  `saveForAppointmentType`). FIELD CATALOG: read
  `angular/src/app/appointments/appointment/send-back-fields.ts` -- the field-config
  FieldName keys MUST match its FlaggableField.key vocabulary (the design's
  CF_FIELDS0 labels are display only). `cf-config.util.ts` (+spec) for catalog
  grouping + form<->batch-DTO mapping. Statuses usage shows "--" (not tracked).
- T3 People hub (rail + table + search/filters + column chooser + Patients detail
  view w/ portal chip + Invite-to-portal deep-link + appointments table + edit modal;
  attorney/examiner table+modal). `people.util.ts` (+spec).
- T4 repoint the 9 routes (5 config + 4 people, all children-based, NO shadow) to the
  2 hubs with `data.section`; keep guards.
- T5 verify live (stafsuper1 + clistaff1-as-Intake: no Config/attorney nav, nothing
  403s) + karma; responsive at 1366/1920/2560; then squash-merge to
  feat/internal-user-pages + push + restart angular.

## Tasks (ordered; approach flag per ~/.claude/rules/rpe-workflow.md)

- **T0 [code]** Responsive container fix (D5) in `_in-shell.scss`; smoke-test merged pages.
- **TN [test-after]** Permission-gated nav (D7): add `requiredPolicy?` to
  `InternalNavItem` + populate it for every gated item from the route guards;
  gate the shell nav by ABP `PermissionService.getGrantedPolicy` so items the
  user lacks are hidden. Unit-test the filter (granted -> shown, not-granted ->
  hidden, no-policy -> role-only, admin -> all). Independent of the hubs; do early.
- **T1 [code]** `_in-config.scss` + `_in-people.scss`; register in styles.scss.
- **B1 [tdd]** Backend field config: add `Required` to entity + DTOs +
  migration; batch-upsert `SaveForAppointmentTypeAsync` (replace-set, validates
  field names against the catalog); unit-test the batch + Required; regenerate proxy.
- **B2 [tdd]** Backend usage counts + guards: usageCount on lookup list
  DTOs; delete guards (system + in-use) on the lookups lacking them and on the 4
  People services; localized messages; unit-test the guards. Regenerate proxies.
- **B3 [code]** People portal status + appts-per-person endpoints. First resolve
  the invite-state source (D4 pre-build unknown); add minimal invite persistence
  only if needed. Regenerate proxies.
- **T2 [test-after]** `InternalConfigurationComponent` (standalone, OnPush): rail
  (routerLinks) + shared section table (usage + lock chips + guarded delete) +
  ra-modal CRUD reusing the 5 detail-form services; the Types Field Configuration
  panel (toggles + default + batch save) when D1=A. `cf-config.util.ts` (+spec)
  [tdd] for the field-catalog grouping + form-state <-> DTO mapping.
- **T3 [test-after]** `InternalPeopleComponent` (standalone, OnPush): rail + table
  + search/filters + column chooser + ra-modal CRUD; Patients detail view
  (cards + appointments table + portal chip + Invite-to-portal deep-link).
  `people.util.ts` (+spec) [tdd] for portal-status mapping + client search/filter.
- **T4 [code]** Point the 9 routes (5 config + 4 people route arrays) to the two
  hubs with `data.section`; keep guards. Verify no shadow entries in app.routes.ts.
- **T5 [verify]** Live (Playwright, stafsuper1; also clistaff1 for Patients-as-Intake
  to confirm no-delete + no attorney/config access) + karma for the new utils.

## Risks / gotchas
- Field-config has NO booking-form enforcement yet (D1 stores config only); say so in the UI copy.
- People delete without a guard (D2=B) will surface raw FK errors -- prefer D2=A guards.
- SSN: never render the full value; the detail/edit use the masked value; full reveal stays behind `GetFullSsnAsync` + `RevealSsn` (do not wire reveal into this page unless asked). PHI: all demo data synthetic.
- `.in-content` change is global -> regression-check merged pages at multiple widths (D5).
- Patient list proxy may carry stale singular nav-prop fields (as Locations did) -- check before binding; localize any cast.
- DOB is a bare date -> no timezone conversion (deferred date/time plan).

## Out of scope
- Booking-form enforcement of field config (separate wizard change).
- The "Invited" portal sub-state if invite tracking is absent (D4=B).
- Date/time UTC retrofit (only keep DOB naive).
- Users & Access / Admin pages (Prompt 16).

## Verification
- Build: `ng build` clean; `dotnet build` + targeted `dotnet test` (if B1/B2).
- Karma: cf-config.util + people.util specs.
- Live (falkinstein:4250): each Config section CRUD + lock/usage + (Types) field
  config save; People rail + Patients table/filters/columns/detail/edit + Invite
  deep-link + attorney/examiner CRUD; responsive check at 1366/1920/2560.
- Permission-gated nav (D7): log in as Intake (clistaff1) -> the sidebar must NOT
  render Configuration, Change Requests/Logs/Reports, Scheduling, or
  attorneys/examiners; Patients IS shown (no delete button). Confirm there is no
  sidebar link that leads to a 403 for the logged-in user (every visible item
  resolves). Spot-check Supervisor (stafsuper1) sees the full tenant nav. If
  feasible, revoke one permission via the admin UI and confirm its nav item
  disappears (proves permission-driven, not role-driven).

## Post-verification follow-ups (2026-06-16)

Live verification (stafsuper1) surfaced gaps. Adrian approved these fixes. On
sub-branch `fix/config-people-followups` off `feat/internal-user-pages`; atomic
commit per item; batch live-verify + squash-merge at the end.

- **F5a [code]** Expose attorney Email end-to-end. The ApplicantAttorney /
  DefenseAttorney ENTITIES already store `Email`, but their Create/Update/read
  DTOs omit it, so the People hub cannot edit it. Add `Email` to the 3 DTOs each
  + map it (Mapperly auto-maps; entity has the field) + regen the proxies + add
  an Email input to the attorney branch of the people edit modal. ClaimExaminer
  already has email end-to-end (no change).
- **F2 [code]** Backfill `IsSystem` on canonical lookup rows. Seeders set
  IsSystem only on insert, so pre-existing English/California/AME (and any system
  doc-type) stay false and show no System chip. Add a host-context EF migration
  whose Up runs idempotent `UPDATE ... SET IsSystem=1 WHERE Id IN (<seed guids>)`
  keyed by CaseEvaluationSeedIds. Production-safe + idempotent (no wipe).
- **F3 [code]** Grant Staff Supervisor `AppointmentStatuses.Default` (view only)
  in the role seeder. The status lookup is enum-driven/non-functional, so with
  per-action gating the supervisor sees the 6 seeded statuses as read-only
  reference (no Create/Edit/Delete buttons). Keeps the design's section honest.
- **F1 [verify]** Drive clistaff1 (Intake) live: no Config/attorney/examiner rail,
  Patients shown without Delete, field-config hidden, nothing 403s.
- **F4 [verify]** Clean the stray PQME field-config (SSN hidden) test row created
  during verification: reopen panel -> SSN Visible -> Save.
- **F6 [docs]** Document that AppointmentDocumentType has no per-row required
  flag (required-docs is the RequiredDocumentEvaluator/package mechanism); the
  modal's isActive/"Inactive" mapping stays. No code change.

Deferred (noted, not in scope): gated SSN reveal for Patients.RevealSsn holders
(Intake); attorney appointments-count column.
