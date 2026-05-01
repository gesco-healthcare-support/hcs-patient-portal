[Home](../INDEX.md) > [Product Intent](./) > Business Context

# Patient Portal -- Business Context (Intended)

**Status:** draft -- Phase 0 of intended-behavior runbook, interview in progress
**Last updated:** 2026-04-22
**Runbook:** [.claude/prompts/INTENDED_BEHAVIOR_DOCS_PROMPT_v2.md](../../.claude/prompts/INTENDED_BEHAVIOR_DOCS_PROMPT_v2.md)
**Umbrella plan:** [docs/plans/2026-04-22-intended-behavior-docs-execution.md](../plans/2026-04-22-intended-behavior-docs-execution.md)

> This document captures the business frame inside which every per-feature intent doc under `docs/product/` will be written. It does NOT describe what the Patient Portal code currently does -- that is the job of `docs/features/**` and feature `CLAUDE.md` files. Every claim is source-tagged per the runbook's Source Tags section. Code is never cited as authoritative for intent.

## Gesco and the Patient Portal

Gesco performs Independent Medical Examinations (IMEs) for California workers' compensation cases. [Source: doc: root CLAUDE.md -- confirmed 2026-04-22]

The Patient Portal is the booking surface for those IMEs: clients book their appointments with a medical examiner through the portal. [Source: Adrian-confirmed 2026-04-22]

This document deliberately does not describe Gesco's commercial relationships, billing mechanics, or revenue flow. Those exist -- an IME generates work that flows through Gesco's editors, the insurance companies that approve the resulting paperwork, and Gesco's billing process -- but they do not shape Patient Portal behaviour and are therefore out of scope for this intent-documentation set. [Source: Adrian-confirmed 2026-04-22, scope correction]

## The 4-product pipeline

The Patient Portal is the second of four products in Gesco's case-evaluation pipeline:

`Digital Forms -> Patient Portal -> Case Tracking -> MRR AI` [Source: doc: root CLAUDE.md -- confirmed 2026-04-22 for pipeline ordering]

