---
status: draft
title: CLAUDE.md refresh + documentation IA overhaul
date: 2026-05-31
branch: docs/claude-md-and-docs-overhaul
author: Adrian (with Claude)
---

# CLAUDE.md refresh + documentation IA overhaul

<!-- This plan is the contract. It was produced from a read-only Phase 0 inventory +
     Phase 1 per-layer code comprehension (21 agents) and a web-research pass on
     CLAUDE.md best practices (5 agents, official + community + power-user, cross-checked
     against the official memory doc). No CLAUDE.md or doc has been edited yet. Execution
     starts only after Adrian approves this plan. -->

## Goal

Make this repo self-explaining on the modern stack as it actually is on `main` today
(PRs #267-#273 included): a correct, consistent, loading-aware set of `CLAUDE.md` files at
every layer and non-trivial feature, plus a streamlined `docs/` tree whose claims match the
code. Two deliverables, in order: (1) this plan, which we stop on; (2) the executed change
set after approval.

Scope owned: project `CLAUDE.md` files, `.claude/rules/*`, everything under `docs/`.
Out of scope: `~/.claude/` (Adrian's personal config), and all application source/test/build
config -- except a wrong doc-reference embedded in code, which we FLAG, never fix silently.

---

## 1. The CLAUDE.md standard we will apply

Verified against the official memory doc (code.claude.com/docs/en/memory) and the best-
practices doc, then cross-checked against ~40 community / power-user sources (see Sources).
The prompt's standard held on every point; research added the refinements marked [R].

**Loading mechanics (why the structure below is shaped this way):**
- Ancestor `CLAUDE.md` (root down to cwd) load in full at launch; subdirectory files load
  lazily only when Claude reads a file in that directory; sibling directories never load.
- `@import` expands into context AT LAUNCH -- it aids authoring but saves zero tokens. So
  docs are referenced as plain-text paths, never `@import`. [R: this is the single most
  common myth in community guides; debunked by official docs + every power-user source.]
- Only the project-ROOT `CLAUDE.md` is re-injected after `/compact`; nested files reload
  lazily and not until a matching file is read again. So the root must be self-sufficient
  for anything global. [R]
- Block-level HTML comments are stripped before context -- free maintainer notes.

**Size + budget:**
- Hard cap: under 200 lines per file (official; adherence degrades past it).
- Target leaner [R]: root hub ~80-120 lines; layer files ~50-110; feature files ~30-110.
  Community consensus is that 60-150 is the high-signal band and lower is better.
- We will report the combined line/token footprint of all `CLAUDE.md` after execution.

**Curation -- the deletion test [R]:** every line must answer "would removing this cause
Claude to make a concrete mistake?" Cut anything Claude infers by reading code, anything a
linter/CI already enforces, generic programming advice, personality/aspirational prose, and
file-by-file codebase tours.

**Shape (WHY / WHAT / HOW):** one-line purpose (WHY it exists), what lives here + key files
(WHAT), and the non-obvious conventions/gotchas a senior engineer would need (HOW). Scannable
headers + bullets, not prose.

**Writing rules:**
- Concrete + verifiable, never vague ("AppServices carry `[RemoteService(IsEnabled=false)]`",
  not "follow ABP conventions").
- Every non-obvious rule carries its WHY (co-locate the failure mode it prevents).
- Pair prohibitions with a positive alternative ("use `npx ng build` then `npx serve`", not
  just "never `ng serve`"). [R]
- Use `IMPORTANT` / `YOU MUST` sparingly -- reserve for the 1-3 hardest constraints per file;
  over-emphasis collapses the signal. [R]
- Front-load the highest-priority rules. [R]

**Structure -- hub-and-spoke + additive nesting:**
- Root = lean hub: global/cross-cutting conventions + a cross-reference MAP (plain-text path
  table) to every layer/feature `CLAUDE.md` and the key docs. [R: the map makes nested files
  discoverable even when Claude is not in that directory.]
- Nested files are ADDITIVE ONLY -- they never repeat root/parent content. [R: duplication
  wastes tokens and drifts when one copy changes. This is stricter than "no contradictions".]

**What does NOT belong in CLAUDE.md:** multi-step procedures -> a skill (`.claude/skills/`);
file-glob-specific rules -> `.claude/rules/*.md` with `paths:` frontmatter (the real token
saver; files without `paths:` load every session); long reference -> a `docs/` page by
plain path; zero-exception enforcement -> a hook/CI (CLAUDE.md is advisory). [R]

---

## 2. Per-feature / per-layer CLAUDE.md skeleton (reused for every create)

```markdown
# <Layer/Feature> -- <one-line purpose>

## What lives here
- <key file / entity / component> -- <one-line role>

## Conventions (non-obvious only)
- <concrete rule> -- <why it matters / what it prevents>

## Gotchas
- <trap a new engineer would hit>

## Related
- <plain-text doc path>   <!-- plain path, never @import -->
```

Root hub uses a flatter shape: Stack one-liner; Build/run + service order; the 6-8 binding
ABP/Angular conventions; a "Map" table (layer/feature CLAUDE.md + key docs by path); a short
"What never to do" list. Target ~80-120 lines.

---

## 3. CLAUDE.md target list

Legend: KEEP (accurate, lean) | UPDATE (additions, stays <200) | TRIM (over/near 200 or
verbose, cut to lean + fix drift) | CREATE (missing) | FOLD (delete file; move 1-line gotcha
into the layer file) | REWRITE.

### 3a. Layer + root files

| Path | Action | Note (current -> target lines) |
| --- | --- | --- |
| `CLAUDE.md` (root) | REWRITE -> hub | 254 -> ~110. Drop branch-scoped replicate mission detail (see Decision 4); add Map table; fix dead ref `docs/parity/it-admin-notification-templates.md` |
| `src/...Domain.Shared/CLAUDE.md` | UPDATE | 38 -> ~75. Add enum-placement deviation, `Extensions/ExtraPropertyConverters` (ABP bool bug), `Notifications/Events` namespace, AbpUiOverride/AccountOverride, en.json duplicate-key bug |
| `src/...Domain/CLAUDE.md` | CREATE | ~120. Dual-context rule; Stateless machine (only `AppointmentManager` fires transitions); capacity model; Patient SSN plaintext + never-clear; dual-ctor managers; Mapperly-only; 7 blob containers; framework-subfolder map; "Thin host-scoped lookups" subsection (absorbs the 5 folded files) |
| `src/...Application.Contracts/CLAUDE.md` | UPDATE | 50 -> ~78. Add `IUserSignatureAppService` not `IApplicationService`; Dashboard permission atypical (Host/Tenant, no Default); `INotificationDispatcher` in-process; `SsnRevealDto` masked design; Books `CreateUpdateBookDto` violation |
| `src/...Application/CLAUDE.md` | UPDATE | 37 -> ~78. Fix "17 features" -> 39+; mapper split (6 partial files); SSN masking mandatory on all patient exits; `ExternalSignupAppService` missing `[RemoteService]`; `SystemParameters`/`NotificationTemplates` extend `ApplicationService` |
| `src/...EntityFrameworkCore/CLAUDE.md` | UPDATE | 41 -> ~72. Fix dead key-file ref (`...DbContextModelCreatingExtensions.cs` does not exist); elevate Patient non-IMultiTenant PHI guard; two-context duplication rule; `AddRepository()` registration |
| `src/...HttpApi/CLAUDE.md` | UPDATE | 34 -> ~62. Add `api/public/*` namespace + `[IgnoreAntiforgeryToken]`; per-action auth exceptions (rule 4 is violated today); split-controller pattern; note `CaseEvaluationController` base is unused |
| `src/...HttpApi.Host/CLAUDE.md` | CREATE | ~95. 9 Hangfire daily jobs (PT cron chain 06:00-09:15); chained rate limiter; JWT subdomain issuer; exception->status mapping; 12/10 MB upload caps; `appsettings.secrets.json` sensitive; Docker-only run; SMTP silent-fail gotcha |
| `src/...AuthServer/CLAUDE.md` | CREATE | ~95. OpenIddict server (44368); Razor Account page overrides; account email via direct DI (not HTTP); Redis DataProtection key sharing; subdomain-only tenant; jobs enqueue-only; SQL->AuthServer->API->Angular order |
| `src/...DbMigrator/CLAUDE.md` | CREATE | ~40. One-shot console; run via docker-compose; `--disable-redis`; seed contributors live in Domain, not here |
| `src/...HttpApi.Client/CLAUDE.md` | CREATE (tiny) | ~28. Single module; `AddHttpClientProxies`; no hand-written proxies; consumer sets `RemoteServices:Default`. (See Decision 5.) |
| `test/CLAUDE.md` | KEEP | 51, accurate |
| `angular/src/app/CLAUDE.md` | UPDATE | 43 -> ~110. Fix feature count 18 -> 22; expand `shared/` (address/auth/ssn/lookup are the most non-trivial code); blob-download-via-HttpClient rule; address-provider DI; booking-form section-ownership; packet 5s polling; `performFullLogout`; `postLoginRedirectGuard` is `canMatch`; `AppLookupSelect` OnPush fix |

### 3b. Domain feature files (existing)

| Path | Action | Note |
| --- | --- | --- |
| `Domain/Appointments/CLAUDE.md` | TRIM | 271 -> ~120. Remove stale "no state-machine guard" claim (machine IS enforced); condense Angular UI tables |
| `Domain/DoctorAvailabilities/CLAUDE.md` | TRIM | 318 -> ~120. Cut verbose test prose; add that `SubmitRescheduleAsync` sets slot `Reserved` |
| `Domain/Patients/CLAUDE.md` | TRIM | 219 -> ~110. Fix Entity-Shape "no IMultiTenant" (closed FEAT-09 2026-05-05); keep SSN never-clear + fuzzy match |
| `Domain/Locations/CLAUDE.md` | TRIM | 190 -> ~85 |
| `Domain/Doctors/CLAUDE.md` | TRIM | 183 -> ~90 |
| `Domain/AppointmentAccessors/CLAUDE.md` | TRIM | 163 -> ~80. Update "MVP dormant" (`CreateOrLinkAsync` now active, Phase 11i) |
| `Domain/ApplicantAttorneys/CLAUDE.md` | TRIM | 156 -> ~80 |
| `Domain/AppointmentEmployerDetails/CLAUDE.md` | TRIM | 126 -> ~70 |
| `Domain/AppointmentApplicantAttorneys/CLAUDE.md` | TRIM | 113 -> ~70 |
| `Domain/Invitations/CLAUDE.md` | KEEP | 65, lean + accurate |
| `Domain/Books/CLAUDE.md` | KEEP | 102, accurate (documents vestigial scaffold) |
| `Domain/States/CLAUDE.md` | FOLD | 154 -> delete; 1-line gotcha (5 SetNull FKs, no max-length, `ObjectMapper` violation) into Domain layer "Thin lookups" |
| `Domain/AppointmentStatuses/CLAUDE.md` | FOLD | 176 -> delete; gotcha = DAT-05 entity/enum disconnect |
| `Domain/AppointmentTypes/CLAUDE.md` | FOLD | 169 -> delete; gotcha = host-scoped + 4 inbound FKs |
| `Domain/AppointmentLanguages/CLAUDE.md` | FOLD | 155 -> delete; gotcha = dual-DbContext anomaly + missing seed |
| `Domain/WcabOffices/CLAUDE.md` | FOLD | 143 -> delete; preserve download-token CSRF pattern as a note in the HttpApi layer file (reusable showcase) |
| `Domain/AppointmentDocuments/CLAUDE.md` | CREATE | ~90. Two entities (AppointmentDocument + AppointmentPacket), two managers, `CreateQueued` factory, blob containers, packet-merge Hangfire job, VerificationCode anonymous upload |
| `Domain/AppointmentChangeRequests/CLAUDE.md` | CREATE | ~70. Dual-ctor; cancel-window gate (SystemParameter); reschedule slot-hold + state-machine call |
| `Domain/Notifications/CLAUDE.md` | CREATE | ~80. RecipientResolver(s), 6+ Hangfire jobs, SlotCascadeHandler (now log-only stub), dispatcher fan-out |
| `Domain/NotificationTemplates/CLAUDE.md` | CREATE | ~70. TemplateCode string keys, EmailBodyResources embedded HTML, substitutor, seeder, 59 codes (mostly stub bodies) |

Moderate Domain features `AppointmentTypeFieldConfigs`, `CustomFields`, `PackageDetails`,
`DefenseAttorneys`: see Decision 2 (recommend folding 1-2 line facts into the Domain layer
file's "Notable features", not separate files).

### 3c. Application + Angular feature files

| Path | Action | Note |
| --- | --- | --- |
| `Application/ExternalSignups/CLAUDE.md` | KEEP | 122, accurate (documents missing `[RemoteService]`, etc.) |
| `angular/src/app/appointments/CLAUDE.md` | CREATE | ~95. 55-field FormGroup owned by parent; 7 template-only section children; race-safe version counters; AA/DA toggle confirmation modal; blob-download rule |
| `angular/src/app/patients/CLAUDE.md` | CREATE | ~70. SSN Design B: never pre-fill; `SsnInputComponent` sole entry surface; profile dual-load (external-users/me vs patients/me) |
| `angular/src/app/doctor-availabilities/CLAUDE.md` | CREATE | ~60. generate/add load one component; `toIdArray` collapses MTM objects; empty selectedDays = all-days |
| `angular/src/app/shared/CLAUDE.md` | CREATE | ~95. Address provider abstraction (Smarty/mock swap via env); `AppLookupSelect` OnPush fix; `SsnInput`/`SsnMask`; `performFullLogout`; `postLoginRedirectGuard` canMatch; `resolveStateId` |

Angular `internal-users` / `external-users`: see Decision 3 (recommend folding into the
Angular layer file's "Notable features").

Totals: 13 creates, 5 deletes (folded), 9 trims, 8 updates, 1 rewrite, 4 keeps.

---

## 4. Documentation IA overhaul

### 4a. Per-doc template (applied across docs/)

```markdown
# <Title>

> Purpose: <one line>. Audience: <role>. Last verified: YYYY-MM-DD vs <commit-ish>.

<body>

## Related
- <relative path> -- <why>
```

### 4b. Target tree (after prune + restructure)

```
docs/
  INDEX.md                 <- REWRITE: map the ACTUAL tree, zero dead links
  GLOSSARY.md              keep
  parity-review-log.csv    keep (active decision register)
  architecture/  api/  backend/  database/   <- UPDATE for current code
  business-domain/  frontend/  design/        <- UPDATE drift; keep accurate ones
  decisions/               <- canonical ADRs; absorb docs/adrs/; add 4 new ADRs
  security/                <- UPDATE (Patient IMultiTenant resolved; add SSN-reveal egress)
  runbooks/                <- heavy prune (see 4c) + fix wave-1 + DOCKER-DEV staleness
  repo-map/                <- regenerate map.md + index.json (stale since 2026-04-13)
  onboarding/              <- fix DOTNET_ENVIRONMENT + dead .claude/discovery ref
  devops/  research/  testing/  plans/        keep (light fixes)
  (removed) adrs/          -> merged into decisions/
  (removed) demo-readiness/  status-reports/  superpowers/   -> pruned (see 4c)
```

INDEX shape: stack table; "I want to..." quick-nav (only live links); section list grouped
by the dirs above; each entry one line. It is linked from the root `CLAUDE.md` Map.

### 4c. Prune list (delete; recoverable via git). All grep-verified unreferenced unless noted.

| Path | Reason |
| --- | --- |
| `docs/runbooks/findings/demo-prep/` (README, stack-quirks, demo-qa, spa-load-perf, container-logs-sweep, + bundle) | Tuesday 2026-05-27 demo passed; point-in-time only. Screenshots are historical -- confirm in Decision 6 whether to keep the 9 PNGs |
| `docs/runbooks/findings/2026-05-25-tuesday-demo-script.md` | Click-by-click script for a completed demo |
| `docs/runbooks/findings/2026-05-24-tuesday-demo-prep-handoff.md` | Session handoff for a passed demo; merge log is in git |
| `docs/runbooks/findings/2026-05-25-demo-polish-inventory.md` | Triaged; F4-01 shipped, rest closed |
| `docs/runbooks/findings/bugs/OBS-33-bug-021-duplicate-id.md` | Housekeeping meta-finding, resolved |
| `docs/runbooks/findings/bugs/OBS-9-port-pinning-env.md` | Superseded same day by per-worktree .env |
| `docs/demo-readiness/2026-05-11-pre-demo.md`, `...2026-05-12-pre-demo-rerun-post-fixes.md` | Pre-demo snapshots; blocking bugs fixed + merged |
| `docs/status-reports/2026-05-18-status-for-manager.md` | Dated roadmap; 120+ commits later, listed gaps shipped |
| `docs/superpowers/specs/2026-05-20-task-{a,b,c}-*.md` | Tasks merged via #208 (2026-05-22); plan copies already deleted by ship-plan; orphans |

Not pruned despite looking stale: `docs/parity/wave-1-parity/_parity-flags.md` is referenced
by `CaseEvaluationDomainErrorCodes.cs:692` (code) -- KEEP, flag the code path (section 6).
`docs/research/proxy-regen-stringvalues-fix.md` is a still-open upstream bug -- KEEP.

### 4d. Accuracy fixes (UPDATE, not prune) -- highest-drift docs

- `backend/PERMISSIONS.md`: 16 -> 30+ groups; add Appointments Approve/Reject/Request*,
  Patients.RevealSsn, DefenseAttorneys, AppointmentDocuments/Packets, CustomFields, etc.
- `database/EF-CORE-DESIGN.md`: 14 -> 34+ DbSets.
- `backend/APPLICATION-SERVICES.md`: 7 -> 25+ AppServices; fix slot-booking section (no
  longer marks slot Booked on create); AppointmentsAppService deps 11 -> 25.
- `backend/ENUMS-AND-CONSTANTS.md`: add CustomFieldType, RequestStatusType; ExternalUserType
  location + Adjuster=5.
- `architecture/MULTI-TENANCY.md` + `database/MIGRATION-GUIDE.md`: 20+ new entities; 24 -> 46
  host migrations.
- `security/{AUTHORIZATION,DATA-FLOWS,HIPAA-COMPLIANCE,THREAT-MODEL}.md`: Patient IMultiTenant
  resolved (FEAT-09); add `GetFullSsnAsync` egress + RevealSsn permission.
- `frontend/{ROUTING-AND-NAVIGATION,APPOINTMENT-BOOKING-FLOW,ANGULAR-ARCHITECTURE,
  COMPONENT-PATTERNS,ROLE-BASED-UI}.md`: /account removed; +4 routes; booking form split into
  7 sections; Claim Examiner = 4th external role; provider list 26.
- `design/_design-tokens.md`: reconcile with `angular/src/styles/_brand.scss` (divergent
  variable names + `--brand-primary` #06519f vs #055495). PRESERVE the file (cited by CLAUDE.md).
- `runbooks/DOCKER-DEV.md`: 6 -> 9 compose services (minio, minio-init, gotenberg); refresh
  last-tested; fix dead `devops/DEVELOPMENT-SETUP.md` + `runbooks/INCIDENT-RESPONSE.md` links.
- `runbooks/ENGINEERING-ROADMAP.md` + bug files: BUG-012 (#238) and BUG-036 (#247) are fixed;
  SEED-2 promote needs-rehydration -> open; remove dead OBS-2..7 index rows.

---

## 5. ADR additions (Nygard format, `docs/decisions/NNN-*.md`)

Existing: 001 Mapperly, 002 manual controllers, 003 dual DbContext, 004 doctor-per-tenant,
005 no-ng-serve, 006 subdomain routing; plus orphan `docs/adrs/ADR-007-host-aware-tenant-
resolver.md`. One-doctor-per-tenant already exists (004) -- NOT re-added.

- MOVE `docs/adrs/ADR-007-*` -> `docs/decisions/007-host-aware-tenant-resolver.md`; delete
  `docs/adrs/`; fix `decisions/README.md` index (currently says next=006; actual next = 012).
- ADD 008 capacity-aware slot rework (#267).
- ADD 009 audited SSN reveal, design B (#272) -- two-gate model, mask-on-standard-payload.
- ADD 010 DOCX -> PDF packet generation (immutability rationale).
- ADD 011 per-role packet access / PacketVisibility allow-list (#270).

---

## 6. parity-v2 reconciliation + code refs to flag

Reconcile (one-line "Resolved by #NNN (YYYY-MM-DD)" cites; do NOT re-run the audit):
- `parity-v2/04-emails-sms.md` G-04-05, G-04-09 -> #268.
- `parity-v2/01-booking.md` G-01-08 + DA toggle -> #269.
- `parity-v2/03-documents-packets.md` E7 -> #271; E8 -> #270; E3 queue-timing note.
- `parity/_parity-flags.md` PF-001 already resolved (#272) -- verify only.
- `parity-v2/INDEX.md` roll-up counts updated.
- Preserve: `parity/_parity-flags.md`, `parity/samples/poc/`, `parity-review-log.csv`.

Code references to FLAG only (application code is out of scope -- list for Adrian, do not edit):
- `NotificationTemplateConsts.cs:29`, `ChangeRequestRejectedEmailHandler.cs:90`,
  `JdfAutoCancelledEmailHandler.cs:113` -> doc-comment path `docs/parity/it-admin-notification-
  templates.md` was deleted in #273.
- `CaseEvaluationDomainErrorCodes.cs:692` -> points at `docs/parity/wave-1-parity/_parity-
  flags.md` (file still exists; canonical is now `docs/parity/_parity-flags.md`).

In-scope reference fixes we WILL make: `.claude/rules/dotnet-env.md` uses
`src/HCS.CaseEvaluation.HttpApi.Host` -- real path is `src/HealthcareSupport.CaseEvaluation.
HttpApi.Host` (a rule file, ours to fix); root `CLAUDE.md:107` dead parity link.

Also fix `.claude/rules/` drift while in there: `angular.md` ("ABP proxy bundler" phrasing +
missing Vite-DI reason), `dotnet.md` (add `[RemoteService(IsEnabled=false)]`, Mapperly
partial pattern, two-context note) -- keeping each rule a true subset that does not contradict
the layer files.

---

## 7. Execution groups (in order; commit per group; PR to main at end, no merge)

- **(a) Root hub + layer CLAUDE.md + rules.** Rewrite root; create Domain/HttpApi.Host/
  AuthServer/DbMigrator/HttpApi.Client; update the 5 existing layer files + angular layer;
  fix `.claude/rules/{dotnet-env,angular,dotnet}.md`.
- **(b) Per-feature CLAUDE.md.** Trim 9 Domain files; fold + delete 5; create 4 Domain + 4
  Angular feature files (per Decisions 2-3).
- **(c) Docs accuracy + prune.** Apply 4d fixes; delete 4c prune list; merge `adrs/` ->
  `decisions/`; regenerate repo-map.
- **(d) Docs restructure + INDEX + template.** Rewrite INDEX; apply per-doc template; verify
  every link resolves.
- **(e) parity-v2 reconciliation + ADRs + flag code refs.** Section 5 + 6.

Commit messages: conventional-commit, scope `docs` / `claude-md`; rationale inline; no
internal doc paths or ticket codes in the body (per commit-format rule).

---

## 8. Verification checklist (run after each group; all must pass at the end)

- [ ] `wc -l` every `CLAUDE.md` < 200; report each + the combined footprint.
- [ ] No `CLAUDE.md` uses `@import` to load a doc; references are plain-text paths.
- [ ] Every "Related"/Map path resolves to a file that exists post-change (grep + test).
- [ ] No nested `CLAUDE.md` duplicates root/parent content (spot-check overlaps).
- [ ] No contradictions across root/layer/feature/rules on: ABP `[RemoteService]`, Mapperly,
      Angular build (no ng serve), two-DbContext, SSN handling.
- [ ] `docs/INDEX.md` exists, is linked from root, maps the tree, has zero dead links.
- [ ] Every doc carries the template header (title/purpose/audience/last-verified).
- [ ] Prune list is gone from the tree; preserved files (parity-v2, _parity-flags, POC
      samples, design-tokens, fixtures) still present (grep before each delete).
- [ ] parity-v2 items closed by #268-272 each carry a one-line resolved cite.
- [ ] Re-verify >= 5 reconciled doc claims against current code (e.g. DbSet count, permission
      groups, route tree, slot-booking behavior, SSN egress).
- [ ] Code-ref flags (section 6) reported to Adrian; no application code edited.

---

## 9. Open decisions for Adrian (resolve at approval)

1. **Trivial lookups (States, AppointmentStatuses, AppointmentTypes, AppointmentLanguages,
   WcabOffices).** Recommend FOLD: delete the 5 files, move each 1-line gotcha into the Domain
   layer file's "Thin host-scoped lookups". Alternative: TRIM in place to ~30 lines each (keeps
   lazy-load granularity but keeps 5 files). Recommendation: FOLD.
2. **Moderate Domain features (AppointmentTypeFieldConfigs, CustomFields, PackageDetails,
   DefenseAttorneys).** Recommend FOLD their key facts into the Domain layer "Notable features"
   (1-2 bullets each) rather than 4 new files. Alternative: CREATE all 4. Recommendation: FOLD.
3. **Angular internal-users / external-users.** Recommend FOLD into the Angular layer file.
   Alternative: 2 own files. Recommendation: FOLD.
4. **Root rewrite.** Recommend a general repo hub on `main`; the 254-line replicate-old-app
   PRIMARY MISSION is branch-specific and stays on `feat/replicate-old-app` only. The new hub
   will still point to `docs/parity-v2/` and note the replicate effort. Confirm `main`'s root
   no longer carrying the full mission is acceptable.
5. **HttpApi.Client.** Recommend a ~28-line own file (consistency: every layer has one).
   Alternative: a 3-line note in the root hub. Recommendation: own file.
6. **demo-prep screenshots (9 PNGs).** Prune with the demo-prep bundle, or keep as historical
   evidence? Recommendation: prune (recoverable via git).

---

## 10. Sources

Official (verified verbatim): code.claude.com/docs/en/memory; code.claude.com/docs/en/best-
practices; anthropic.com/engineering/claude-code-best-practices; anthropic.com/research/long-
running-Claude. Community/power-user consensus (40+ sources) cross-checked against the above;
notable: bswen.com (bloat), techsy.io (9 rules 2026), obviousworks.ch (WHAT/WHY/HOW, ~70%
advisory ceiling, hooks-vs-CLAUDE.md), datacamp (deletion test, positive alternatives),
boringbot/Kjramsy (41% overhead cut via path-scoped rules; @import myth), HN #46098838 (canary
phrase), awesome-claude-code. Three myths to avoid repeating: `@import` saves tokens (false);
`#` quick-add (deprecated); nested files survive `/compact` (they do not).
