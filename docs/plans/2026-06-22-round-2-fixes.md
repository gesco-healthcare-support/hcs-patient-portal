---
doc: round-2-fixes
date: 2026-06-22
type: plan
status: approved
base-branch: feat/frontend-rework
session: B
review: approved 2026-06-22 (scope + decisions confirmed by Adrian via modal)
---

# Round 2 fixes -- R2-1, R2-2, R2-4, R2-5, R2-6 (+ dev-seed fix)

## Scope + lanes

Build all Session-B-lane items today: R2-1, R2-2, R2-4, R2-5, R2-6, plus the
committed OpenIddict seeding fix. R2-3 (capacity + per-slot booked names) is
Session A's lane -- their #2 rework already built the backend (RemainingCapacity
on the DTO + GetSlotPatientNamesAsync endpoint); deferred to Session A,
internal-staff-only for PHI. Not built here.

## Decisions (Adrian, 2026-06-22)

- D1 -- DA gets FULL parity with Applicant Attorney (master created on register +
  self-edit profile).
- D2 -- Claim Examiner becomes a COMPLETE user: a full entity (add firm fields),
  master-on-register, self-edit profile, and full appointment abilities
  (request / view / reschedule / cancel) like the other parties -- "not just an
  enum somewhere."
- D3 -- Patient + party matching is EMAIL-authoritative: if a party's (AA/DA/CE)
  or patient's email already exists, LINK to that record and fill missing fields;
  never create a duplicate. No production data to migrate (fresh DB).
- D4 -- Capture booker identity on the appointment (the logged-in party, or a
  paralegal who booked on their behalf).
- D5 -- R2-3 -> Session A, internal-staff-only.

## Root cause (verified)

R2-2/R2-4 duplicate-account bug: `AppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync`
and `UpsertDefenseAttorneyForAppointmentAsync` find-or-create the party master by
`ApplicantAttorneyId` / `IdentityUserId` ONLY -- never by email -- so a named
party with an existing email but differing/blank other fields creates a SECOND
master. No unique email index exists on AA/DA/CE. CE has no booking-time master
(free-text email only). R2-5 (reset email) is a symptom: `PasswordResetGate`
requires `EmailConfirmed`, which DA/CE registration does not reliably set.

R2-6: send-back DOES mutate the appointment + child records (verified). The
`address` flag maps only to `Patient.Address` (line 2), not street/city/state/zip.

## Tasks (backend-first; migrations are my exclusive lane)

| # | Task | Approach | Migration |
| --- | --- | --- | --- |
| T1 | R2-1: internal-appointment-detail `historyOpen` -> false (DONE). | code | No |
| T2 | R2-6: split send-back `address` -> addressStreet/City/State/ZipCode (catalog + InfoRequestSnapshot + correction lock + ApplyPatientCorrections + Angular fix-it UI). | test-after | No |
| T3 | Email-authoritative party matching: add `FindByNormalizedEmailAsync` to AA/DA(/CE) repos; rewrite the two Upsert*Attorney methods to match email-first -> update missing fields -> link; create only when no email match. | tdd (matcher) | No |
| T4 | Booker identity: add `BookedByUserId` to Appointment (set from CurrentUser at create). | tdd (guard) | Yes |
| T5 | DA parity: create DA master on register (mirror AA) + ensure EmailConfirmed; MyAttorneyProfile already supports DA (verify). Fixes R2-5. | tdd (signup) | No |
| T6 | CE complete user: add firm fields to ClaimExaminer; manager create/update; CE master on register + email-link; `MyClaimExaminerProfile` self-scoped service + DTOs (mirror #9); Angular CE profile; confirm CE appointment abilities (permissions already granted). | tdd + test-after | Yes |
| T7 | Reset email: with DA/CE confirmed + valid, verify reset sends; move DA FirmName from IdentityUser.ExtraProperties to the DefenseAttorney column. | test-after | No |
| T8 | Filtered unique index on (TenantId, Email) for AA/DA/CE. | code | Yes |
| T9 | Committed OpenIddict ClientId seeding fix (docker-compose.yml db-migrator env, or fix migrator appsettings loading). | code | No |
| T10 | Backend build + pathspec commit + proxy regen + stack rebuild + live verify. | verify-live | -- |

Migrations may be combined (BookedByUserId + CE firm columns + email indexes) into
one or two adds; single-writer, clean-snapshot gate before each.

## Risks / coordination

- Session A has UNCOMMITTED edits to `attorney-profile.*` + `external-appointment-detail.*`.
  R2-4 Angular (DA/CE profile) overlaps -- coordinate; prefer NEW files for the CE
  profile; pathspec commits only; avoid editing files A has open.
- CE-complete-user is the long pole (migration + new service + Angular + proxy
  regen) and may run past the 5:30pm target; land email-matching + DA parity +
  reset first (highest value).
- Email-authoritative matching changes booking behavior -- TDD the matcher and
  verify existing bookings still attach to the right master.

## Verify

- Duplicate-account repro: book naming a DA whose email already exists with a
  different name -> exactly ONE DA master, linked (not duplicated).
- DA + CE register -> self-edit profile -> password-reset email arrives.
- Send-back address sub-fields: flag street/city/state/zip -> party corrects ->
  appointment + patient mutate.
- Fresh reseed -> login works (seeding fix committed, no manual override).
