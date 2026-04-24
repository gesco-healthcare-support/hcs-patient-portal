# Implementation Research -- Patient Portal MVP

> Per-capability solution briefs, a dependency graph, open-question routing, and a
> wave-ordered implementation plan for the MVP gaps identified in
> `docs/gap-analysis/`. Read-only research phase. No application code is modified
> on this branch. All writes live inside `docs/implementation-research/`.
>
> Branch: `docs/implementation-research`. Worktree: `W:/patient-portal/implementation-research/`.

## Purpose

The gap analysis at `docs/gap-analysis/` identifies 30 to 35 MVP-blocking capability
gaps plus 5 NEW-side security defects and 1 quality defect across ~100 track-level
findings. It makes no recommendation on solution or sequence. This tree:

1. Collapses the 130 in-scope gap IDs into 39 capability groups (each a coherent
   implementation unit).
2. Produces a per-capability solution brief grounded in NEW-version code, live API
   probes, and authoritative external sources (Phase 2).
3. Builds a dependency graph across capabilities (Phase 3 -> `dependencies.md`).
4. Routes scope-blocked capabilities to the 32 open questions in the gap-analysis
   README (Phase 4 -> `blocked-on-scope.md`).
5. Orders the unblocked capabilities into implementation waves (Phase 5 -- updates
   this README).

## Phase status

| Phase | Status | Output |
|---|---|---|
| 0. Worktree setup | Done | This tree on branch `docs/implementation-research`, based on main |
| 1. Inventory + scaffold | Done | 39 capability stubs + 3 index files |
| 1.5. Service verification | Pending | `probes/service-status.md` |
| 2. Per-capability research | Pending | `solutions/*.md` filled by subagent batches |
| 3. Dependency synthesis | Pending | `dependencies.md` (Mermaid + waves) |
| 4. Blocked-on-scope routing | Pending | `blocked-on-scope.md` (Q-grouped) |
| 5. Wave-ordered plan | Pending | This README -- final wave list + roll-up |
| 6. Handoff | Pending | Final status message, no push, no PR |

## Directory layout

- `README.md` (this file) -- capability inventory + schema + protocols + phase status.
- `solutions/<slug>.md` -- 39 per-capability briefs (stubs until Phase 2 completes).
- `probes/<slug>-<ISO-timestamp>.md` -- live-probe logs. Every `## Live probes`
  bullet in a brief has a matching log file here.
- `probes/service-status.md` -- running state of NEW services when Phase 2 began.
- `dependencies.md` -- Phase 3 output.
- `blocked-on-scope.md` -- Phase 4 output.

## Source inputs (read before any brief)

Every Phase 2 brief author reads these inputs before writing. Do not summarise from
memory; read the files.

1. `docs/gap-analysis/README.md` -- master aggregated gap tables, the 32 consolidated
   open questions (lines 227-271), and the post-MVP deferral list.
2. `docs/gap-analysis/10-deep-dive-findings.md` -- 4 errata (PDF server-side never
   called, SMS fully disabled in transitions, scheduler hardcoded-`1` bug,
   CustomField is fixed-type not dynamic), 5 NEW-SEC defects, and applicable ABP
   research pointers. **Erratum claims override any contradicting claim in tracks
   01 through 09.**
3. Repository root `CLAUDE.md` -- service start order (AuthServer 44368 -> HttpApi.Host
   44327 -> Angular 4200), no `ng serve` (ADR-005), never edit `angular/src/app/proxy/`,
   short project path requirement on Windows.
4. `docs/decisions/001-..` through `005-..` -- 5 ADRs defining intentional
   architectural differences. No brief may reverse these:
   - ADR-001: Riok.Mapperly over AutoMapper
   - ADR-002: Manual controllers, every AppService carries `[RemoteService(IsEnabled = false)]`
   - ADR-003: Dual DbContext (`CaseEvaluationDbContext` host + tenant, `CaseEvaluationTenantDbContext` tenant-only)
   - ADR-004: Doctor-per-tenant multi-tenancy (the doctor IS the tenant)
   - ADR-005: Never use `ng serve`, always `ng build` + `npx serve`
