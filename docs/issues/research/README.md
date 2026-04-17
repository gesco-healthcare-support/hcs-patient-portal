[Home](../../INDEX.md) > [Issues](../) > Research

# Issue Research Pack

Structured research for every open issue tracked in [docs/issues/](../). Each file contains:

- **Current state** verified against the codebase on 2026-04-17.
- **Official documentation** (ABP, Microsoft, NIST, OWASP, CA DWC) with URLs.
- **Community findings** (ABP forum threads, GitHub issues, Stack Overflow, practitioner blogs).
- **Recommended approach** (the point where research converges; no code).
- **Gotchas / blockers** others hit on the same path.
- **Open questions** -- product vs engineering decisions still needed.
- **Related** links to adjacent issues.

The pack is designed to be picked up cold: open the issue file, expand on the recommended approach, and start implementing.

---

## Coverage

### Critical + High severity (14)

#### Security

| ID | Title | Severity |
|---|---|---|
| [SEC-01](SEC-01.md) | Secrets committed to source control (partially mitigated) | Critical |
| [SEC-02](SEC-02.md) | PII logging enabled by default | High |
| [SEC-03](SEC-03.md) | External user lookup unauthenticated | High |
| [SEC-05](SEC-05.md) | Password policy fully relaxed | High |

#### Data Integrity

| ID | Title | Severity |
|---|---|---|
| [DAT-01](DAT-01.md) | Race condition on slot booking | Critical |
| [DAT-02](DAT-02.md) | Duplicate confirmation numbers possible | Critical |
| [DAT-03](DAT-03.md) | Reschedule does not release old slot | High |
| [DAT-04](DAT-04.md) | Non-transactional tenant creation | High |
| [DAT-05](DAT-05.md) | Disconnected status representations | High |

#### Confirmed Bugs

| ID | Title | Severity |
|---|---|---|
| [BUG-01](BUG-01.md) | Slot conflict detection logic inverted | High |
| [BUG-02](BUG-02.md) | Appointment status changes never persisted | High |

#### Incomplete Features

| ID | Title | Severity |
|---|---|---|
| [FEAT-01](FEAT-01.md) | Appointment status workflow has no implementation | High |
| [FEAT-02](FEAT-02.md) | Claim Examiner role has no UI or workflow | High |

#### Architecture

| ID | Title | Severity |
|---|---|---|
| [ARC-03](ARC-03.md) | Hardcoded placeholder values for Gender and DOB | High |

### Medium + Low severity (22)

#### Security

| ID | Title | Severity |
|---|---|---|
| [SEC-04](SEC-04.md) | CORS policy is wide open | Medium |

#### Data Integrity

| ID | Title | Severity |
|---|---|---|
| [DAT-06](DAT-06.md) | Missing DB indexes on frequently-queried FK columns | Medium |
| [DAT-07](DAT-07.md) | Missing unique constraints on `RequestConfirmationNumber` and `Patient.Email` | Medium |

#### Confirmed Bugs

| ID | Title | Severity |
|---|---|---|
| [BUG-04](BUG-04.md) | Slot preview uses only first input's location label | Medium |
| [BUG-05](BUG-05.md) | Slot save fires N+1 individual HTTP POSTs | Medium |
| [BUG-06](BUG-06.md) | `goBack()` always navigates to root | Low |
| [BUG-07](BUG-07.md) | `onSubmit()` error in `save()` silently swallowed | Low |
| [BUG-09](BUG-09.md) | Past-date appointments accepted without validation | Medium |
| [BUG-10](BUG-10.md) | `fromTime > toTime` accepted on slot creation | Medium |
| [BUG-12](BUG-12.md) | Page title shows "MyProjectName" (partially resolved) | Low |

#### Incomplete Features

