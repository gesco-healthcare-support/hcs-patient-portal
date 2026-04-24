[Home](../INDEX.md) > [Product Intent](./) > README

# Patient Portal -- Product Intent Documentation

**Status:** Phase 1 methodology, draft pending gate approval 2026-04-22
**Runbook:** [.claude/prompts/INTENDED_BEHAVIOR_DOCS_PROMPT_v2.md](../../.claude/prompts/INTENDED_BEHAVIOR_DOCS_PROMPT_v2.md)
**Umbrella plan:** [docs/plans/2026-04-22-intended-behavior-docs-execution.md](../plans/2026-04-22-intended-behavior-docs-execution.md)

## Purpose of `docs/product/`

This directory captures INTENDED behaviour -- what the Patient Portal is supposed to do -- in contrast to `docs/features/**` and per-feature `CLAUDE.md` files, which capture what the code CURRENTLY does. Every claim carries a source tag. Code is never cited as authoritative for intent (v2 runbook Constraint 4).

## Reading order

1. [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md) -- business frame, personas, tenant definition, regulatory environment.
2. Per-feature intent docs (one per feature in the scope list below, delivered in MVP-criticality order).
3. Cross-cutting concern docs under `docs/product/cross-cutting/`.
4. `DISCREPANCIES.md` -- intent-vs-implementation gaps, cross-referenced to `docs/issues/`.
5. `OUTSTANDING-QUESTIONS.md` -- consolidated manager-facing agenda.

## Scope

### In scope

**Feature intent docs** (14 files; delivered in umbrella plan tasks T2-T8):

- Appointments
- DoctorAvailabilities
- Doctors
- Patients
- ApplicantAttorneys / AppointmentAccessors / AppointmentApplicantAttorneys (attorney cluster)
- AppointmentEmployerDetails
- Locations / States / WcabOffices / AppointmentTypes / AppointmentLanguages / AppointmentStatuses (lookup cluster -- one compressed interview, six files)

**Cross-cutting concern docs** (5 files under `docs/product/cross-cutting/`; umbrella plan tasks T9-T13):

- Multi-tenancy model (T9)
- Authentication and role-based access (T10)
- Appointment lifecycle (T11)
- Email and notification touchpoints (T12)
- Tenant provisioning (T13)

### Out of scope

- **Books** (ABP scaffold, no product purpose).
- **Gesco-wide billing / revenue mechanics** -- the full Gesco commercial chain (editors, insurance approvals, billing, payer identity) does not shape Patient Portal behaviour; permanently scoped out of `docs/product/` per the 2026-04-22 scope correction. See 00-BUSINESS-CONTEXT.md.
- Any content already canonised under `docs/features/**`, `docs/issues/**`, `docs/business-domain/**`, or a feature `CLAUDE.md` is NOT rewritten here; it is referenced or lifted with a source tag when relevant.

## Feature order for Phase 2

MVP-criticality order per umbrella plan, not the order in the root CLAUDE.md feature index.

1. Appointments (T2) -- 13-state lifecycle is the product's centre of gravity.
2. DoctorAvailabilities (T3).
3. Doctors (T4).
4. Patients (T5) -- Q12 default-password intent is MVP-blocking.
5. Attorney cluster: ApplicantAttorneys + AppointmentAccessors + AppointmentApplicantAttorneys (T6).
6. AppointmentEmployerDetails (T7).
7. Lookup cluster (T8) -- compressed-mode single session, six output files.
8. Cross-cutting concerns (T9 through T13), one session each, after all feature docs.

## Per-feature document template

Every file at `docs/product/<feature-kebab>.md` uses the template below (v2 runbook, verbatim, ratified 2026-04-22). The template is a floor, not a ceiling -- add sections if a feature needs them, but do not remove sections.