- **Digital Forms** (upstream) -- role and what it hands to the Patient Portal (if anything): [UNKNOWN -- queued for manager: Adrian's manager / pipeline architect; Adrian does not personally hold this answer, confirmed 2026-04-22]
- **Patient Portal** (this product) -- where IME appointments are booked. [Source: Adrian-confirmed 2026-04-22]
- **Case Tracking** (downstream) -- what the Patient Portal hands to Case Tracking after an appointment reaches a specific state: [UNKNOWN -- queued for manager: Adrian's manager / pipeline architect; Adrian does not personally hold this answer, confirmed 2026-04-22]
- **MRR AI** (downstream) -- relationship to Patient Portal (direct hand-off, or further downstream of Case Tracking): [UNKNOWN -- queued for manager: Adrian's manager / pipeline architect; Adrian does not personally hold this answer, confirmed 2026-04-22]

## User personas

The Patient Portal supports four booker populations as equal first-class users at MVP. [Source: Adrian-confirmed 2026-04-22] No single persona is primary; each has its own navigation, permission model, and UX flow from MVP day one.

A "booker" is a user who logs into the portal, picks a medical examiner's open slot, and creates a booking. Non-booker personas (examiner-side staff, administrative roles) are also supported and are captured below.

### Applicant attorney

Legal representative for the injured worker in a workers'-compensation claim. Books IMEs on behalf of their client. [Source: Adrian-confirmed 2026-04-22]

- Goals in the portal: [UNKNOWN -- queued for Adrian]
- Authority in the portal: [UNKNOWN -- queued for Adrian]

### Patient (injured worker)

The worker whose medical status is being examined. Self-service booking is a supported flow. [Source: Adrian-confirmed 2026-04-22]

- Goals in the portal: [UNKNOWN -- queued for Adrian]
- Authority in the portal: [UNKNOWN -- queued for Adrian]

### Claim examiner / adjuster

Insurance-side user at an insurance carrier or third-party administrator (TPA). Books IMEs as part of managing a claim file. [Source: Adrian-confirmed 2026-04-22]

- Goals in the portal: [UNKNOWN -- queued for Adrian]
- Authority in the portal: [UNKNOWN -- queued for Adrian]

### Defense attorney

Legal representative for the employer or carrier in an adversarial workers'-compensation matter -- the opposing side to the applicant attorney in the same case. [Source: Adrian-confirmed 2026-04-22]

**Relationship to applicant attorney in the portal:** The defense attorney behaves essentially the same as an applicant attorney -- same booker capabilities, same portal-feature footprint. Both appear as distinct user-type options in the signup flow alongside Patient and Claim Examiner; the signup exposes exactly four user-type options. [Source: Adrian-confirmed 2026-04-22; vocabulary grounded against code enum `ExternalUserType` with values Patient=1, ClaimExaminer=2, ApplicantAttorney=3, DefenseAttorney=4, verified 2026-04-22 to confirm we are describing the same four-option structure]

- Goals in the portal: same as applicant attorney at Phase 0 framing; Phase 2 feature docs capture any divergence.
- Authority in the portal: same as applicant attorney at Phase 0 framing; the T10 Auth-and-Roles cross-cutting doc confirms whether any permission differs.
- **Phase 3 verification task (not a coverage gap):** Applicant attorney has a dedicated `ApplicantAttorneys` domain entity (per the root CLAUDE.md feature index); at signup, defense attorney creates an IdentityUser + "Defense Attorney" role but no separate domain entity. Phase 3 will verify whether that asymmetry matches the intent of "behaves essentially the same" -- for example, whether defense attorneys need (or do not need) firm/contact data captured via an `ApplicantAttorneys`-like entity, and whether the Appointments cluster (`AppointmentAccessors`, `AppointmentApplicantAttorneys`) already accommodates both attorney types via a single role-driven path.

### Examiner office staff

The medical examiner's own front-desk or scheduling staff, who manage the examiner's calendar from inside the portal. They operate within their examiner's tenant with broad authority inside it and no authority across other tenants. [Source: Adrian-confirmed 2026-04-22]

- Goals in the portal: [UNKNOWN -- deferred to Phase 2 feature docs (DoctorAvailabilities, Appointments) and the T10 Auth-and-Roles cross-cutting doc]
- Authority in the portal: [UNKNOWN -- deferred to T10 Auth-and-Roles]

### Host admin (Gesco-side)

A Gesco-internal administrator operating at the host level, above all tenants. Responsible for provisioning new medical examiners as new tenants, managing host-scoped master data (locations, states, WCAB offices, appointment types, languages, statuses), and cross-tenant administrative concerns. The only user type that legitimately operates across tenants. [Source: Adrian-confirmed 2026-04-22]

- Goals in the portal: [UNKNOWN -- deferred to Phase 2 cross-cutting docs (T9 multi-tenancy, T13 tenant provisioning, T10 auth-and-roles)]
- Authority in the portal: [UNKNOWN -- deferred to T10 Auth-and-Roles]

### Roles that do NOT interact with the Patient Portal

- **Editors** -- the internal Gesco staff who review paperwork before insurance-company approval operate entirely in downstream products (Case Tracking and/or MRR AI) and do not log into the Patient Portal. They are not a portal persona. [Source: Adrian-confirmed 2026-04-22]

## Customer segments -- what a tenant represents

A **tenant** in the Patient Portal represents **one medical examiner and their office**. [Source: Adrian-confirmed 2026-04-22]

A tenant does NOT map to an insurance carrier, a TPA, a law firm, a self-insured employer, or any other payer in the workers'-compensation chain. Each medical examiner's practice is provisioned as its own tenant. Transactional data (appointments, doctor availabilities, attorney links, employer details) scopes to the tenant; master lookup data (locations, states, appointment types, statuses, languages, WCAB offices) is host-scoped and shared across all tenants. [Source: doc: root CLAUDE.md -- confirmed 2026-04-22 for the host-vs-tenant entity split; Source: Adrian-confirmed 2026-04-22 for the tenant-unit definition]

The four booker populations (applicant attorneys, patients, claim examiners, defense attorneys) are USERS of the portal, not tenants. A user interacts with one or more tenants (medical examiners) during a booking, but the user does not themselves form a tenant unit.

## Regulatory environment

### HIPAA

The Patient Portal's formal HIPAA classification (covered entity vs business associate vs not-yet-determined) is not known to the developer; this is a legal / compliance decision that sits above the engineering layer. [UNKNOWN -- queued for manager: compliance / legal counsel; Adrian does not personally hold this answer, confirmed 2026-04-22]

In the absence of a formal classification, PHI-handling intent for the portal is documented under the conservative assumption that HIPAA applies. The portal handles data that is almost certainly PHI -- injured-worker demographics, claim numbers, medical-examination scheduling, and (depending on downstream hand-offs) medical-examination outcomes. Intent for audit logging, access control, encryption-at-rest and in-transit, and data-retention features is written assuming HIPAA compliance is required until told otherwise. [Source: Adrian best-guess 2026-04-22 -- NEEDS CONFIRMATION]

**P2 (real patient data in a deployed environment):** To the developer's knowledge, no real patient data has ever been loaded into any deployed environment. All runs to date have been developer localhost with seeded synthetic data. No breach-notification posture is inherited from historical deployments. [Source: Adrian-confirmed 2026-04-22]

### California Division of Workers' Compensation (DWC)

The Patient Portal operates inside the California workers'-compensation regulatory ecosystem. The developer is generally aware that DWC rules exist but does not personally hold specific rule-by-rule knowledge of which regulations directly constrain portal behaviour. [Source: Adrian-confirmed 2026-04-22]

Specific DWC rules that shape portal features (candidate examples: QME panel assignment timing per 8 CCR 30-36, mandatory medical-legal forms, defense-vs-applicant IME distinctions, mandatory appointment-notice timelines) are [UNKNOWN -- queued for manager: Adrian's manager or a DWC-domain expert]. Phase 2 feature docs should treat DWC-specific rules as open until confirmed, rather than inferring them from code or industry defaults (v2 runbook Constraint 3 -- do not invent business rules).

### Other compliance surfaces

[UNKNOWN -- not yet surfaced. California CMIA (Confidentiality of Medical Information Act), SB 446, and CCPA/CPRA could each apply depending on Gesco's formal HIPAA posture and client relationships. Queued for manager: compliance / legal counsel.]

## Glossary

Terms that recur across the feature docs. External industry terms are flagged NEEDS CONFIRMATION so Adrian or a DWC-domain expert can correct any misdefinition at the gate.

- **IME** -- Independent Medical Examination. A medical evaluation performed by a neutral physician in a workers'-comp or disability context. [Source: industry-standard -- NEEDS CONFIRMATION]
- **QME** -- Qualified Medical Evaluator or Evaluation. A physician certified by the California DWC Medical Unit to perform medical-legal evaluations, or the evaluation performed by such a physician. [Source: industry-standard -- NEEDS CONFIRMATION]
- **AME** -- Agreed Medical Evaluator or Evaluation. A physician agreed upon by both applicant and defense attorneys in a contested case, or the evaluation so performed. [Source: industry-standard -- NEEDS CONFIRMATION]
- **WCAB** -- Workers' Compensation Appeals Board. The California administrative body that adjudicates workers'-compensation disputes. [Source: industry-standard -- NEEDS CONFIRMATION]
- **DWC** -- California Division of Workers' Compensation. The state agency administering California workers'-compensation law. [Source: industry-standard -- NEEDS CONFIRMATION]
- **Claim examiner / adjuster** -- insurance-side claim-file manager at an insurance carrier or TPA. [Source: industry-standard -- NEEDS CONFIRMATION]
- **Applicant attorney** -- legal representative for the injured worker in a workers'-compensation claim. [Source: industry-standard -- NEEDS CONFIRMATION]
- **Defense attorney** -- legal representative for the employer or carrier in an adversarial WC matter; the opposing side to the applicant attorney in the same case. [Source: Adrian-confirmed 2026-04-22]
- **TPA** -- Third-Party Administrator. A firm that administers workers'-compensation claims on behalf of carriers or self-insured employers. [Source: industry-standard -- NEEDS CONFIRMATION]
- **Tenant** (Patient Portal) -- one medical examiner and their office. Transactional data scopes to the tenant. [Source: Adrian-confirmed 2026-04-22]
- **Host** (Patient Portal) -- the cross-tenant scope where master lookup data (locations, states, WCAB offices, appointment types, languages, statuses) lives. [Source: doc: root CLAUDE.md -- confirmed 2026-04-22]
- **Booker** (Patient Portal) -- a user who creates a new appointment. At MVP the portal supports four booker populations: applicant attorneys, patients / injured workers, claim examiners / adjusters, and defense attorneys. [Source: Adrian-confirmed 2026-04-22]
- **Non-booker personas** (Patient Portal) -- examiner office staff (tenant-scoped) and host admin (Gesco-side, cross-tenant). [Source: Adrian-confirmed 2026-04-22]
