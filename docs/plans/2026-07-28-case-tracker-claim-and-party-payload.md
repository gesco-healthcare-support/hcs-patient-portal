---
feature: Case Tracker claim and party payload (integration Part 6)
date: 2026-07-28
status: complete
base-branch: main
related-issues: []
---

## Goal

Publish the claim, injury and party data the Case Tracker's staff need to decide which of a patient's
records a case files under, so medical records stop being filed against the wrong claim.

## Context & decisions

Parts 1-4 are merged (#393, #395, #396, #397 -> main `bdb6782a`). The Case Tracker's staff currently see
only patient name and date of birth when choosing between a patient's claims, and pick wrong. Every field
that fixes this already exists in the portal and is mandatory at booking; it is simply not published.

Resolved decisions (no open questions remain):

- Decision: `injuries` is an ARRAY at the top level of `data`, because an appointment genuinely supports
  multiple injury rows (a specific plus a cumulative injury) and there is no primary flag. Flattening
  would force US to pick a primary claim with less information than their staff have. Confirmed by Levon
  2026-07-28.
- Decision: send BOTH the raw `claimNumber` / `wcabAdj` and a normalised variant of each, because both
  are free text -- validated only for required and 50 characters, with no pattern, trim or case rule
  (`Application.Contracts/AppointmentInjuryDetails/AppointmentInjuryDetailCreateDto.cs:16-17`). Without a
  normalised form the receiver cannot group two bookings of one claim, which is the whole point of the
  change; without the raw form their staff cannot see what was actually typed.
- Decision: normalisation strips every non-alphanumeric character and uppercases, so `WC-4417`,
  `WC 4417` and `wc4417` all collapse to `WC4417` (Levon's own example). ACCEPTED TRADE-OFF: this can
  theoretically collide two genuinely different claim numbers that differ only in punctuation. Acceptable
  because the receiver groups for HUMAN confirmation and still has the raw value; documented in the
  contract so nobody treats the normalised form as a key.
- Decision: `patient.samePersonGroupKey` is a SHA-256 hex digest over the office tenant id plus the
  patient row id, NOT the raw `Patient.Id`. Levon asked for a hash and he is right: equality is his only
  use, so an opaque value gives identical utility while making it impossible for anyone downstream to
  mistake a real foreign row key for an identifier. Salting with the tenant id makes a cross-office false
  match impossible by construction rather than by everyone remembering the office-scoping rule, and needs
  no secret to provision or rotate -- a rotated secret would silently break every previously published
  key. Brute-forcing is infeasible regardless because the input contains a GUID.
- Decision: `Patient.Id` itself is NEVER published. It is a surrogate row key and means nothing in
  CalMed's or the Case Tracker's world, where patient identity is minted by CalMed. This is why the field
  is not called `portalPatientId`.
- Decision: attorneys carry the FULL address block, not the five-field minimum, because Levon may need to
  serve documents on parties and their deserializer ignores fields it does not consume yet -- taking it
  now costs us nothing and saves a second contract revision and deploy.
- Decision: `primaryInsurances` and `claimExaminers` are TOP-LEVEL arrays, NOT nested inside each
  injury. Levon assumed they nested and asked us not to flatten them for his convenience; the truth is
  the opposite of his assumption. Both entities key on `AppointmentId` with no injury foreign key
  (`AppointmentPrimaryInsurance.cs:19`, `AppointmentClaimExaminer.cs:19`), so on a two-injury appointment
  the portal genuinely does not record which insurer covers which claim. MUST be called out in the reply
  and the contract so their staff never infer a link that does not exist.
- Decision: only `IsActive` insurance and claim-examiner rows are published, since both entities carry
  that flag and an inactive row is one the office has retired.
- Decision: WCAB office is sent as name plus abbreviation, never its id, and DOES nest per injury because
  `AppointmentInjuryDetail.WcabOfficeId` is genuinely per-injury.
- Decision: structured body-part rows are NOT sent. Levon answered "yes to all three" naming insurance,
  claim examiner and WCAB office, and did not ask for coded body parts; `bodyPartsSummary` covers it.
- Decision: this needs a NEW resolver rather than inline additions, because
  `IntakePayloadBuilder.BuildAsync` is already about 52 lines against the repo's 50-line function
  threshold. See T6.

Answers given to Levon that this plan must keep true:
- An empty `injuries` array is possible but not expected. Booking blocks submit without at least one
  entry (`angular/src/app/appointments/appointment-add.component.ts:1874`, wizard guard `:353`, BUG-043
  OLD parity), but that guard is CLIENT-side only and injury rows are written in a separate call, so the
  resolver must handle zero rows without throwing.

## All needed context

| Piece | Anchor |
|---|---|
| Injury fields | `Domain/AppointmentInjuryDetails/AppointmentInjuryDetail.cs:24-43` -- `DateOfInjury`, `ToDateOfInjury`, `ClaimNumber`, `IsCumulativeInjury`, `WcabAdj`, `BodyPartsSummary`, `WcabOfficeId` |
| ONE query for injuries + WCAB office + body parts | `Domain/AppointmentInjuryDetails/IAppointmentInjuryDetailRepository.cs:12` -- `GetListWithNavigationPropertiesAsync(filterText, appointmentId, ...)` |
| Nav-props shape | `AppointmentInjuryDetailWithNavigationProperties.cs:11-14` |
| WCAB office | `Domain/WcabOffices/WcabOffice.cs:21,24` -- `Name`, `Abbreviation` |
| Insurance | `Domain/AppointmentPrimaryInsurances/AppointmentPrimaryInsurance.cs:19-50` -- keyed on `AppointmentId`; `Name`, `Suite`, `PhoneNumber`, `FaxNumber`, `Street`, `City`, `Zip`, `StateId`, `IsActive` |
| Claim examiner | `Domain/AppointmentClaimExaminers/AppointmentClaimExaminer.cs:19-51` -- keyed on `AppointmentId`; adds `Email`, `Fax` |
| Attorneys (denormalised) | `Domain/Appointments/Appointment.cs:75-130` -- `ApplicantAttorney*` and `DefenseAttorney*`; emails at `:58,61` |
| State name | `Domain/.../State.cs:21` -- `Name`. NOTE: no existing payload code resolves a state, this is the first |
| Batch-lookup pattern to mirror | `Payload/DocumentListResolver.cs:90-109` -- `ResolveTypeNamesAsync` resolves referenced ids in ONE query, not per row |
| Payload facade | `Payload/IntakePayloadBuilder.cs:41-92` |
| Patient section | `Payload/PartyResolver.cs:51-58` |
| Serializer (camelCase, nulls kept) | `IntakePayloadSerializer.cs` |
| Contract section A table | `docs/integration/case-tracker-api-contract.md` |

Gotchas:

- The PHI scanner hook rejects 8+ consecutive digits as a possible MRN. Claim-number fixtures must avoid
  long digit runs -- use forms like `WC-SAMPLE-4417`.
- Do NOT resolve states or WCAB offices per row. Batch them, mirroring `ResolveTypeNamesAsync`.
- `IntakePayload` is 199 lines against a 400-line file threshold, so the new DTOs are better as their own
  file than appended there.
- Nulls are deliberately serialized, not omitted (see `IntakePayloadSerializer`), so a null attorney
  object appears explicitly.
- The reconcile GET (Part 4) shares `IIntakePayloadBuilder`, so every field added here appears in the
  pull response automatically. No separate work, but the contract's section F wording must not imply
  otherwise.

## Tasks (implementation blueprint)

### T1 - Claim identifier normaliser

- what: CREATE `Domain/Integration/CaseTracker/Payload/ClaimIdentifierNormalizer.cs` with
  `static string? Normalize(string? value)`: returns null for null/whitespace, else uppercases with
  `ToUpperInvariant` and removes every character that is not a letter or digit.
- pattern: `Payload/IntegrationTimestamp.cs` -- a small pure static helper in the same folder
- approach: tdd
- acceptance: WHEN given `WC-4417`, `WC 4417`, `wc4417` or ` wc-4417 `, THE SYSTEM SHALL return `WC4417`
  for all four. WHEN given null, empty or whitespace, THE SYSTEM SHALL return null. THE SYSTEM SHALL NOT
  alter the caller's original string.

### T2 - Same-person group key hasher

- what: CREATE `Domain/Integration/CaseTracker/Payload/SamePersonGroupKey.cs` with
  `static string Compute(Guid? tenantId, Guid patientId)`: SHA-256 over the invariant string
  `{tenantId:D}|{patientId:D}` (using the literal `host` when `tenantId` is null), returned as lowercase
  hex.
- pattern: `IntegrationOutboxManager.BuildIdempotencyKey` -- same SHA-256-over-composed-string approach
- approach: tdd
- acceptance: THE SYSTEM SHALL return the same value for the same office and patient on every call.
  WHEN the same patient id is computed under two different office ids, THE SYSTEM SHALL return different
  values. THE SYSTEM SHALL NOT include the raw patient id in its output. THE SYSTEM SHALL return a
  64-character lowercase hex string.

### T3 - Claim and party payload DTOs

- what: CREATE `Domain/Integration/CaseTracker/Payload/IntakeClaimSections.cs` holding
  `IntakeInjuryEntry` (`Id`, `DateOfInjury`, `ToDateOfInjury`, `IsCumulativeInjury`, `ClaimNumber`,
  `ClaimNumberNormalized`, `WcabAdj`, `WcabAdjNormalized`, `BodyPartsSummary`, `WcabOffice`),
  `IntakeWcabOfficeSection` (`Name`, `Abbreviation`), `IntakeAttorneySection` (first, last, firm, web
  address, phone, fax, email, street, city, state, zip), `IntakeInsuranceSection` and
  `IntakeClaimExaminerSection`. MODIFY `Payload/IntakePayload.cs` adding `Injuries`,
  `ApplicantAttorney`, `DefenseAttorney`, `PrimaryInsurances`, `ClaimExaminers`; MODIFY
  `IntakePatientSection` adding `SamePersonGroupKey`.
- pattern: the existing sections in `IntakePayload.cs:86-199`, including their XML docs explaining WHY a
  field is nullable
- approach: code
- acceptance: THE SYSTEM SHALL expose the new sections as camelCase JSON with dates as `yyyy-MM-dd`.
  Attorney sections SHALL be nullable; the three collections SHALL default to empty rather than null.

### T4 - Injury resolver

- what: CREATE `Domain/Integration/CaseTracker/Payload/InjuryResolver.cs` (`ITransientDependency`):
  one call to `IAppointmentInjuryDetailRepository.GetListWithNavigationPropertiesAsync(appointmentId: ...)`,
  mapping each row to `IntakeInjuryEntry` with both raw and normalised identifiers and the WCAB office
  name/abbreviation from the navigation property.
- pattern: `Payload/DocumentListResolver.cs` -- repository I/O in a resolver, pure mapping alongside
- approach: tdd
- acceptance: WHEN an appointment has two injuries, THE SYSTEM SHALL return both with their own claim
  numbers. WHEN an injury has no WCAB office, THE SYSTEM SHALL return a null office rather than throwing.
  WHEN an appointment has no injury rows, THE SYSTEM SHALL return an empty list. THE SYSTEM SHALL populate
  the normalised identifiers from `ClaimIdentifierNormalizer`.

### T5 - Party detail resolver

- what: CREATE `Domain/Integration/CaseTracker/Payload/PartyDetailResolver.cs` (`ITransientDependency`)
  returning a composite with `ApplicantAttorney`, `DefenseAttorney`, `PrimaryInsurances` and
  `ClaimExaminers`. Attorneys are read from the appointment's own denormalised columns; insurance and
  examiners from their repositories filtered to `IsActive`. State names for ALL of them resolved in ONE
  batched query.
- pattern: `DocumentListResolver.ResolveTypeNamesAsync:90-109` for the batched id-to-name lookup
- approach: tdd
- acceptance: WHEN neither attorney is recorded, THE SYSTEM SHALL return null for both rather than empty
  objects. WHILE an insurance or examiner row is inactive, THE SYSTEM SHALL omit it. THE SYSTEM SHALL
  resolve every referenced state in a single query regardless of row count. WHEN a state is missing, THE
  SYSTEM SHALL return a null state name rather than throwing.

### T6 - Wire into the payload builder without breaching the function threshold

- what: MODIFY `Payload/IntakePayloadBuilder.cs`: inject `InjuryResolver` and `PartyDetailResolver`, and
  split the existing single `BuildAsync` into `BuildAsync` (resolver orchestration plus envelope) and a
  private `ComposePayload` (the scalar and section assignment). Add `SamePersonGroupKey` to the patient
  section via `PartyResolver`.
- pattern: the facade's existing docstring at `IntakePayloadBuilder.cs:11-15`, which already states that
  I/O belongs in resolvers so each piece stays inside the complexity thresholds
- approach: code
- acceptance: THE SYSTEM SHALL keep every method at or under 50 lines. THE SYSTEM SHALL produce the same
  values as before for every pre-existing field, so Parts 1-4 behaviour is unchanged.

### T7 - Tests

- what: CREATE `test/.../Domain.Tests/Integration/CaseTracker/ClaimIdentifierNormalizerTests.cs`,
  `SamePersonGroupKeyTests.cs`, `InjuryResolverTests.cs`, `PartyDetailResolverTests.cs`; MODIFY
  `IntakePayloadBuilderTests.cs` to assert the new sections appear and that the raw patient id does NOT
  appear anywhere in the serialized JSON.
- pattern: `IntakePayloadBuilderTests.cs` -- real resolvers over mocked repositories, and its existing
  negative assertion that no patient identifier is present
- approach: tdd
- acceptance: The system shall cover normalisation equivalence, office-salted hash inequality, multi-injury
  mapping, the empty-injury case, inactive-row filtering, batched state resolution, and the negative
  assertion that `Patient.Id` never appears in the payload.

### T8 - Contract update

- what: MODIFY `docs/integration/case-tracker-api-contract.md` section A: add the new fields with their
  nullability; state that `primaryInsurances` and `claimExaminers` are per APPOINTMENT and carry NO link
  to a specific injury; state that the normalised identifiers exist for grouping and are not keys, and
  that punctuation-only differences collapse; state that `samePersonGroupKey` is an office-salted hash,
  equality-only, and never a patient identifier. Add a dated revision entry.
- approach: code
- acceptance: THE SYSTEM SHALL document every new field, and SHALL explicitly warn that insurance and
  examiner rows are not linked to an injury.

## Validation loop

From the repo root, in order:

```bash
dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
```
```bash
dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```
```bash
dotnet test HealthcareSupport.CaseEvaluation.slnx
```
```bash
python .claude/scripts/verify_structure.py
```

Done-bar: all four green (structure check 0 FAIL), and no fixture contains real-looking patient data. The
EF test project takes 8-10 minutes; that is normal.

## Risk / rollback

Blast radius: additive to the payload, plus one refactor of a merged file. No migration, no schema change,
no change to any existing field's value. The receiver ignores unknown properties, so it cannot break them
even before they consume the fields.

Two things to watch:
- This meaningfully WIDENS the ePHI leaving the portal: claim numbers, injury dates, body parts, employer
  and insurer details, and attorney contact details. It is all still gated behind
  `CaseTrackerPushEnabled` and must not flow before TLS is in place, which remains blocked on the internal
  DNS name and certificate.
- The reconcile GET shares the payload builder, so these fields appear on that anonymous, token-gated
  endpoint too. That endpoint has no rate limiter by prior decision, so the value of a leaked token goes
  up with this change. Worth revisiting the limiter decision once real data flows.

Rollback: revert the PR. Code-only, no migration. Fields already sent would simply stop being sent; the
receiver tolerates their absence.