```markdown
# <Feature Name> -- Intended Behavior

**Status:** draft | developer-reviewed | manager-confirmed
**Last updated:** YYYY-MM-DD
**Primary stakeholder:** <role or name, or [UNKNOWN -- queued for manager]>

## Purpose

<Why this feature exists. Source-tagged.>

## Personas and goals

<Per-persona goals. Reference 00-BUSINESS-CONTEXT.md for persona definitions; expand here only with feature-specific goals.>

## Intended workflow

<Step-by-step happy path. Present tense. Source-tagged per step. Call out role-based branches.>

## Business rules and invariants

<Validation rules, constraints, uniqueness scopes, allowed value ranges. One bullet per rule. Source-tagged.>

## Integration points

<Dependencies on other features. Data flowing in / out. Participation in the 4-product pipeline if any.>

## Edge cases and error behaviors

<What should happen when X fails, Y is missing, Z conflicts, the user double-submits, etc.>

## Success criteria

<Testable conditions that indicate this feature works as intended.>

## Known discrepancies with implementation

<Filled in Phase 3. Before Phase 3: "Pending Phase 3 cross-reference pass".>

## Outstanding questions

<Any [UNKNOWN] items specific to this feature, each phrased as a concrete manager-facing question.>
```

## Seed-question classification

Each P1-P10 (from [docs/issues/QUESTIONS-FOR-PREVIOUS-DEVELOPER.md](../issues/QUESTIONS-FOR-PREVIOUS-DEVELOPER.md)) and Q1-Q12 (from [docs/issues/TECHNICAL-OPEN-QUESTIONS.md](../issues/TECHNICAL-OPEN-QUESTIONS.md)) is classified by **who answers** and **where it surfaces**, per the umbrella plan binding table.

| Seed | Topic | Classification | Bound to session |
|------|-------|---------------|------------------|
| P1 | Contract / SOW | Manager-only | Phase 0 touch-down (scope-adjusted out per revenue correction); Phase 4 candidate |
| P2 | Real patient data in deployed environment | **Adrian-answered 2026-04-22: no real PHI ever in deployed env** | Phase 0 Regulatory |
| P3 | HIPAA compliance decisions | Manager + legal | Phase 0 Regulatory |
| P4 | End-client identity | Manager-only | Scope-adjusted out per revenue correction; Phase 4 candidate |
| P5 | External service accounts | Partial Adrian + manager | Phase 0 / Pipeline section |
| P6 | Verbal commitments to client | Manager | Spot-check per feature in Phase 2 |
| P7 | Handover circumstances | Adrian may know; manager confirms | Phase 0 Business Context (low priority) |
| P8 | User research / personas | Manager + client | Phase 0 Personas |
| P9 | DWC / carrier integrations | Manager | Phase 0 Regulatory + T2 Appointments |
| P10 | Undisclosed security vulnerabilities | OUT OF SCOPE for intent docs | Tracked in `docs/issues/` |
| Q1 | Appointment status workflow rules | Adrian best-guess + manager confirm | T2 Appointments + T11 Appointment Lifecycle |
| Q2 | Enum vs lookup table | Adrian direct | T8 Lookup cluster (AppointmentStatuses file) |
| Q3 | Confirmation number scope | Adrian direct | T2 Appointments + T9 Multi-tenancy |
| Q4 | Claim Examiner role | Manager | T6 Attorney cluster + T10 Auth-and-Roles |
| Q5 | Orphaned Appointment fields | Adrian + manager | T2 Appointments |
| Q6 | Advance booking window | Manager + industry-standard fallback | T2 Appointments |
| Q7 | `DoctorConsts.EmailMaxLength = 49` | Adrian direct | T4 Doctors |
| Q8 | Deployment target | OUT OF SCOPE -- infra | Skip in Phase 2-4 |
| Q9 | Social OAuth providers | Manager | T10 Auth-and-Roles |
| Q10 | File Management module usage | OUT OF SCOPE -- dependency cleanup | Skip in Phase 2-4 |
| Q11 | ABP Commercial license ownership | OUT OF SCOPE -- operations | Skip in Phase 2-4 |
| Q12 | Default password for auto-created patients | Adrian + manager | T5 Patients + T10 Auth-and-Roles |

