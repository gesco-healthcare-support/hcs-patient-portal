---
feature: address-validation
date: 2026-05-29
status: in-progress
base-branch: main
related-issues: []
sequence: 6 of 6 (largest; booking-form cluster; last)
branch: feat/address-validation
vendor: Smarty (US Address Autocomplete Pro + US Street Address verification) -- Adrian decision 2026-05-31
---

## Goal

Add address autocomplete-as-you-type plus a pre-submit standardization prompt
("use suggested vs keep mine") to every address group in the booking form,
behind a provider-agnostic interface so the concrete vendor is a swap, not a
rewrite. Outcome: fewer typos and USPS-correct formatting captured before the
booking is submitted.

## Context

### Verified current state (live UI + code, 2026-05-31)

Investigated the running booking form at `/appointments/add` (Playwright, logged
in as a Patient). **No console errors or warnings are thrown** -- the form loads
clean; address fields are plain text with zero validation, autocomplete, or
logging today. So "why are logs thrown" has a concrete answer here: nothing is
thrown; this is a pure additive enhancement, not a bug fix.

**Six address groups confirmed in the UI** (all use a native `<select>` for
State, populated with the 50 states from the `State` lookup; all text fields are
plain `<input>`/`maxLength` only):

| # | Group | Where it renders | Address controls | Suite? | Required? | Persistence path |
|---|---|---|---|---|---|---|
| 1 | Patient | Patient Demographics (inline) | `street`, `address` (labeled "Unit #"), `city`, `stateId`, `zipCode` | "Unit #" (`address`) | optional | `updatePatientProfile()` -> `PatientUpdateDto` (`appointment-add.component.ts` ~1444-1453) |
| 2 | Employer | Employer Details (inline) | `employerStreet`, `employerCity`, `employerStateId`, `employerZipCode` | none | optional | `createEmployerDetailsIfProvided()` -> POST `appointment-employer-details` (~1699-1709) |
| 3 | Applicant Attorney | Applicant Attorney Details (inline, "Include" toggle, default on) | `applicantAttorneyStreet`, `applicantAttorneyCity`, `applicantAttorneyStateId`, `applicantAttorneyZipCode` | none | required when included | `upsertApplicantAttorneyForAppointmentIfProvided()` -- SEE GOTCHA 1 |
| 4 | Defense Attorney | Defense Attorney Details (inline, "Include" toggle) | `defenseAttorneyStreet`, `defenseAttorneyCity`, `defenseAttorneyStateId`, `defenseAttorneyZipCode` | none | required when included | `upsertDefenseAttorneyForAppointmentIfProvided()` -- SEE GOTCHA 1 |
| 5 | Insurance | Claim Information modal ("Add +", repeatable per injury) | `injuryInsuranceStreet`, `injuryInsuranceSte`, `injuryInsuranceCity`, `injuryInsuranceStateId`, `injuryInsuranceZip` | STE | optional | `persistInjuryDraftsIfProvided()` -> POST `appointment-primary-insurances` |
| 6 | Claim Examiner | Claim Information modal (same modal, repeatable) | `injuryClaimExaminerStreet`, `injuryClaimExaminerSte`, `injuryClaimExaminerCity`, `injuryClaimExaminerStateId`, `injuryClaimExaminerZip` | STE | required (in modal) | `persistInjuryDraftsIfProvided()` -> POST `appointment-claim-examiners` |

Backend DTOs already carry every address field (`Street`/`City`/`StateId` +
`ZipCode` or `Zip`, plus `Suite` on insurance/CE, plus patient `Address`) --
**no schema change needed** (confirmed across Patient, AppointmentEmployerDetail,
ApplicantAttorney, DefenseAttorney, AppointmentPrimaryInsurance,
AppointmentClaimExaminer domain + DTO classes).

### OLD-app parity

OLD (`P:\PatientPortalOld`) had the SAME address fields per entity and **no
address validation or autocomplete anywhere** -- plain `<textarea>`/`<input>`,
a State `<select>`, and a 5-digit zip mask (`rx-mask mask="99999"`). No USPS /
Smarty / Google / geocoding references in OLD source or docs. So this feature is
a NEW enhancement beyond OLD parity, not a port; nothing in OLD constrains it.

### Gotchas discovered (must be handled in build)