| ID | Title | Severity |
|---|---|---|
| [FEAT-03](FEAT-03.md) | Tenant dashboard is a placeholder | Medium |
| [FEAT-04](FEAT-04.md) | `AppointmentEmployerDetail` and `AppointmentAccessor` have no Angular modules | Medium |
| [FEAT-05](FEAT-05.md) | Email system is not wired up | Medium |
| [FEAT-07](FEAT-07.md) | Near-zero test coverage | Medium |
| [FEAT-08](FEAT-08.md) | Swagger OAuth does not work from browser in Docker | Medium |

#### Architecture

| ID | Title | Severity |
|---|---|---|
| [ARC-01](ARC-01.md) | Vestigial `Books` entity from ABP scaffold | Medium |
| [ARC-02](ARC-02.md) | Business logic in the Application Service layer, not Domain | Medium |
| [ARC-04](ARC-04.md) | Role name strings duplicated across 8+ files | Medium |
| [ARC-05](ARC-05.md) | `AppointmentAddComponent` eagerly loaded | Low |
| [ARC-06](ARC-06.md) | DTO validation attributes missing on availability input DTOs | Low |
| [ARC-07](ARC-07.md) | Hardcoded English strings bypass localisation | Low |
| [ARC-08](ARC-08.md) | Missing `[RemoteService(IsEnabled = false)]` on 3 AppServices | Medium |

### Technical Open Questions (12)

Code-level unknowns from [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md). Most block a specific fix or cleanup decision.

| ID | Title | Blocks |
|---|---|---|
| [Q-01](Q-01.md) | Intended appointment status workflow (13-state enum, no state machine) | FEAT-01 implementation |
| [Q-02](Q-02.md) | `AppointmentStatus` lookup table vs enum -- source of truth | Database cleanup, FEAT-01 |
| [Q-03](Q-03.md) | Confirmation number uniqueness: global vs per-tenant | DAT-07 unique-constraint design |
| [Q-04](Q-04.md) | Claim Examiner role purpose (stubbed, excluded from external lookup) | FEAT-02 implementation |
| [Q-05](Q-05.md) | Orphan fields on Appointment (`InternalUserComments`, `IsPatientAlreadyExist`, `AppointmentApproveDate`) | AppointmentUpdateDto redesign |
| [Q-06](Q-06.md) | Minimum advance booking window (3-day policy?) | BUG-09 fix scope |
| [Q-07](Q-07.md) | `DoctorConsts.EmailMaxLength = 49` (unusual value) | DoctorConsts cleanup |
| [Q-08](Q-08.md) | Intended deployment target (Azure/AWS/on-prem?) | First deploy |
| [Q-09](Q-09.md) | Social OAuth providers configured but not in repo | AuthServer config |
| [Q-10](Q-10.md) | `Volo.FileManagement` wired up but unused | Dependency cleanup |
| [Q-11](Q-11.md) | ABP Commercial license ownership & transfer | Package restore continuity + SEC-01 |
| [Q-12](Q-12.md) | Hardcoded default password for auto-created patients | SEC-05 + FEAT-05 fix scope |

### Previous-Developer Questions (10)

Questions from [QUESTIONS-FOR-PREVIOUS-DEVELOPER.md](../QUESTIONS-FOR-PREVIOUS-DEVELOPER.md). **Not code-resolvable.** Each file provides indirect repo evidence, industry / regulatory context, a best-guess default answer with confidence label, and recommended channels for obtaining a definitive answer.

| ID | Title | Risk tier |
|---|---|---|
| [P-01](P-01.md) | Active client engagement / contract / SOW | Legal: breach of contract, IP ownership dispute |
| [P-02](P-02.md) | Prior production/staging deployment with real PHI | Legal: HIPAA breach notification obligations |
| [P-03](P-03.md) | HIPAA compliance decisions, BAAs, risk assessment | Legal: HIPAA civil/criminal exposure |
| [P-04](P-04.md) | Actual end client identity and contacts | Operational: no stakeholder to align with |
| [P-05](P-05.md) | External service accounts (SMTP, APM, domain, SSL, cloud) | Operational: services expire/fail silently |
| [P-06](P-06.md) | Verbal commitments to client not reflected in code | Product: building the wrong thing |
| [P-07](P-07.md) | Nature and circumstances of handover | Context: misreading "done" vs "abandoned" |
| [P-08](P-08.md) | User research / stakeholder feedback artefacts | Product: undoing deliberate design decisions |
| [P-09](P-09.md) | California DWC / carrier integration discussions | Product: self-contained vs connected |
| [P-10](P-10.md) | Undisclosed security vulnerabilities or prior incidents | Legal + Security: inheriting a compromised system |

