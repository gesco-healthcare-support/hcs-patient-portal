---
title: QA / UX triage - host + tenant multi-office findings
date: 2026-06-29
status: in-progress
branch: fix/host-portal-ux-and-branding
---

# QA / UX Triage (2026-06-29)

Source: owner testing pass across IT Admin, Staff Supervisor, Intake Staff, and
general flows. Role-grouped findings were consolidated into 12 distinct items
(A-M; K already fixed this session). Each was diagnosed read-only by a dedicated
investigator (workflow `qa-triage-patient-portal`, run wf_fc86811e-23b).

Process: go item by item -> plan -> fix -> verify -> commit -> next.

## Recommended order

| # | ID | Item | Effort | Risk | Decision needed | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | K + email | Close out in-flight: tenant login title+logo, email-tenant resolver | - | low | no | DONE 038efe94 (email) + 75af63e5 (login); live logo re-render deferred |
| 2 | J | Panel strike list double badge | XS | low | cosmetic only | DONE fdf872f8 (+ karma unblock be9a2823) |
| 3 | D | "Create Internal User" tenant field | XS | low | confirm scope | DONE 45025547 (full removal) + proxy regen 7afd582c |
| 4 | A | Show office name as "Dr. {name}" | S | low | scope of "everywhere" | DONE a3164d9b (5 pages + switcher; banner -> F) |
| 5 | G | Dashboard time filter must drive all sections | M | low | which sections + host/intake | DONE 2a7d9168 (tenant: donut/trend/activity; host/intake deferred) |
| 6 | M | Authorized-user emails (2 wired stubs) | M | low | wording sign-off | DONE ee6199e6 (reseed-verified; accessor ##URL## restored) |
| 7 | F | "Acting as" banner + Evaluators relabel + direct switch | M-L | med-HIGH | DECIDED 2026-06-29 | next: remove banner + switcher-inside-office exit; host label "Management"; office->office via single-click CUSTOM AuthServer grant (needs isolation/HIPAA gate re-run) |
| 8 | C | Host-scope invite needs a tenant chooser | M | med | picker scope | todo |
| 9 | B | Reusable table: search / filter / sort | L | med | build-vs-buy, server-side | todo (Claude Design) |
| 10 | H | Intake-assignments UX at scale (depends on B) | M | low | layout choice | todo (Claude Design) |
| 11 | E | Role/permission matrix UX for non-technical staff | L | med | grouping taxonomy | todo (Claude Design) |
| 12 | I | Intake-staff host analytics for assigned offices | M | med | metric set | todo (needs product decision) |
| 13 | L | "Request more info" must cover every wizard field | XL | high | scope of "every field" | todo (largest) |

Ordering logic: lock in already-done work, then XS/S high-confidence fixes, then
low-risk backend correctness, then medium frontend, then the design-research
cluster (B -> H, E, I), then the single XL/high-risk item (L) last.

Couplings: A and F both edit `internal-shell-layout`; do them adjacently. H
depends on B's reusable table. B/E/H all want a Claude Design pass.

## Live verification (2026-06-30)

Stack rebuilt (api+authserver+angular), all healthy. Verified live:
- K - curl tenant login: title "Appointment Portal", logo data URI clean (no entity corruption).
- A - browser as IT Admin: "Dr. {office}" on dashboard tenants table, /users/tenants,
  /host/branding (Office col only - Display name left as stored), /host/intake-assignments
  (dropdown + table), and the navbar switcher dropdown. Subdomain + Display name not prefixed.
- D - browser: Create Internal User form has only Role/First/Last/Email/Phone (no Tenant);
  internal-users list has no Tenant column.
- G - API (tenant scope, impersonated Falkinstein): trend buckets 1/5/13 and donut total
  4/6/6 and activity 5/6/6 all shift Week/Month/Quarter; tiles already worked.
- J - 4 passing component specs (live badge needs a strike-list document).

---

## Item details

### K (+ email) - in-flight closeout [applied, needs verify+commit]
- **Login title/logo (K):** 19 non-en culture files had `"AppName":"CaseEvaluation"`;
  set all to "Appointment Portal" (en.json already correct). `BrandingHead/Default.cshtml`
  now uses `@Html.Raw(Model.LogoCss)` so the `+` in base64 data URIs is not
  HTML-encoded inside the `<style>` raw-text element (was corrupting the logo).
- **Email-tenant resolver:** `AppointmentRecipientResolver` now stamps
  `TenantId = _currentTenant.Id` on enqueued `SendAppointmentEmailArgs` so the
  recurring reminder jobs no longer enqueue `TenantId=null` (host scope).
- **Next:** rebuild api+authserver, verify tenant login (title + logo) + en.json
  parity, decide on the resolver regression test, commit atomically.

### A - Office name as "Dr. {name}" [S / low / frontend]
- **Root cause:** no single-source formatter; raw `Tenant.Name` (lowercased
  subdomain slug) is bound inline in each template. Hazard: `/host/branding` shows
  raw name beside the staff-editable `OfficeBranding.DisplayName`, and the
  intake-assignments dropdown stores the raw name in a `LookupDto.DisplayName`
  field (misleading name) - a blanket replace would mis-prefix.
- **Fix:** new pure standalone `OfficeNamePipe` (mirror `ssn-mask.pipe.ts`),
  idempotent ("dr." prefix-safe), applied ONLY to raw-tenant-name bindings, never
  to `OfficeBranding.DisplayName`. Sites: internal-dashboard.html L96-97,
  internal-users-hub.html L405-406, intake-assignments.ts L45/L77,
  host-branding.ts L45 (NOT L49), optionally internal-shell L90/L103/L159.
- **Decisions:** scope of "everywhere" (shell pill/switcher/banner too?);
  title-case the leading letter (`Dr. Falkinstein` vs `Dr. falkinstein`)?

### B - Reusable table search/filter/sort [L / med / fullstack]
- **Root cause:** all 5 host tables are bespoke hand-rolled `<table>`s; no shared
  grid/managed-table component exists. Only the "Pending Invites" section has a
  (server-side) search. Stock ABP endpoints (`ExtensibleUserService.getList`, Saas
  `TenantService.getList`) already accept filter+sorting+paging but the client
  fetches `maxResultCount:500` and never sends them.
- **Fix:** one standalone `app-managed-table` (column defs, search box, header
  asc/desc arrows + optional "sort by", client|server mode). Client-side for
  intake-assignments/branding/dashboard-activity; server-side for tenants +
  internal-users (wire stock endpoints, real paging).
- **Decisions:** build bespoke vs ABP extensible-table vs Material; which tables
  need server-side paging; per-table search columns + discrete filters; header
  arrows AND a sort-by dropdown (owner asked both); default sorts; paging UX.

### C - Host-scope invite tenant chooser [M / med / fullstack]
- **Root cause:** `InviteExternalUserDto` has no `TenantId`;
  `InviteExternalUserAsync` reads ambient `CurrentTenant.Id` and throws
  "Tenant context required for invite." at host scope (clean error, not a 500 or
  mis-scope). `InvitationManager.IssueAsync` inserts under ambient scope, so under
  db-per-office the row would need `CurrentTenant.Change(target)` to land in the
  office DB. A built-but-unused `GetTenantOptionsAsync` (host-only) exists.
- **Fix:** add optional `Guid? TenantId` to the DTO; backend requires+validates it
  at host scope and wraps issue+url+dispatch in `CurrentTenant.Change`; frontend
  shows a required Office picker (from `getTenantOptions`) only in host scope.
  Regen proxy. Check `ResendInviteAsync` for the same Change-scope need.
- **Decisions:** any office vs operator-scoped offices; branded/subdomained invite
  link (yes); is the legacy standalone invite component still routed?

### D - Create Internal User tenant field [XS / low / frontend, mostly done]
- **Finding:** the Tenant field, list column, create-result row, signal, getter,
  guard, and `LookupDto` import are ALREADY gone (prior pass). Only residual: the
  create call still sends `tenantId: EMPTY_GUID` because the generated proxy DTO
  types `tenantId` required. Backend ignores it (`CurrentTenant.Change(null)`).
- **Fix:** make proxy DTO `tenantId?` (ideally via `abp generate-proxy`), drop the
  `EMPTY_GUID` placeholder + const. Two files, no backend change.
- **Decisions:** confirm narrowed scope (residual cleanup) or close as resolved.

### E - Role/permission matrix UX [L / med / fullstack]
- **Root cause:** custom matrix (not stock @volo modal) renders a flat checkbox
  list. Definitions put all ~45 families in ONE group still labelled "Book Store";
  ~40 CRUD children reuse generic `Permission:Create/Edit/Delete` keys -> bare
  "Create"/"Edit"/"Delete" labels; no descriptions (ABP has no description field);
  no nesting/search.
- **Fix:** (a) split `CaseEvaluationPermissionDefinitionProvider` into domain
  groups + entity-specific labels + fix the "Book Store" label (additive keys,
  NEVER change permission name constants - they're persisted/seeded); (b) enhance
  the Angular matrix: nest by `parentName`, plain-language descriptions from a
  curated map, search/filter, collapsible groups, "Advanced" for stock ABP groups.
- **Decisions:** grouping taxonomy; hide/move stock ABP groups; tooltip vs inline
  help; English-only v1; land on epic vs main.

### F - "Acting as" banner + switching + Evaluators relabel [M / med / fullstack]
- **Root cause:** custom shell replaced ABP's "back to impersonator" menu item with
  an amber banner "Acting as {branded office}" + "Back to Evaluators" (the only
  exit). Switcher hidden inside an office (`canSwitch = !intake && hostScope`).
  Direct office->office is forbidden server-side: `HostIntakeImpersonationExtensionGrant`
  gates on the CURRENT principal, which inside office A is the office admin who
  lacks `Saas.Tenants.Impersonation`.
- **Fix:** (1) reword banner ("Viewing {office}" / "In office: {office}"); (2) or
  remove banner + move exit into the account menu; (4) replace "Evaluators" strings
  (html L5 alt, L168 button, ts L379 toaster, optionally L90 chip/subtitle) with
  "Administration"/"Management". (3) direct switch needs either a fragile UI
  two-hop (de-impersonate + re-impersonate, fights ABP's per-grant reload) or a
  custom AuthServer grant using the `ImpersonatorUserId` claim (security review).
- **Decisions:** reword vs remove banner; exact host label; is single-click
  office->office a hard requirement (server change) or is a two-hop acceptable?

### G - Dashboard time filter scope [M / low / backend]
- **Root cause:** frontend is correct (range signal refetches the whole DTO; every
  section binds `data()`). Backend `BuildTenantDashboardAsync(range)` feeds the
  window only to Approved/Rejected KPIs; donut/trend/recent-activity use fixed
  windows by design (`DashboardRange.cs` documents it). Filter is also hidden for
  host + intake.
- **Fix:** backend-only for tenant scope - thread `currentStart` into
  StatusBreakdown (date filter), Trend (range-derived buckets), RecentActivity
  (lower bound). Leave live Pending tiles, SLA deadline, Today's schedule
  point-in-time. Host/intake range support is net-new (decision).
- **Decisions:** which sections follow the range; donut by creation vs status-change
  date; give host/intake the filter; weekly vs monthly trend buckets for quarter.

### H - Intake-assignments at scale [M / low / fullstack, depends on B]
- **Root cause:** flat one-row-per-(operator,office) table -> duplicate operator
  rows; single-pair assign form; no search/filter/group/paging/bulk; backend
  `GetListAsync` is unbounded with an N+1 per row.
- **Fix:** group-by-staff expandable cards (count badge, office chips w/ inline
  unassign), multi-select assign, search box. Client-side grouping needs no
  backend change (S); bulk endpoints + N+1 fix are additive (M).
- **Decisions:** layout (group-by-staff [rec] vs group-by-office vs matrix); bulk
  now or later; search by operator only or also office.

### I - Intake-staff host analytics [M / med / fullstack]
- **Root cause:** intake lands on `/host/my-offices` (a bare switcher) because they
  lack `Dashboard.Host` (by design). No endpoint returns per-office metrics
  filtered to a single operator's assignments; data is either all-office
  (Dashboard.Host) or single-office post-impersonation (Dashboard.Tenant).
- **Fix:** new read-only endpoint gated by `IntakeImpersonation` that loops the
  operator's assigned office ids (reuse `GetMyOfficesAsync` + `CurrentTenant.Change`)
  computing per-office counters; render tiles on the landing. MUST iterate only
  assigned offices (isolation invariant - test with a canary unassigned office).
- **Decisions:** which metrics (pending / change-requests / today / overdue);
  cross-office worklist (PHI at host scope) vs counts only; deep-link tiles;
  confirm gate stays IntakeImpersonation.

### J - Panel strike list double badge [XS / low / frontend]
- **Root cause:** one document, two distinct fields - the category
  (`AppointmentDocumentTypeId` -> "Panel Strike List") AND the `IsPanelStrikeList`
  flag (server sets it true when the category name matches). The staff list renders
  BOTH as badges -> two identical "Panel Strike List" badges. Not a duplicate row.
- **Fix:** UI-only de-dup in `appointment-documents.component.html` L140-145 - show
  the flag badge only when the category label isn't already the strike-list name.
  Keep the flag (PQME approval gate + submit gate depend on it). The wizard staged
  list already shows one badge.
- **Decisions:** which single badge text wins (category label [rec] vs fixed flag) -
  cosmetic.

### L - Request-more-info every field [XL / high / fullstack]
- **Root cause:** the flaggable set (13 of ~55 wizard fields) is hardcoded in FIVE
  coupled layers (registry `send-back-fields.ts`, `external-fix-it.util.ts`
  source/payload maps, `SaveInfoRequestCorrectionsInput`, `InfoRequestCorrectionLock`,
  and `AppointmentInfoRequestsAppService` snapshot+resubmit-gate+writers). Most
  "missing" fields (employer, full attorney/CE/insurance detail, custom fields)
  have NO correction write-path and live on entities the fix-it page doesn't load.
  The F-018 resubmit gate is fail-OPEN for unknown keys (security-relevant).
- **Fix:** NOT "extend an enum." Recommend a metadata-driven refactor: one
  field-descriptor table (key -> group/label, owning entity, getter, setter,
  resolved-predicate) consumed by registry/lock/snapshot/gate/generic writer;
  fix-it renders editors from descriptors. Custom fields need a key convention
  (`customField:{id}`) + generic path.
- **Decisions:** does "every field" mean all ~55 incl. custom fields and scheduling
  fields; build edit paths for fields that lack them vs flag-only; enforce a
  per-field "was actually fixed" predicate (recommended).

### M - Authorized-user email stubs [M / low / backend]
- **Root cause:** 20 of 63 seeded notification codes render stub bodies/subjects
  (`<p>Stub body...` / `[code] -- TODO`). Of those, only 2 are WIRED to live
  triggers but were never given bodies: `AccessorAppointmentBooked` (new authorized
  party invite - and its stub drops the `##URL##` password-setup link, so the
  invitee cannot set a password) and `AppointmentCancelledDueDate` (JDF auto-cancel
  to booker). The other 18 are dormant (no dispatch site).
- **Fix:** port the 2 OLD bodies into `EmailBodies/AccessorAppointmentBooked.html`
  + `AppointmentCancelledDueDate.html` (accessor body MUST include `##URL##` +
  `##Email##`), add their `EmailSubjects` entries; reseed (DbMigrator) to replace
  seeded stub rows; add a regression test that no dispatched code renders a stub.
  Optional: dispatcher stub-guard (Tier 2). Defer the 18 dormant codes (YAGNI).
- **Decisions:** approve OLD copy as-is or supply revised; subject prefix wording;
  confirm deferring the 18; is the stub-guard in scope.
