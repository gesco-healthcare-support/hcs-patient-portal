---
feature: Re-book a new appointment from a prior one that did not happen
date: 2026-08-14
status: draft
base-branch: main
related-issues: []
---

# Item 1 -- Re-book from a prior appointment

## Goal

Let a user start a NEW appointment prefilled from a prior appointment of theirs that was
cancelled, no-showed or not-seen, by entering that appointment's confirmation number.

## Context & decisions

Phase 5 already points users at this flow without providing it: when someone tries to
re-evaluate a `NoShow`/`NotSeen` first evaluation, the error reads _"That appointment was a
first evaluation that was not completed, so it cannot be re-evaluated. Please submit a new
appointment request."_ (`en.json`, key `Appointment:RevalSourceIncompleteFirstEvaluation`).
There is no prefilled path to that new request today.

Resolved decisions:

1. **Decision: add a third lifecycle flow rather than widening Reval's gate**, because Reval
   means "follow-up to an exam that happened" and this is "the exam never happened, book it
   again". Conflating them would corrupt `EvaluationKind`, which the Case Tracker uses to
   label a case folder.
2. **Decision: the new flow produces `EvaluationKind.Evaluation`**, because a re-book after a
   no-show is a first evaluation that finally takes place, not a follow-up.
3. **Decision: mint a fresh confirmation number**, because the source appointment still exists
   as a cancelled/no-show record and keeps its own number; two live appointments sharing one
   number would be indistinguishable in our lists and in the Case Tracker's folder labels.
4. **Decision: DO set `OriginalAppointmentId` on the new appointment** (Adrian, 2026-08-14).
   This gives staff traceability from the new booking back to the one that fell through.
   Consequence, accepted knowingly: it reaches the Case Tracker as `previousAppointmentId`
   alongside `evaluationKind: EVAL`, a combination their contract does not currently describe.
   T8 amends the contract; the change must also be told to Levon.
5. **Decision: both entry points** -- a `?type=3` lookup mirroring Reval, and a "Book again"
   deep link from the source appointment mirroring the existing `?mode=rerequest&source=<n>`
   pattern. The requirement describes typing a number; the deep link means they usually will
   not have to.
6. **Decision: enforce caller-linkage server-side for ALL THREE flows**, not just the new one.
   Today `LoadRevalSourceAsync` and `LoadResubmitSourceAsync` check status only; the
   accessor/creator check exists solely in `GetByConfirmationNumberAsync`, which is the read
   path the UI happens to call first. Confirmation numbers are sequential (`A00005`, `A00036`,
   `A00065`) and therefore guessable, so this is a real hole and adding a third instance of the
   same pattern while leaving two open would be worse than fixing them together.
7. **Decision: internal staff get the same status gate with a distinct message**, mirroring the
   `RevalSourceNotApprovedAdminHint` precedent where IT Admin sees different wording but is
   still refused. The override is not a free pass.

## All needed context

| Fact                                                                                                    | Anchor                                               |
| ------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| Flow enum, `ReSubmit = 1, Reval = 2`                                                                    | `AppointmentLifecycleValidators.cs:156`              |
| `CanResubmit(sourceStatus)` -- simplest gate to mirror                                                  | `AppointmentLifecycleValidators.cs:26`               |
| `ResolveConfirmationNumber(flow, source, freshlyGenerated)`                                             | `AppointmentLifecycleValidators.cs:121`              |
| `LoadResubmitSourceAsync` -- shortest load+gate to mirror                                               | `AppointmentManager.cs:151`                          |
| `LoadRevalSourceAsync` -- the variant with a role-aware message                                         | `AppointmentManager.cs:184`                          |
| `CreateRevalAsync` -- app-service shape to mirror                                                       | `AppointmentsAppService.cs:689`                      |
| `CreateAppointmentInternalAsync(input, lifecycleFlow, sourceConfirmationNumber, originalAppointmentId)` | `AppointmentsAppService.cs:~736`                     |
| `EvaluationKindPolicy.FromLifecycleFlow`                                                                | `EvaluationKindPolicy.cs:19`                         |
| Where `EvaluationKind` is written on create                                                             | `AppointmentsAppService.cs:906`                      |
| `EnsureCanReadAppointmentAsync` -- the linkage check to reuse                                           | `AppointmentsAppService.cs:344`                      |
| Error-code constants (Reval/ReSubmit)                                                                   | `CaseEvaluationDomainErrorCodes.cs:293,305,319,332`  |
| Error-code -> localization mapping                                                                      | `AppointmentExceptionTranslator.cs:93`               |
| Angular `bookingMode: 'new' \| 'reval' \| 'reRequest'` + `?type=` doc block                             | `appointment-add.component.ts:300-330`               |
| Booking route (registered twice: internal + external shells)                                            | `app.routes.ts:66` and `:419`                        |
| Attendance statuses helper                                                                              | `AppointmentLifecycleValidators.IsAttendanceOutcome` |