5. The `## Summary` and `## Delta` sections of each per-track doc
   `docs/gap-analysis/01-..md` through `09-..md`. Skip the `## OLD version state`
   and `## NEW version state` inventories unless the specific capability requires
   drilling for file:line evidence.

## Capability inventory (39 total)

Every in-scope gap ID appears in exactly one capability below.

### Foundation / infrastructure

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [lookup-data-seeds](solutions/lookup-data-seeds.md) | Lookup data seeds (States/Types/Languages/Statuses/Locations/WcabOffices) | DB-15 | Q23 |
| [internal-role-seeds](solutions/internal-role-seeds.md) | Internal role seeds + structure | DB-16, 5-G01, 5-G02, 5-G03, 5-G04 | Q21, Q22 |
| [blob-storage-provider](solutions/blob-storage-provider.md) | Blob storage provider (DB BLOB vs S3) | CC-04 | Q17 |
| [email-sender-consumer](solutions/email-sender-consumer.md) | Email sender consumer (`IEmailSender` wiring) | CC-01 | -- |
| [sms-sender-consumer](solutions/sms-sender-consumer.md) | SMS sender consumer (Twilio / ABP SMS) | CC-02 | track-10 erratum (SMS fully disabled in OLD) |
| [background-jobs-infrastructure](solutions/background-jobs-infrastructure.md) | Background jobs infrastructure (ABP BackgroundJobs vs Hangfire) | CC-03 | Q18 |

### Appointment lifecycle (core)

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [appointment-state-machine](solutions/appointment-state-machine.md) | 13-state transition enforcement | G2-01 | Q5 |
| [appointment-booking-cascade](solutions/appointment-booking-cascade.md) | DoctorAvailability booking cascade on reschedule/cancel/delete | G2-02 | -- |
| [appointment-lead-time-limits](solutions/appointment-lead-time-limits.md) | Lead-time + max-time constraints | G2-03 | -- |
| [appointment-full-field-snapshot](solutions/appointment-full-field-snapshot.md) | Full-field snapshot (CancelledById, RejectedById, reasons) + RoleAppointmentType gate | G2-10, G2-12 | -- |
| [appointment-search-listview](solutions/appointment-search-listview.md) | `/appointment-search` standalone + AllAppointmentRequest permission | UI-07, G-API-19, 5-G07 | -- |

### Appointment workflows

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [appointment-change-requests](solutions/appointment-change-requests.md) | Reschedule / cancel change-request workflow + pending queue | DB-02, G2-06, 03-G05, A8-09, R-01, R-02, R-10, UI-04, UI-05, UI-06, G-API-10 | -- |
| [appointment-change-log-audit](solutions/appointment-change-log-audit.md) | Change log + PHI audit trail | DB-03, G2-13, DB-14, 5-G06, A8-10, R-03, UI-01, G-API-09 | Q14 |
| [appointment-injury-workflow](solutions/appointment-injury-workflow.md) | Injury details + body parts + claim examiner + primary insurance | G2-07, DB-07, DB-08, DB-09, 03-G04, A8-07, G-API-11 | Q3, Q4 |
| [appointment-accessor-auto-provisioning](solutions/appointment-accessor-auto-provisioning.md) | Auto-create external accessor users | G2-05, A8-04 | -- |
| [appointment-notes](solutions/appointment-notes.md) | Notes thread per appointment | DB-10, 03-G10, A8-01, UI-17, G-API-06 | Q10 |
| [scheduler-notifications](solutions/scheduler-notifications.md) | Scheduler + 9 recurring notification jobs | G2-11, 03-G09, G-API-15 | depends on email/SMS/jobs resolution |

