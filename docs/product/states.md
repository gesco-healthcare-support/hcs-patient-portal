[Home](../INDEX.md) > [Product Intent](./) > States

# States -- Intended Behavior

**Status:** draft -- Phase 2 T8, lookup cluster
**Last updated:** 2026-04-24
**Primary stakeholder:** Host admin (global list) + tenant admin (per-tenant hide/show) [Source: Adrian-confirmed 2026-04-24]

> Captures INTENDED behaviour for the `State` lookup entity -- the US-state reference list used in address fields across the portal. Every claim is source-tagged; code is never cited as intent authority.

## Purpose

`State` is a host-managed catalogue of US states used in address fields across the portal (patient, attorney, employer, location, WCAB-office addresses). Gesco's host admin maintains one global list of 50 states; each tenant can hide states it does not use from its own users' address dropdowns, but cannot add its own state entries. [Source: Adrian-confirmed 2026-04-24, Q-A common-with-customisation + Q-A2 hide/show]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` for persona definitions.

- **Host admin (Gesco).** Owns the global state list. Seeds the 50 states at install; edits only to correct typos or add a missing territory. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; consistent with Q-A2 implying tenants cannot ADD entries]
- **Tenant admin (doctor's admin).** Can hide states their practice does not serve from address dropdowns for their own tenant's users. Cannot add or edit the global list. [Source: Adrian-confirmed 2026-04-24, Q-A2 hide/show]
- **Bookers.** Read the (tenant-filtered) state list on the booking form's address sections. [Source: inferred from code -- all address-carrying entities FK to `State`]

## Intended workflow

### At install

Host admin seeds the 50 US states through the admin UI. No automatic data seeder populates `State`. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; code observation: no `*DataSeedContributor*.cs` for lookup entities]

### Per-tenant hide/show

Tenant admin marks states as hidden for their tenant via a per-tenant visibility flag. Hidden states do not appear in address dropdowns for users in that tenant; global rows remain unchanged. [Source: Adrian-confirmed 2026-04-24, Q-A2]

### On address form use

Booker fills an address; the state field renders as a dropdown of states visible for the booker's tenant. [Source: inferred from code + Q-A2]

## Business rules and invariants

- **Common global base.** All tenants share one master state list. Only Gesco's host admin edits it. [Source: Adrian-confirmed 2026-04-24]
- **Per-tenant hide/show customisation.** Each tenant can hide entries from its own users. Cannot add new entries. [Source: Adrian-confirmed 2026-04-24, Q-A2]
- **Presence on address fields.** State remains a nullable FK on every referring entity; optional at MVP. [Source: verified via code review 2026-04-24]
- **Name uniqueness.** Each state Name is intended unique in the global list. Current code does not enforce. [Source: Adrian best-guess -- NEEDS CONFIRMATION]

## Integration points

`State` is FK-referenced (all SetNull on delete) by: `Location.StateId`, `WcabOffice.StateId`, `Patient.StateId`, `ApplicantAttorney.StateId`, `AppointmentEmployerDetail.StateId`. [Source: verified via code review 2026-04-24]

Per-tenant hide/show adds a visibility layer on top of the global table -- not present in current code; a new table or per-tenant filter mechanism is needed.

## Edge cases and error behaviors

- **Deleting a global State in use.** Current FK behavior is SetNull -- deletion silently nulls the state column on every referring row. Intent: [UNKNOWN -- queued for Adrian: block deletion when any row references it, or accept the silent SetNull since States rarely change?]
- **Tenant hides a State that existing records already use.** Existing records retain their state value; only NEW dropdowns on that tenant's UIs exclude the hidden state. [Source: Adrian best-guess -- NEEDS CONFIRMATION; consistent with typical hide/show semantics]
- **Duplicate Name on create.** No uniqueness constraint; duplicates currently permitted by the AppService.

## Success criteria

- The 50 US states list is available on every tenant's address dropdowns by default.
- Host admin can add or correct a global State entry; change appears across all tenants immediately.
- Tenant admin can hide a state from their own tenant's dropdowns without affecting other tenants.
- Existing records referencing a tenant-hidden state are not broken.

## Known discrepancies with implementation

- `[observed, not authoritative]` No per-tenant visibility flag exists -- per-tenant hide/show is intent-only; a new `TenantStateVisibility` table (or equivalent tenant-filter mechanism) has to be added.
- `[observed, not authoritative]` `State.cs` has only a `Name` field; no USPS abbreviation or ISO code.
- `[observed, not authoritative]` SetNull on every referring entity; a global State deletion silently strips the state from every row.
- `[observed, not authoritative]` No uniqueness index on `State.Name`.
- `[observed, not authoritative]` No `DataSeedContributor` for `State`; the 50 states require manual entry at install.
- `[observed, not authoritative]` The OUTSTANDING-QUESTIONS.md glossary entry ("shared reference data ... the same across all practices") predates the Q-A + Q-A2 customisation answers and should be read as "common base with per-tenant hide/show" for the non-Locations entities (States / AppointmentTypes / AppointmentLanguages / AppointmentStatuses). Glossary text is not updated in T8 (deferred to a later turn).

## Outstanding questions

- Deletion-of-in-use State behavior ([UNKNOWN -- queued for Adrian]): block or silent SetNull.