Gotchas:

- `EnsureCanReadAppointmentAsync` lives in the Application layer but the gates live in the
  Domain manager. The linkage check needs a seam that does not invert the architecture --
  pass the caller's identity into the manager, or perform the check in the app service before
  delegating. Do NOT move `EnsureCanReadAppointmentAsync` into Domain.
- The route is registered TWICE. A new `?type=3` must work through both.
- `AppointmentExceptionTranslator` must learn the new codes or the user sees a raw code.

## Tasks

### T1 -- add the flow and its predicates

approach: tdd

MODIFY `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentLifecycleValidators.cs`:
add `ReBook = 3` to `AppointmentLifecycleFlow` (:156); add
`CanCreateReBook(AppointmentStatusType sourceStatus)` returning true only for
`CancelledNoBill`, `CancelledLate`, `NoShow`, `NotSeen`; add
`ResolveReBookRejectionCode(AppointmentStatusType sourceStatus, bool callerIsInternal)`.

pattern: `CanResubmit` (:26) for the predicate shape, `ResolveRevalRejectionCode` for the
role-aware code selection.

acceptance (EARS):

- WHEN the source status is `CancelledNoBill`, `CancelledLate`, `NoShow` or `NotSeen`, THE
  SYSTEM SHALL return true from `CanCreateReBook`.
- WHEN the source status is any other value, THE SYSTEM SHALL return false.
- WHERE the caller is internal, THE SYSTEM SHALL resolve a distinct rejection code from the
  external one.

### T2 -- error codes and messages

approach: code

MODIFY `CaseEvaluationDomainErrorCodes.cs`: add `AppointmentReBookSourceNotEligible` and
`AppointmentReBookSourceNotEligibleStaffHint`. MODIFY
`Domain.Shared/Localization/CaseEvaluation/en.json` with both messages. MODIFY
`AppointmentExceptionTranslator.cs:93` region to map both.

acceptance (EARS): WHEN a re-book is refused, THE SYSTEM SHALL return a localized message
naming the statuses that ARE eligible, never a raw error code.

### T3 -- confirmation number + evaluation kind

approach: tdd

MODIFY `AppointmentLifecycleValidators.ResolveConfirmationNumber` (:121) so `ReBook` returns
the freshly generated number. MODIFY `EvaluationKindPolicy.FromLifecycleFlow` (:19) so `ReBook`
returns `EvaluationKind.Evaluation`.

acceptance (EARS):

- WHEN the flow is `ReBook`, THE SYSTEM SHALL use the freshly generated confirmation number.
- WHEN the flow is `ReBook`, THE SYSTEM SHALL persist `EvaluationKind.Evaluation`.

### T4 -- domain gate

approach: tdd

MODIFY `AppointmentManager.cs`: add `LoadReBookSourceAsync(string sourceConfirmationNumber,
bool callerIsInternal)`.

pattern: `LoadResubmitSourceAsync` (:151); throw `EntityNotFoundException` when absent and
`BusinessException` with the resolved code when ineligible, carrying `.WithData("confirmationNumber", ...)`
and `.WithData("status", ...)` as `LoadRevalSourceAsync` (:199-201) does.