### Resolved during verification (3)

Marked Fixed in source docs on 2026-04-17 after spot-check:

| ID | Title | Verification |
|---|---|---|
| BUG-03 | `GetDoctorAvailabilityLookupAsync` filter always false | Method has no WhereIf predicate in current code |
| BUG-11 | Menu labels show localization key prefixes | ~19 `Menu:*` keys present in `en.json` |
| FEAT-06 | No CI/CD pipeline | 17 GitHub Actions workflows under `.github/workflows/` |

### Not covered in this pack

- **BUG-08** -- already Fixed per the source issue doc.

---

## Suggested sequence when fixing

Several issues cluster -- fixing them together is more efficient than isolated fixes.

### Tier 1 -- Critical/High severity path

1. **Before touching anything**: resolve [DAT-05](DAT-05.md) (enum vs lookup). All status work downstream depends on this choice.
2. **Status workflow slice**: [DAT-05](DAT-05.md) + [BUG-02](BUG-02.md) + [FEAT-01](FEAT-01.md) together. Also wires up [DAT-03](DAT-03.md)'s slot-release triggers and [Q-05](Q-05.md)'s `AppointmentApproveDate`.
3. **Concurrency slice**: [DAT-01](DAT-01.md) + [DAT-02](DAT-02.md) + [DAT-03](DAT-03.md). Same `IAbpDistributedLock` pattern serves all three. Add [DAT-07](DAT-07.md) unique-index as a DB-level backstop.
4. **Security hardening**: [SEC-02](SEC-02.md), [SEC-04](SEC-04.md), [SEC-05](SEC-05.md) are config-level changes; single PR. [SEC-01](SEC-01.md) rotation and history purge is a separate op. [SEC-03](SEC-03.md) pairs with [FEAT-02](FEAT-02.md) since they touch the same method.
5. **Tenant provisioning**: [DAT-04](DAT-04.md) + [ARC-03](ARC-03.md) (both touch `DoctorTenantAppService`).
6. **Slot conflict**: [BUG-01](BUG-01.md) + [BUG-04](BUG-04.md) stand-alone; small; do anytime.
7. **Claim Examiner persona**: [FEAT-02](FEAT-02.md) with [SEC-03](SEC-03.md) fix. Product decision required first.

### Tier 2 -- Medium/Low hardening

1. **Backend validation**: [BUG-09](BUG-09.md), [BUG-10](BUG-10.md), [ARC-06](ARC-06.md) together -- all missing invariants on the same DTOs/AppServices.
2. **Layering refactor**: [ARC-02](ARC-02.md) -- hoists BUG-09/10 guards into Manager. Enables better test coverage ([FEAT-07](FEAT-07.md)).
3. **DB performance**: [DAT-06](DAT-06.md) -- index additions based on real query plans; pair with [DAT-07](DAT-07.md) unique constraints.
4. **Cleanup pass**: [ARC-01](ARC-01.md) Books, [Q-10](Q-10.md) FileManagement, [Q-05](Q-05.md) orphan fields -- all schema-dropping migrations. Consider bundling into a single "remove unused scaffolding" PR.
5. **Convention pass**: [ARC-04](ARC-04.md) role constants, [ARC-08](ARC-08.md) `[RemoteService]` attribute -- mechanical fixes.
6. **Angular UX**: [BUG-05](BUG-05.md), [BUG-06](BUG-06.md), [BUG-07](BUG-07.md), [ARC-05](ARC-05.md), [ARC-07](ARC-07.md), [BUG-12](BUG-12.md) -- frontend polish.
7. **Infrastructure**: [FEAT-05](FEAT-05.md) email + [FEAT-08](FEAT-08.md) Docker Swagger -- these unblock other work ([SEC-05](SEC-05.md) password reset, [ARC-03](ARC-03.md) profile nudge).
8. **Dashboard + feature modules**: [FEAT-03](FEAT-03.md), [FEAT-04](FEAT-04.md) -- product-driven UI work.
9. **Schema consistency**: [Q-07](Q-07.md) EmailMaxLength, [ARC-06](ARC-06.md) DTO `[StringLength]` -- bundle.
10. **OAuth decision**: [Q-09](Q-09.md) -- product + compliance decision, likely "remove social providers". Align with [SEC-01](SEC-01.md) secrets strategy.
11. **Test coverage**: [FEAT-07](FEAT-07.md) as ongoing work; add tests as each fix lands, not as a separate sprint.

