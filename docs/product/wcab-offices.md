[Home](../INDEX.md) > [Product Intent](./) > WCAB Offices

# WCAB Offices -- Intended Behavior

**Status:** draft -- Phase 2 T8, lookup cluster. MVP role queued for manager (Q26 in OUTSTANDING-QUESTIONS.md).
**Last updated:** 2026-04-24
**Primary stakeholder:** [UNKNOWN -- queued for manager]

> Captures INTENDED behaviour for the `WcabOffice` lookup entity -- the catalogue of California Workers' Compensation Appeals Board (WCAB) district-office locations. Adrian has deferred the MVP intent for this entity to the manager (he did not know the business role of WCAB offices in the portal's flows). Every claim source-tagged.

## Purpose

A WCAB office is a California state district office where workers'-compensation cases are filed and hearings are conducted. A `WcabOffice` record names one such office (Name, Abbreviation, address) and marks it active or inactive. The entity exists in code with full CRUD, Angular UI, and Excel export, but **has zero inbound FKs** -- no booking, appointment, notification, or other flow reads it. [Source: verified via code review 2026-04-24]

**MVP role: queued for manager (Q26).** Adrian did not know what WCAB offices are used for in this portal; he asked 2026-04-24 to route the question to the manager. Until the manager answers, the existing code is treated as zombie -- the entity is retained in the schema but no intent is written for how it should be used. [Source: Adrian-confirmed 2026-04-24 via Q-A answer ("I don't know what WCAB offices are ... lets leave that as a question to ask to manager")]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` for persona definitions.

- **Host admin (Gesco).** Manages the WCAB office catalogue today if anyone does; authority is inferred by default since the code ships a host-admin CRUD UI. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION via Q26]
- **Doctor's office, case parties (attorneys, insurance, adjustor), bookers.** Would read the catalogue for case context if it were surfaced; no such surface exists currently. [Source: inferred from zero-consumer code state]

## Intended workflow

[UNKNOWN -- depends on Q26's answer.] Candidate workflows the manager may confirm:

- Data directory only. Host admin enters the DWC-published WCAB office list; staff reference via the admin UI or Excel export; no other flow consumes it at MVP.
- Planned future linkage. The entity is seeded in advance for a post-MVP feature that will add an FK from Appointment or a case record.
- Downstream consumption. Case Tracking or MRR AI reads the list; the portal maintains it as source-of-truth.
- Not needed at MVP. Deprioritise the admin UI; revisit when there is a concrete consumer.

## Business rules and invariants

- **Scoping: host-scoped pending clarification.** The current code treats WcabOffices as host-scoped (no `IMultiTenant`; configured under `IsHostDatabase()`). Adrian did not redirect this in the Q-A answer (he explicitly said the other five entities but excluded WCAB as a don't-know). The catalogue remains host-scoped at intent until the manager clarifies, with the option to revise once Q26 returns. [Source: code observation + Adrian's don't-know on Q-A]
- **Abbreviation shape.** Each office has a short abbreviation (e.g., `LAO`, `SFO`, `SAC`). Uniqueness is expected but not enforced in code. [Source: Adrian best-guess -- NEEDS CONFIRMATION via Q26]
- **Active / inactive flag.** `IsActive` allows hiding an office without deletion. [Source: inferred from code]
- **Authentication on list surfaces.** The `GetListAsExcelFileAsync` method is `[AllowAnonymous]` in the AppService. Whether this is intentional (public WCAB directory data) or a code-level gap is [UNKNOWN -- queued for Adrian on review; likely a gap to close rather than intentional design since the same endpoint at every other entity requires auth].

## Integration points

- **No inbound FK.** No entity references `WcabOfficeId` in the current code. [Source: verified via Grep 2026-04-24]
- **Outbound FK.** `WcabOffice.StateId` (SetNull).
- **Excel export.** `WcabOfficesAppService.GetListAsExcelFileAsync` -- currently `[AllowAnonymous]`.
- **Downstream-product consumption.** Not established at MVP. Potentially a post-MVP linkage.

## Edge cases and error behaviors

Most edge cases are moot until Q26 pins down the entity's role. For now:

- **Deletion of an active office.** No inbound FK, so deletion is unconstrained at DB level. In practice, deactivate (`IsActive = false`) rather than delete. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Duplicate abbreviation.** No uniqueness constraint; duplicates permitted.
- **Anonymous Excel export.** No PHI risk (WCAB offices are public DWC data), but the auth bypass is inconsistent with every other entity's export endpoint.

## Success criteria

Success criteria cannot be fully written until Q26 is answered. Baseline at MVP: the entity continues to exist in the schema without breaking any other flow, and the `[AllowAnonymous]` gap is surfaced for closure in a follow-up build item.

## Known discrepancies with implementation

- `[observed, not authoritative]` Zero inbound FKs -- `WcabOffice` has no consumer in the Patient Portal and functions as a standalone reference table with admin CRUD only.
- `[observed, not authoritative]` `GetListAsExcelFileAsync` is marked `[AllowAnonymous]`. The WCAB office list is exportable without authentication (no PHI risk, but the pattern is inconsistent with every other entity's exports).
- `[observed, not authoritative]` Full CRUD AppService + per-action permissions + Angular UI exist; the feature was built to a complete CRUD surface but is disconnected from the rest of the product.
- `[observed, not authoritative]` No uniqueness constraint on `Abbreviation`; duplicates permitted.
- `[observed, not authoritative]` No `DataSeedContributor` populates the catalogue at install.

## Outstanding questions

Full MVP role for WCAB offices is Q26 in `OUTSTANDING-QUESTIONS.md`. Re-read this file after Q26 returns.