acceptance (EARS):

- WHEN no appointment matches the confirmation number, THE SYSTEM SHALL throw
  `EntityNotFoundException`.
- IF the source is ineligible, THEN THE SYSTEM SHALL throw a `BusinessException` carrying the
  source status as data.

### T5 -- caller-linkage on all three create paths

approach: tdd

MODIFY `AppointmentsAppService.cs`: before delegating in `CreateRevalAsync` (:689),
`ReSubmitAsync` (:672) and the new re-book endpoint, verify the caller may read the source
appointment.

pattern: `EnsureCanReadAppointmentAsync` (:344), already used by
`GetByConfirmationNumberAsync` (:287).

acceptance (EARS):

- WHEN an external caller supplies a confirmation number for an appointment they neither
  created nor accessor on, THE SYSTEM SHALL refuse the create for re-book, re-eval AND
  re-submit alike.
- WHEN an internal caller supplies a confirmation number within their tenant, THE SYSTEM SHALL
  allow it.
- The refusal SHALL NOT reveal whether the confirmation number exists.

### T6 -- the app-service endpoint

approach: tdd

MODIFY `AppointmentsAppService.cs` + `IAppointmentsAppService.cs`: add
`CreateReBookAsync(string sourceConfirmationNumber, AppointmentCreateDto input)` that loads the
source via T4, enforces T5, and calls `CreateAppointmentInternalAsync` with
`lifecycleFlow: AppointmentLifecycleFlow.ReBook`, `sourceConfirmationNumber: source.RequestConfirmationNumber`
and `originalAppointmentId: source.Id`.

pattern: `CreateRevalAsync` (:689) verbatim, minus the IT-Admin role read.

acceptance (EARS): WHEN a re-book is created from an eligible source, THE SYSTEM SHALL persist
a new appointment with a fresh confirmation number, `EvaluationKind.Evaluation`, and
`OriginalAppointmentId` set to the source's id.

### T7 -- Angular mode, route and entry points

approach: test-after

MODIFY `appointment-add.component.ts`: extend `bookingMode` to include `'reBook'`, decode
`?type=3`, and update the doc block at :300-330. Reuse the existing source-lookup UI. MODIFY the
source appointment detail component to add a "Book again" action, visible only when the status
is one of the four eligible ones, deep-linking with the confirmation number pre-filled. Verify
both route registrations (`app.routes.ts:66`, `:419`).

pattern: the `reRequest` deep link (`?mode=rerequest&source=<conf#>`) already launched from a
rejected appointment's view page.

acceptance (EARS):

- WHEN a user opens the booking route with `?type=3`, THE SYSTEM SHALL show the
  confirmation-number lookup.
- WHERE an appointment is cancelled, no-showed or not-seen AND the viewer may read it, THE
  SYSTEM SHALL offer a "Book again" action.
- WHILE the source has not been loaded, THE SYSTEM SHALL keep Submit disabled.

### T8 -- contract amendment for the new field combination

approach: code

MODIFY `docs/integration/case-tracker-api-contract.md`: record that `previousAppointmentId`
can now be present on an appointment whose `evaluationKind` is `EVAL`, meaning "this replaces an
appointment that did not take place", distinct from the re-evaluation meaning it has carried
until now.

acceptance (EARS): WHEN a re-book is pushed, THE CONTRACT SHALL describe the
`previousAppointmentId` + `EVAL` combination and what it means.

NOTE FOR THE BUILDER: this is a receiver-visible change. Tell Adrian when this task lands so it
reaches Levon rather than arriving unannounced.

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

Blast radius: T5 tightens two SHIPPED flows (re-eval, re-submit). Anything relying on creating
against an unlinked confirmation number breaks -- nothing legitimately should, but this is the
one task that changes existing behaviour rather than adding to it.

Rollback: revert the PR. No schema change, no migration, so no data to unwind.