---

## Verification method

Each issue was verified by reading the referenced source files on branch `fix/nullable-leftovers` at `de6f2f1` (HEAD on 2026-04-17). Findings:

- **37 of 40** issues in the original docs/issues/ catalogue remained open (14 Critical/High + 22 Medium/Low + 4 orphan Qs - 3 resolved during verification).
- **3 resolved** during spot-check: BUG-03 (filter gone), BUG-11 (Menu:* keys present), FEAT-06 (CI/CD shipped).
- **Drifts noted** during verification:
  - SEC-01: partially mitigated (placeholders on HEAD, originals in git history + dev `appsettings.Local.json`).
  - SEC-03: signature drift -- now `string? filter` not `ExternalUserLookupRequestDto`.
  - ARC-06: DTO field types drift -- `DateTime` not `DateOnly` as docs suggest.

All other facts (line numbers, hardcoded values, missing attributes, enum counts) match the current code.

---

## How this was produced

Verification: direct source reads + one `Explore` subagent for spot-checks.

Research: 17 parallel sub-agents across three waves (5 for Critical/High issues, 6 for Medium/Low issues, 6 for Q + P questions), one per cluster, each briefed with code-level facts and asked for official docs + community forums with URLs and confidence labels.

Cluster mapping:

| Wave | Cluster | Items |
|---|---|---|
| 1 | Security | SEC-01, SEC-02, SEC-03, SEC-05 |
| 1 | Concurrency | DAT-01, DAT-02, DAT-03, BUG-01 |
| 1 | Tenant UoW | DAT-04 |
| 1 | Status workflow | DAT-05, BUG-02, FEAT-01 |
| 1 | Roles / placeholders | FEAT-02, ARC-03 |
| 2 | Security + DB | SEC-04, DAT-06, DAT-07 |
| 2 | Backend validation | BUG-09, BUG-10, ARC-02, ARC-06, ARC-08 |
| 2 | Angular UX | BUG-05, BUG-06, BUG-07, ARC-05, ARC-07, BUG-12 |
| 2 | Dead/incomplete | ARC-01, FEAT-03, FEAT-04, BUG-04, ARC-04, Q-10 |
| 2 | Infrastructure | FEAT-05, FEAT-08 |
| 2 | Tests + orphan Qs | FEAT-07, Q-05, Q-07, Q-09 |
| 3 | Status & workflow Qs | Q-01, Q-02 |
| 3 | Business-rule Qs | Q-03, Q-04, Q-06, Q-12 |
| 3 | Infra & licensing Qs | Q-08, Q-11 |
| 3 | Legal/compliance Ps | P-01, P-03, P-10 |
| 3 | Operational Ps | P-02, P-04, P-05, P-09 |
| 3 | Product/handover Ps | P-06, P-07, P-08 |

Sources cited inline per file. Where research could not settle a question, the file lists it under **Open questions** (Q-files) or **Default assumption / Risk if wrong** (P-files) rather than guessing.
