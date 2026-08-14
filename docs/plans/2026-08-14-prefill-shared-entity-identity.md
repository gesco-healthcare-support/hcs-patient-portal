---
feature: Stop prefill from overwriting shared attorney records
date: 2026-08-14
status: draft
base-branch: main
related-issues: []
---

# Item 3 -- Shared-entity identity in prefill

## Goal

Stop a prefilled booking from overwriting the shared attorney master record, while still reusing
the same attorney and the same patient row.

## Context & decisions

The prefill carries `applicantAttorneyId` / `defenseAttorneyId` plus concurrency stamps forward
so the upsert reuses the existing attorney. That reuse is wanted; the mechanism is not. The
attorney upsert has three branches (`AppointmentsAppService.cs:1295-1340`):

| Condition            | Behaviour                                                                             |
| -------------------- | ------------------------------------------------------------------------------------- |
| id present           | `GetAsync` then `UpdateAsync` with input values **unconditionally** -- blanks clobber |
| no id, email matches | `UpdateAsync` with `input.X ?? existing.X` -- **merge**, blanks preserved             |
| neither              | `CreateAsync` -- new row                                                              |

The middle branch's own comment (R2-2, 2026-06-22) states _"email is the authoritative identity
for a party"_. So the carried id is not required to get reuse -- it only decides whether blanks
destroy data on a record other appointments also point at.

Resolved decisions:

1. **Decision: stop carrying the attorney ids entirely**, because email matching already
   reuses the same master with merge semantics, so we keep the reuse and lose the clobber. A
   genuinely different attorney falls through to `CreateAsync` on its own.
2. **Decision: the patient keeps one row.** Preserve `patientId` and `isExisting`, because
   `samePersonGroupKey` is computed from `Patient.Id` and is what tells the Case Tracker two
   claims belong to the same person. Creating a second patient row would break that link.
3. **Decision: no change for Employer, Primary Insurance or Claim Examiner**, because each is a
   per-appointment child row carrying its own `AppointmentId` -- copying and editing them
   already creates fresh rows and cannot affect a prior appointment.
4. **Not in scope: the legal-trail guarantee for attorneys.** It already holds. `Appointment`
   carries ~20 denormalised attorney columns and `PartyDetailResolver.cs:103-108` reads from the
   appointment, not the shared entity, so editing the master never changes what a prior
   appointment reports. Patient is the only entity that violates the guarantee, and that is
   item 5.

## All needed context

| Fact                                 | Anchor                                                                                             |
| ------------------------------------ | -------------------------------------------------------------------------------------------------- |
| Ids + stamps returned by the prefill | `reval-prefill.mapper.ts:55-58`                                                                    |
| Ids assigned into the component      | `appointment-add.component.ts:1463-1466`                                                           |
| Id sent on submit                    | `appointment-add.component.ts:3044` (`applicantAttorneyId: this.applicantAttorneyId ?? undefined`) |
| Concurrency stamp sent on submit     | same body object, `concurrencyStamp` key                                                           |
| Three-branch upsert                  | `AppointmentsAppService.cs:1295-1340`                                                              |
| Email lookup used by branch 2        | `_applicantAttorneyRepository.FindByNormalizedEmailAsync`                                          |
| Patient reuse flag                   | `appointment-add.component.ts:1953` `isPatientAlreadyExist`                                        |

Gotchas:

- The ids are ALSO set by the attorney search/pick flows (`appointment-add.component.ts:2938`,
  `:3010`). Those are legitimate -- the booker explicitly chose an existing master. Only the
  PREFILL assignment should stop; do not remove the fields.
- Branch 2 still writes changed non-null values to the master. That is intended: an attorney who
  moved office should have their directory record updated. What it no longer does is null out
  fields the booker happened to clear.

## Tasks

### T1 -- drop the ids from the prefill result

approach: tdd

MODIFY `angular/src/app/appointments/shared/reval-prefill.mapper.ts`: remove
`applicantAttorneyId`, `applicantAttorneyConcurrencyStamp`, `defenseAttorneyId` and
`defenseAttorneyConcurrencyStamp` from the result type (:55-58) and stop populating them. Update
the header comment, which currently states the ids are carried deliberately, to record why they
no longer are.

acceptance (EARS): WHEN a prefill is built from a source appointment, THE SYSTEM SHALL NOT return
an attorney entity id or concurrency stamp.

### T2 -- stop assigning them in the component

approach: test-after

MODIFY `appointment-add.component.ts:1463-1466`: remove the four assignments. Leave the fields
and the search-flow assignments at `:2938` and `:3010` untouched.

acceptance (EARS):

- WHEN a booking is prefilled from a source, THE SYSTEM SHALL submit with no attorney id, so the
  server resolves the master by email.
- WHEN the booker explicitly picks an attorney from search, THE SYSTEM SHALL still submit that
  attorney's id.

### T3 -- regression cover for the merge path

approach: tdd

CREATE or MODIFY a backend test asserting the three upsert branches: id present overwrites,
email match merges, neither creates. This behaviour is now load-bearing for the trail guarantee
and is currently only exercised incidentally.

pattern: existing app-service tests under
`test/HealthcareSupport.CaseEvaluation.Application.Tests/`.

acceptance (EARS):

- WHEN an upsert supplies no id and an email matching an existing attorney, THE SYSTEM SHALL
  preserve existing values for fields the input leaves null.
- WHEN an upsert supplies no id and an unmatched email, THE SYSTEM SHALL create a new attorney.

## Validation loop

```bash
cd /c/src/patient-portal/main && dotnet format --verify-no-changes
```

```bash
cd /c/src/patient-portal/main && dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```

```bash
cd /c/src/patient-portal/main && dotnet test HealthcareSupport.CaseEvaluation.slnx -c Release --no-build
```

```bash
cd /c/src/patient-portal/main && npx ng build
```

```bash
cd /c/src/patient-portal/main && export CHROME_BIN=<chrome path> && npx ng test --watch=false --browsers=ChromeHeadless --include='**/appointments/**/*.spec.ts'
```

## Risk / rollback

Blast radius: every prefilled booking (re-eval, re-submit, and re-book once item 1 lands). The
behavioural change is that a cleared field no longer clears it on the attorney master. If a
booker previously relied on blanking a field to remove it from the directory record, that stops
working -- which is the point.

Rollback: revert the PR. No schema change.