1. **Attorney (AA/DA) address persistence is unconfirmed.** The booking form
   collects AA/DA Street/City/State/Zip (required when the section is included),
   but the booking upsert (`upsertApplicant/DefenseAttorneyForAppointmentIfProvided`)
   may not send the address -- the attorney address lives on the master
   `ApplicantAttorney`/`DefenseAttorney` record. T2 must VERIFY where the chosen/
   standardized address is persisted; if it is dropped today, autocomplete still
   aids entry but the standardized value would not be stored -- flag to Adrian.
2. **Insurance + CE addresses live inside a dynamically-added modal**
   (Claim Information "Add +"), and the modal is **repeatable per injury**. The
   autocomplete attachment must bind to inputs created at runtime (each injury
   draft), not just static fields.
3. **State is a native `<select>` keyed by `StateId` (GUID), not a text field.**
   A vendor returns a state name or USPS 2-letter code; filling the form requires
   resolving that to the matching `StateId`. T1 must include a
   name/abbreviation -> `StateId` resolver built from the existing State lookup
   (`getStateLookup`, which returns `LookupDto<Guid>` with the display name).
4. **Field-name inconsistency across groups** (`zipCode` vs `zip`, suite present
   only on patient/insurance/CE, patient's secondary line labeled "Unit #" =
   the `address` control). The fill adapter needs a per-group field map.
5. **No zip mask in NEW** (OLD had `99999`). Standardization should ideally
   return ZIP+4; the form's plain zip input can hold `#####-####`.

## Approach

- **Provider-agnostic core (vendor-neutral):** an `AddressValidationProvider`
  interface with `autocomplete(query, sessionToken?)` and
  `validate(address)` (returns a standardized candidate + a "matches input"
  verdict). Ship a deterministic in-memory mock for development and tests so all
  UI work lands before the vendor is chosen. The interface carries an optional
  opaque `sessionToken` so a Google adapter can do session-billing and a Smarty
  adapter can ignore it -- no interface change when the vendor is picked.
- **State resolver:** a small pure helper that maps a returned state name or
  USPS code to the `StateId` GUID using the loaded State lookup; unit-tested.
- **Autocomplete:** an attachable directive bound to each group's street input
  (works for runtime-created modal inputs too). Selecting a suggestion fills
  that group's street/city/state(+resolve to StateId)/zip(+suite) via the
  per-group field map.
- **Pre-submit standardization:** on submit, validate each ENABLED, non-empty
  address group; where the standardized form differs from the entry, show a
  reusable "use suggested / keep mine" dialog (consolidated for all differing
  groups), apply the choices, then proceed with the existing POST flow.
- **Graceful degradation (hard requirement):** a provider outage, quota
  exhaustion, or disabled-config must NEVER block submission -- on any provider
  error, skip the prompt and submit the entered values. Autocomplete simply
  yields no suggestions.
- **Config-driven vendor + key:** the concrete adapter + API key come from
  environment/config; the mock is the default until T4.

### Vendor comparison (grounded, 2026-05-31; T4 decision -- Adrian's call)

The interface defers this, but here is the current-fact comparison to decide T4:

- **Smarty (US Address Autocomplete Pro + US Street Address verification).**
  USPS **CASS-certified** standardization (authoritative USPS formatting -- a
  direct match for this feature's goal), **SOC 2 + HIPAA compliant**, validates
  to secondary (suite) level. Pricing is simple + predictable: autocomplete
  ~$20/mo for 5k keystroke-lookups (~$0.004 each; can defer firing until ~5
  chars), verification ~$0.60/1k low-volume. **42-day free trial, 1,000 free
  lookups each, no credit card.** No overage charges. Best fit for a single
  demo office + the "USPS-correct" goal; predictable cost.
- **Google Places API (New) Autocomplete + Address Validation.** Autocomplete
  keystrokes are **free when the session terminates** in a Place Details (New)
  or Address Validation request (v4-UUID session token, regenerated per
  selection). Strong if volume grows or Maps Platform is already in use.
  Caveats: the universal $200 credit ended (Feb/Mar 2025) -- now per-product
  free tiers; **abandoned sessions revert to per-request autocomplete billing**
  (~$2.83/1k), making spend harder to forecast; requires a Maps Platform billing
  account + careful session/field-mask handling.
- **Recommendation:** lean **Smarty** for this project (USPS CASS = the exact
  goal, predictable pricing, HIPAA-compliant, generous free trial), with
  **Google Places (New)** as the alternative if Adrian already runs Maps
  Platform or expects higher volume. Either drops into the same interface.

**Alternatives rejected:**
- Hard-coding one vendor now: Adrian chooses later; the abstraction makes T1-T3
  vendor-free. Reject committing before T4.
- Backend-only validation: autocomplete is inherently client-side; doing both
  client-side keeps one integration surface. (A future server-side re-validate
  on the booking POST is possible defense-in-depth but is out of scope here.)
- A zip-regex-only "validation": does not standardize or catch wrong-street/
  wrong-city; misses the goal. Reject.

## Tasks

- T1: Provider-agnostic interface + dev mock + state resolver.
  - approach: tdd
  - files-touched: angular/src/app/shared/address/address-validation.provider.ts (interface + models: `AddressInput`, `AddressSuggestion`, `StandardizedAddress`, `ValidationResult`); a deterministic mock provider; a pure `state-resolver.ts` (name/USPS-code -> StateId from the State lookup) + specs
  - acceptance: interface defines `autocomplete()` + `validate()`; mock returns
    deterministic suggestions/verdicts; state resolver maps name + 2-letter code
    to the right StateId and returns null on no match; unit tests pass.

- T2: Autocomplete attachment across all six groups (incl. the repeatable modal).
  - approach: test-after
  - files-touched: a shared autocomplete directive/component; wire into appointment-add-patient-demographics, appointment-add-employer-details, appointment-add-attorney-section (AA + DA), and appointment-add-claim-information (insurance + CE, runtime-created modal inputs); a per-group field map
  - acceptance: typing in any group's street field shows suggestions; selecting
    one fills street/city/state(resolved to StateId)/zip(+suite where present)
    for THAT group; works for insurance/CE inside a freshly-added Claim
    Information modal. VERIFY + document where AA/DA addresses are persisted
    (gotcha 1) and ensure the standardized value reaches that path.

- T3: Pre-submit standardization dialog + graceful degradation.
  - approach: test-after
  - files-touched: appointment-add.component.ts submit flow; a reusable
    confirm-address dialog component
  - acceptance: on submit, each enabled non-empty address whose standardized
    form differs prompts use-suggested/keep-mine (consolidated); the choices are
    applied before the existing POSTs; with the provider disabled OR erroring,
    submit proceeds with entered values and shows no blocking error.

- T4: Concrete vendor adapter (after Adrian picks Smarty or Google).
  - approach: test-after
  - files-touched: angular/src/app/shared/address/<vendor>-address.provider.ts (new); environment/config (API key, provider switch)
  - acceptance: the adapter implements the interface against the chosen vendor
    (Smarty keystroke-debounce, or Google session-token lifecycle); autocomplete
    + standardization work end-to-end in a built app; mock remains the default
    when no key is configured.

## Risk / Rollback

- Blast radius: booking-form submit flow + six address groups (two inside a
  repeatable modal). A directive bug could mis-fill a field or block submit; the
  graceful-degradation requirement + T3 tests guard the submit path.
- Cost/quota: keystroke or session billing -- mitigated by debounce (Smarty) /
  session tokens (Google) and a low-volume single-office profile; the free
  trials cover development.
- No PHI/BAA constraint (addresses only); no schema change; no OLD parity break.
- Rollback: revert the PR; address fields return to plain free-text entry.

## Verification

Rebuild + serve; in each of the six groups (including a newly-added Claim
Information modal): type a partial street -> pick a suggestion -> street/city/
state/zip(+suite) fill correctly for that group only. Enter a slightly-wrong
zip/street -> on submit the prompt shows standardized vs entered -> both
"use suggested" and "keep mine" submit and persist the chosen values. Disable
the provider (no key) -> autocomplete is inert and submit still succeeds.
Confirm no console errors. Build via `npx ng build --configuration development`.

Sources (vendor facts): Google session pricing
https://developers.google.com/maps/documentation/places/web-service/session-pricing ;
Address Validation billing
https://developers.google.com/maps/documentation/address-validation/usage-and-billing ;
Smarty US Address Autocomplete pricing
https://www.smarty.com/pricing/us-address-autocomplete ; Smarty free trial
https://www.smarty.com/free-address-verification .