### Documents

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [appointment-documents](solutions/appointment-documents.md) | Appointment documents (pre-approval + new-documents + types) | DB-01, G2-08, 03-G01, 03-G02, 03-G03, 5-G05, G-API-01, G-API-02, A8-05, A8-06, R-04, R-05, R-06, UI-02, UI-03, UI-12 | -- |
| [anonymous-document-upload](solutions/anonymous-document-upload.md) | Magic-link anonymous document upload | R-09, UI-13, G-API-03 | Q15 |
| [joint-declarations](solutions/joint-declarations.md) | Joint declarations upload + approval | DB-04, G2-14, 03-G06, A8-08, R-07, UI-14, G-API-04 | -- |
| [document-packages](solutions/document-packages.md) | Document packages + package details | DB-13 | Q9 |

### Patient + attorney + configuration

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [patient-auto-match](solutions/patient-auto-match.md) | 3-of-6 fuzzy match on patient create | G2-04 | -- |
| [attorney-defense-patient-separation](solutions/attorney-defense-patient-separation.md) | Defense vs Patient vs Applicant attorney modelling | DB-05, DB-06, G2-09 | Q1, Q2 |
| [custom-fields](solutions/custom-fields.md) | Custom fields per appointment type | DB-11, 03-G12, 5-G10, A8-03, UI-08, G-API-07 | Q6 |
| [templates-email-sms](solutions/templates-email-sms.md) | Email + SMS notification templates | DB-12, 03-G13, 5-G12, UI-15, G-API-05 | Q7 |

### Admin + reporting

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [appointment-request-report-export](solutions/appointment-request-report-export.md) | Report search page + CSV/XLSX/(PDF) export | 03-G07, 03-G11, 5-G08, G-API-12, G-API-13, A8-13, R-08, UI-11 | Q12 |
| [dashboard-counters](solutions/dashboard-counters.md) | Dashboard counter cards per role | 03-G08, A8-11, G-API-14 | Q13 |
| [user-query-contact-us](solutions/user-query-contact-us.md) | UserQuery / contact-us inbox | 03-G14, G-API-08 | Q11 |
| [users-admin-management](solutions/users-admin-management.md) | Users admin UI (delegate to ABP Identity) | 5-G09, A8-02, UI-10 | -- |
| [system-parameters-vs-abp-settings](solutions/system-parameters-vs-abp-settings.md) | System parameters vs ABP SettingManagement | DB-17, 5-G11, UI-09, G-API-16 | Q8 |

### External-user + account flows

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [account-self-service](solutions/account-self-service.md) | Email verification + forgot-password self-service | 5-G13, 5-G14, A8-12 | Q16 |
| [external-user-home](solutions/external-user-home.md) | External-user `/home` landing page | UI-16 | track-09 Q1 (external-user UX) |

### REST API parity

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [rest-api-parity-cleanup](solutions/rest-api-parity-cleanup.md) | PATCH + composite-delete + doctor M2M + orphan lookups | G-API-17, G-API-18, G-API-20, G-API-21 | Q28 (PATCH) |

### NEW-side defects