## Classification of existing `docs/business-domain/*.md` files

These files pre-date `docs/product/` and contain a mix of intent and observation. Each file's usability for lifting into intent docs is classified 2026-04-22.

| File | Classification | Phase 2 handling |
|------|---------------|-----------------|
| [DOMAIN-OVERVIEW.md](../business-domain/DOMAIN-OVERVIEW.md) | **MIXED** | Per-section re-interview before lifting. Intent-bearing candidates: plain-language narrative, the "Why this design?" multi-tenancy rationale, the end-to-end worked example. Observation-only sections: enum tables, entity property lists, `IMultiTenant` references. Phase 2 reads this file per feature and lifts only the confirmed intent-bearing paragraphs. [Source: Adrian-confirmed 2026-04-22] |
| [APPOINTMENT-LIFECYCLE.md](../business-domain/APPOINTMENT-LIFECYCLE.md) | **INTENT-BEARING (whole)** | T11 Appointment Lifecycle cross-cutting doc lifts the file wholesale as `[Source: doc: business-domain/APPOINTMENT-LIFECYCLE.md -- confirmed 2026-04-22]`. This resolves Q1 (status workflow rules) by treating the 13-state machine diagram, happy path, cancellation/reschedule paths, terminal states, and billing-implications table as confirmed intent. Phase 3 surfaces every code-vs-intent gap (i.e., every diagram transition the code does not implement) as a discrepancy entry. [Source: Adrian-confirmed 2026-04-22] |
| [DOCTOR-AVAILABILITY.md](../business-domain/DOCTOR-AVAILABILITY.md) | **OBSERVATION-ONLY (whole)** | T3 DoctorAvailabilities feature doc does NOT lift from this file. Intent is built from Q&A with Adrian. The file stays in `docs/business-domain/` as a code-reverse-engineering reference. [Source: Adrian-confirmed 2026-04-22] |
| [USER-ROLES-AND-ACTORS.md](../business-domain/USER-ROLES-AND-ACTORS.md) | **OBSERVATION-ONLY (whole)** | T10 Auth-and-Roles cross-cutting doc does NOT lift from this file, despite the per-role capability matrix looking intent-like. Q4 confirms the matrix is unreliable (Claim Examiner has zero code-backed capability). Intent is built from Q&A with Adrian. [Source: Adrian-confirmed 2026-04-22] |

## Source tags in use

Every claim in every `docs/product/*.md` file carries one of these tags (v2 runbook vocabulary):

- `[Source: Adrian-confirmed YYYY-MM-DD]` -- Adrian stated directly with confidence.
- `[Source: Adrian best-guess YYYY-MM-DD -- NEEDS CONFIRMATION]` -- Adrian inferred and flagged uncertainty.
- `[Source: manager-confirmed YYYY-MM-DD -- via Adrian]` -- Manager answered and Adrian relayed.
- `[Source: doc: <path> -- confirmed YYYY-MM-DD]` -- Lifted from an existing intent-flavoured doc section.
- `[Source: industry-standard -- NEEDS CONFIRMATION]` -- CA workers'-comp IME default fill-in.
- `[UNKNOWN -- queued for manager: <role>]` -- Adrian does not know; flagged for a specific manager.
- `[observed, not authoritative]` -- Code-derived observation. ONLY appears in a "Known Discrepancies" section. NEVER a primary source for intent.

## Phase gates

Phase gates from the v2 runbook, tracked here for session resume:

- **Phase 0** -- ratified 2026-04-22 ([00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md), commit `d50a3d0`).
- **Phase 1** -- this README, draft pending gate approval 2026-04-22.
- **Phase 2** -- per-feature intent docs, MVP-critical order per umbrella plan tasks T2-T8 then cross-cutting T9-T13.
- **Phase 3** -- discrepancies pass, two sessions (umbrella plan T14-T15).
- **Phase 4** -- outstanding-questions roll-up (umbrella plan T16).

Each phase has a hard stop gate with explicit Adrian approval before the next phase begins.