| slug | title | gap IDs | scope-block (Q#) |
|---|---|---|---|
| [new-sec-01-appointment-route-permission-guard](solutions/new-sec-01-appointment-route-permission-guard.md) | Add `permissionGuard` to `/appointments/view/:id` and `/add` | NEW-SEC-01 | -- |
| [new-sec-02-method-level-authorize](solutions/new-sec-02-method-level-authorize.md) | Method-level `[Authorize]` on Create/Edit/Delete across AppServices | NEW-SEC-02 | -- |
| [new-sec-03-transactional-tenant-provisioning](solutions/new-sec-03-transactional-tenant-provisioning.md) | Transactional UoW wrapping `DoctorTenantAppService.CreateAsync` | NEW-SEC-03 | -- |
| [new-sec-04-external-signup-real-defaults](solutions/new-sec-04-external-signup-real-defaults.md) | Remove hardcoded Gender/DOB/PhoneType in ExternalSignup | NEW-SEC-04 | -- |
| [new-sec-05-hsts-header](solutions/new-sec-05-hsts-header.md) | Emit `Strict-Transport-Security` header | NEW-SEC-05 | -- |
| [new-qual-01-critical-path-test-coverage](solutions/new-qual-01-critical-path-test-coverage.md) | Tests for tenant provisioning + permissions + external signup + tenant filter | NEW-QUAL-01 | -- |

## Per-capability brief schema

Every `solutions/<slug>.md` follows this exact schema. Sections marked (conditional)
may be omitted only when the capability genuinely has nothing under them (a brief
for a pure NEW-side defect has no OLD reference; a brief for a pure schema gap has
no live probe). Subagents MUST NOT omit other sections.

```markdown
# <Capability name>

## Source gap IDs
Bullet list with relative links to track docs (e.g. `../gap-analysis/02-domain-entities-services.md`).

## NEW-version code read
5 to 15 bullets. Cite `path:line`. Describe what exists, what is stubbed, which
ABP modules are wired without consumers.

## Live probes (conditional)
0 to 6 bullets. Each bullet records a probe: full curl command, HTTP status,
body excerpt (redacted), timestamp, what the probe proves. Link to the full
log at `../probes/<slug>-<ISO-timestamp>.md`.

## OLD-version reference (conditional)
0 to 10 bullets. `path:line` citations. Flag any track-10 errata that apply.

## Constraints that narrow the solution space
- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict
- Row-level `IMultiTenant` (ADR-004), doctor-per-tenant
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003),
  no ng serve (ADR-005)
- HIPAA applicability (one-line reason)
- Capability-specific constraints

## Research sources consulted
Minimum 3, target 5-8 URLs with access dates. Mix ABP docs, Microsoft Learn,
NuGet READMEs, GitHub issues, StackOverflow (>=3 upvotes). If none exist, document
the dead-end searches.

## Alternatives considered
3 to 5 candidates. Each: one-paragraph description, `chosen | rejected | conditional`
tag, one-line reason. If only one viable: state "no viable alternative because
<reason>" with the eliminating constraint cited.

## Recommended solution for this MVP
Paragraph. WHAT, WHERE (project + folder), WHICH ABP primitive. Shape:
entity -> domain -> app service -> controller -> proxy -> Angular -> migration.
No code block over 20 lines.

## Why this solution beats the alternatives
2 to 4 bullets tying to stated constraints.

## Effort (sanity-check vs inventory estimate)
Inventory says X (S/M/L/XL). Analysis confirms or adjusts to Y. One-line rationale.

## Dependencies
- Blocks: <slugs> with reason per edge
- Blocked by: <slugs> with reason per edge
- Blocked by open question: `<verbatim quote from gap-analysis README>` or `none`

## Risk and rollback
- Blast radius: what breaks if implementation goes wrong
- Rollback: exact revert path (flag toggle, migration down, branch revert)

## Open sub-questions surfaced by research
Or "None".
```

## Live Verification Protocol

### MAY probe (read-only)

- `GET http://localhost:44327/swagger/v1/swagger.json` and any `GET /api/app/**`
  read-only endpoint. Proves endpoint existence, response shape, empty-vs-populated
  tables.
- `GET http://localhost:44368/.well-known/openid-configuration` and
  `GET /connect/*` metadata endpoints. Proves OIDC grant types and scopes.
- `POST http://localhost:44368/connect/token` with the seeded `admin@abp.io` /
  `1q2w3E*` (password grant). Against LocalDB only. Issues a token for subsequent
  probes. Token lives only in the probe log; never embed in a brief.
- `GET http://localhost:4200/` and Angular routes via Chrome DevTools MCP (if
  available). Confirms UI presence of ABP module pages.
- Any read-only `curl -H "Authorization: Bearer <token>" http://localhost:44327/...`.

### MAY probe (state-mutating, conditional)

State-mutating probes (POST/PUT/DELETE) are permitted ONLY for:

- Proving `NEW-SEC-02` (method-level authorization bypass) against LocalDB.

Conditions:

- Log the exact command, request body, full response, before-state and after-state.
- Issue the reverse operation in the same subagent run (DELETE the created row,
  PUT the original back) and log the reversal.
- Never probe SaaS tenant creation, IdentityUser creation, OpenIddict client
  creation, ApplicantAttorney creation, or Patient creation. These leave
  persistent state manual cleanup might miss.

### MUST NOT

- Run `dotnet ef database update`, `dotnet ef migrations add`, `DbMigrator`, or any
  schema-mutating command.
- Run `dotnet run`, `npm run start`, `npx serve`, `ng build`, `ng serve`. Services
  are started by the orchestrator in Phase 1.5 only, from the `main/` worktree.
- Probe `appsettings.secrets.json` or production-shaped credentials.
- Log Bearer tokens into briefs. Tokens live only in probe logs and must be
  redacted to `Bearer <REDACTED>` when the probe log is reviewed.
- Probes that leave persistent LocalDB rows beyond the subagent's lifetime.

### Probe log format

`probes/<slug>-<ISO-timestamp>.md`:

```markdown
# Probe log: <slug>

**Timestamp (local):** 2026-04-24T12:34:56
**Purpose:** <what this probe proves>

## Command
\```
curl -sk -H "Authorization: Bearer <REDACTED>" http://localhost:44327/api/app/...
\```

## Response
Status: 200
Body (redacted):
\```
{ ... }
\```

## Interpretation
<what this tells us about the capability>

## Cleanup (if mutating)
<reverse-op command + confirmation of revert>
```

## Constraints that apply to every brief

- ASCII only. No emoji, smart quote, em dash, or Unicode decoration anywhere.
  Per `.claude/rules/code-standards.md` and the commit-message hook.
- No PHI. No real patient names, IDs, or medical records in any example, fixture,
  probe payload, or log excerpt. Use synthetic data only. Per HIPAA.
- Cite every claim. `path:line` for repo evidence, URL with access date for
  external, full command + response for live probes. Per
  `.claude/rules/zero-trust-verification.md`.
- Honour the 5 ADRs. No suggestion that reverses Mapperly, manual controllers,
  dual DbContext, doctor-per-tenant, or the no-ng-serve workaround.
- Effort estimates (S/M/L/XL) use the inventory's scale. S ~= 0.5 to 1 day,
  M ~= 2 to 5 days, L ~= 5 to 10 days, XL ~= 10+ days.
- No code blocks longer than 20 lines in briefs. Link to reference implementations
  in the NEW codebase (e.g. `Doctors/DoctorsAppService.cs`) or ABP docs instead.

## Tooling allowlist

### Orchestrator (main session)

- `Read`, `Write`, `Edit`, `Glob`, `Grep`.
- `Bash`: git operations inside the research worktree, service-start commands
  inside the `main/` worktree, curl probes against localhost.
- `WebFetch` for external research.
- `Agent` tool for subagent dispatch (parallel batches, `general-purpose` type).

### Subagents (per-capability)

- `Read`, `Write`, `Edit`, `Glob`, `Grep` inside `docs/implementation-research/`.
- `Bash`: curl probes only (read-only by default; mutating only under the
  Live Verification Protocol conditions above).
- `WebFetch` for external research.
- No `Agent` nesting.

### Forbidden everywhere

- `dotnet run`, `dotnet ef *`, `DbMigrator` in any worktree.
- `npm run *`, `ng serve`, `ng build` by subagents (orchestrator only, in
  `main/` worktree, Phase 1.5 only).
- `git push`, `gh pr create`, any remote-facing git command.
- Any file write outside `docs/implementation-research/` on this branch.

## Open questions (reference)

Full verbatim text lives at `docs/gap-analysis/README.md:227-271`. The 32
questions are grouped:

- Feature-scope (Q1-Q16): affect whether capabilities stay in MVP.
- Architecture (Q17-Q24): affect how a capability is built.
- Security / compliance (Q25-Q27): partly acknowledged by Adrian.
- Process / confirmation (Q28-Q32): verification items.

Phase 4 groups capabilities by Q# with verbatim quotes in `blocked-on-scope.md`.

## Recovery

If the research goes sideways at any phase, the entire worktree can be removed:

```bash
cd W:/patient-portal/main
git worktree remove --force ../implementation-research
```

This deletes the working directory. The branch `docs/implementation-research` is
preserved (per Adrian's never-auto-delete-branches rule). Nothing outside the
worktree is touched, so there is nothing else to revert.
