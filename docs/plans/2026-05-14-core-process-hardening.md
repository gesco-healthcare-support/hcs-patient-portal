---
slug: 2026-05-14-core-process-hardening
status: approved
audience: Adrian
session: main-worktree userflow testing (hardening pass)
supersedes-execution: 2026-05-14-multi-user-workflows.md (workflows F/G/H/I deferred)
approved-by: Adrian 2026-05-14
---

## Approval directives (2026-05-14)

1. **Throttling**: don't worry about M365 4.5.127 retries; send away.
2. **Phase ordering**: happy paths across all phases FIRST (Round 1), then failure modes across all phases (Round 2).
3. **Bug-blocking policy**: document any bug, continue unless the flow is blocked. If blocked: try API workaround → then SQL workaround → then document + skip + move to next flow. Only stop when the entire plan is implemented at least once.
4. **Email confirmation**: Adrian confirms each email + pastes verification/reset/etc links as they come.

## Round structure

### Round 1 — happy paths (verify the system works as designed)

| Step | Phase / Section | Scenarios |
| --- | --- | --- |
| R1.0 | Pre-execution prep | P0 (register externals), P1 (role grants), P2 (doctor availabilities) |
| R1.1 | Phase 1 happy | 1.1, 1.2, 1.3, 1.4 (already covered as part of P0) |
| R1.2 | Phase 2 happy | 2.1 (first login post-verify), 2.5 (ForgotPassword Razor flow), 2.7 (reset via API direct), 2.8 (sign in after reset), 2.9 (subdomain sign-in if BUG-016 supports it) |
| R1.3 | Phase 3 happy | All 20 scenarios 3.1 – 3.20 (booking matrix) |
| R1.4 | Phase 5 happy | 5.1 (Staff approve), 5.2 (Staff approve with different Responsible), 5.4 (Staff reject), 5.8 (Supervisor approve), 5.9 (Admin approve) |
| R1.5 | Phase 6 happy | All 8 scenarios 6.1 – 6.8 (packets per appointment type) — verified during R1.3 approvals |
| R1.6 | Phase 7 happy | 7.1 (Patient PDF), 7.2 (Patient PNG), 7.3 (Patient JPG), 7.8 (AA PDF), 7.9 (DA PDF), 7.11 (anonymous via verification-code) |
| R1.7 | Phase 8 happy (scope-works) | 8.1 (DA out-of-scope), 8.2 (AA out-of-scope), 8.3 (CE out-of-scope), 8.4 (Patient cross-Patient), 8.6 (anonymous redirect to login) |

### Round 2 — failure modes (verify the system correctly rejects)

| Step | Phase / Section | Scenarios |
| --- | --- | --- |
| R2.1 | Phase 1 failures | 1.5 (dup email), 1.6 (AA missing Firm), 1.7 (empty form), 1.8 (weak pw), 1.9 (pw mismatch), 1.10 (bad email), 1.11 (tampered verify link), 1.12 (verify link replay) |
| R2.2 | Phase 2 failures | 2.2 (unverified login → ConfirmUser), 2.3 (bad password), 2.4 (lockout), 2.6 (reset link via SPA — known BUG-011) |
| R2.3 | Phase 4 (all failure) | All 15 scenarios 4.1 – 4.15 (booking validation gates) |
| R2.4 | Phase 5 failures | 5.3 (no Responsible), 5.5 (reject empty comment), 5.6 (double-approve), 5.7 (approve then reject), 5.10 (Patient tries to approve) |
| R2.5 | Phase 7 failures | 7.4 (.exe upload), 7.5 (.zip upload), 7.6 (0-byte), 7.7 (10 MB), 7.10 (DA out-of-scope upload), 7.12 (anonymous wrong code) |
| R2.6 | Phase 8 failure-mode-edge | 8.5 (non-existent GUID), 8.7 (filter bypass) |

# Core process hardening plan -- exhaustive corner-case coverage

## Context

Adrian's directive 2026-05-14: the priority is making the **core booking process** as hardened and bug-proof as possible. The 8-workflow plan covered the happy path slices (B-E verified; F/G/H/I were optional surface tours). The hardening pass covers **every variation** of:

- Registration (per role × per failure mode)
- Login + verification + password-reset
- Booking (every booker × patient state × attorney/CE/authorized-user combination × claim-info shape)
- Booking validation (every gate, every required-field path)
- Approval / Rejection (every approver role × every decision shape)
- Packet generation (every appointment type × every claim-info shape × delivery edge case)
- Document upload (every uploader × file type × access boundary)
- Cross-actor scope (every actor pair × in-scope + out-of-scope)

20 bugs filed in `docs/runbooks/findings/bugs/` (BUG-001..020 + OBS-1..14 + SEED-1..2). Adrian's fixing those separately; the hardening pass uses the system in its current state and surfaces additional gaps.

## Confirmed configuration (2026-05-14 fresh-DB rebuild)

- **SMTP host**: `mail.securemailprotocol.com:587` (our own creds, not Azure ACS).
- Verified by container-side `cat /app/src/.../appsettings.secrets.json`.
- M365 burst-protection (`4.5.127`) was the only delivery blocker last session. Per Adrian's 2026-05-14 directive ("don't care about rate-limits") this campaign sends without throttling; any 4.5.127 retries are captured in logs but not paused for.
- `AbpSettings` table is empty post-rebuild; SMTP creds resolved purely from `IConfiguration`. [[BUG-020]] log noise expected on every send (harmless).

## Test-data inventory (used across all phases)

| Asset | Source | Notes |
| --- | --- | --- |
| Patient inbox | `SoftwareThree@gesco.com` | Real Gmail inbox; receives all Patient-targeted emails |
| AA inbox | `SoftwareFour@gesco.com` | Real Gmail inbox |
| DA inbox | `SoftwareFive@gesco.com` | Real Gmail inbox |
| CE inbox | `SoftwareSix@gesco.com` | Real Gmail inbox |
| Internal Staff inbox | `SoftwareOne@evaluators.com` | Real Gmail inbox (post role-grant) |
| Internal Supervisor inbox | `SoftwareTwo@evaluators.com` | Real Gmail inbox (post role-grant) |
| Tenant admin | `admin@falkinstein.test` | Seeded; no real inbox; used for permission-gate negative tests |
| Seeded synthetic externals | `patient@falkinstein.test`, `applicant.attorney@falkinstein.test`, `defense.attorney@falkinstein.test`, `adjuster@falkinstein.test` | Seeded; no real inboxes; useful for bulk variation runs where email-content isn't asserted |
| IT Admin | `it.admin@hcs.test` | Host-scope; used in cross-tenant tests |
| Default password | `1q2w3E*r` | All seeded + registered accounts |
| Appointment types | AME, Deposition, Panel QME, QME, Record Review, Supplemental Medical Report | 6 seeded types |
| Locations | Demo Clinic North, Demo Clinic South | 2 seeded |
| Doctor availability slots | None on fresh DB (per [[SEED-2]]) | Will be re-seeded in Prep |
| Pre-fill content | `docs/plans/2026-05-14-form-prefill-content.md` | All field values for the booking form |

## Prep (one-time on fresh DB)

| # | Action | Time |
| --- | --- | --- |
| P0 | Register SoftwareThree (Patient), SoftwareFour (AA), SoftwareFive (DA), SoftwareSix (CE). Verify each via emailed link. | ~10 min |
| P1 | Admin UI: grant SoftwareOne + SoftwareTwo additional roles (Clinic Staff + Staff Supervisor on top of existing admin). | ~3 min |
| P2 | Supervisor UI: seed doctor availabilities for AME, QME, Deposition, Re-Eval (using existing types) across June + July + August 2026. 15-min slot intervals. Aim for 1000+ slots across types. | ~10 min |

## Phase 1 -- Registration (12 scenarios)

| # | Goal | Steps | Expected |
| --- | --- | --- | --- |
| 1.1 | Patient happy path | Register fresh email as Patient with full fields | 204 + success banner + EmailConfirmed=0 |
| 1.2 | AA with Firm Name | Register fresh email as AA, fill Firm Name | 204 + success banner |
| 1.3 | DA with Firm Name | Same as 1.2 for DA | 204 + success banner |
| 1.4 | CE (no Firm Name field) | Register fresh email as CE, confirm Firm Name input does NOT appear | 204 + success banner; OBS-8 confirmed |
| 1.5 | Duplicate email | Submit register with email that already exists | 400 + generic "If new, you'll receive verification" message; NO email echo (BUG-001/002/003 fixes hold) |
| 1.6 | AA missing Firm Name | Pick AA, leave Firm Name empty, submit | Either 400 with field validation OR generic error per [[BUG-012]] (currently observed) |
| 1.7 | Empty form submit | Click Sign Up with no fields filled | Button should be DISABLED per [[BUG-005]] fix |
| 1.8 | Weak password (`abc123`) | Submit with weak password | 400 + password-policy message |
| 1.9 | Password / Confirm mismatch | Submit with non-matching | 400 + mismatch message |
| 1.10 | Email malformed (`not-an-email`) | Submit with bad email shape | 400 + format message |
| 1.11 | Verify-link tampered | Modify confirmationToken in URL by 1 char | SPA shows "could not verify" + ability to request fresh link |
| 1.12 | Verify-link replay | Verify once successfully; re-navigate the same link | Either "already verified" or "could not verify" (TBD which) |

## Phase 2 -- Login / verification / password reset (9 scenarios)

| # | Goal | Steps | Expected |
| --- | --- | --- | --- |
| 2.1 | First login post-verify | Verify SoftwareThree, sign in | Lands on /home with role-appropriate dashboard |
| 2.2 | Login unverified user | Try to sign in before verifying email | Redirect to /Account/ConfirmUser with "Verify" button. Button currently broken per [[BUG-013]] — confirm; use /Account/ResendVerification?autosend=1 workaround |
| 2.3 | Bad password | Sign in with correct email + wrong password | 400 + invalid-credentials message |
| 2.4 | Locked account (5 bad attempts) | Submit wrong password 5+ times | Account lockout message |
| 2.5 | ForgotPassword Razor flow | `/Account/ForgotPassword` → submit email → expect redirect to PasswordResetLinkSent | 302 + email delivered with reset link |
| 2.6 | Reset password via link | Click reset link in email → SPA `/account/reset-password?userId=&resetToken=` | Per [[BUG-011]] currently broken; document state |
| 2.7 | Reset password API direct | `POST /api/account/reset-password` with the token | 204 (workaround for BUG-011) |
| 2.8 | Sign in after reset | Try old password → 400. Try new password → 200 + landing | Both behaviors confirmed |
| 2.9 | Sign in across subdomains | After login at `falkinstein.localhost:4200`, navigate `localhost:4200` directly | Verify OpenIddict redirect-URI handling per [[BUG-016]] |

## Phase 3 -- Booking happy paths (20+ scenarios)

The booker × patient-state × attorney × claim-info matrix collapses into representative scenarios:

| # | Booker | Patient | AA | DA | CE | Insurance | Cumulative | Multi-Injury | Custom Fields | Auth Users |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3.1 | Patient (SoftwareThree) | self (existing) | OFF | OFF | ON | ON | No | 1 | none | none |
| 3.2 | Patient | self (existing) | ON | OFF | ON | ON | No | 1 | none | none |
| 3.3 | Patient | self (existing) | OFF | ON | ON | ON | No | 1 | none | none |
| 3.4 | Patient | self (existing) | ON | ON | ON | ON | No | 1 | none | none |
| 3.5 | Patient | self (existing) | ON | OFF | OFF | OFF | No | 1 | none | none |
| 3.6 | Patient | self (existing) | OFF | OFF | ON | ON | YES | 1 | none | none |
| 3.7 | Patient | self (existing) | OFF | OFF | ON | ON | No | 2 | none | none |
| 3.8 | Patient | self (existing) | OFF | OFF | ON | ON | No | 1 | filled | none |
| 3.9 | Patient | self (existing) | OFF | OFF | ON | ON | No | 1 | none | SoftwareOne (View) |
| 3.10 | Patient | self (existing) | OFF | OFF | ON | ON | No | 1 | none | SoftwareOne (Edit) |
| 3.11 | AA (SoftwareFour) | new (Jane Doe) | ON | OFF | ON | ON | No | 1 | none | none |
| 3.12 | AA | existing (SoftwareThree) | ON | OFF | ON | ON | No | 1 | none | none |
| 3.13 | DA (SoftwareFive) | new (John Smith) | OFF | ON | ON | ON | No | 1 | none | none |
| 3.14 | CE (SoftwareSix) | new (Mary Brown) | OFF | OFF | ON | ON | No | 1 | none | none |
| 3.15 | Staff (SoftwareOne) | existing | ON | OFF | ON | ON | No | 1 | none | none |
| 3.16 | Admin (admin@falkinstein.test) | existing | ON | OFF | ON | ON | No | 1 | none | none |
| 3.17 | Patient | self (existing) | ON | OFF | ON | ON | No | 1 | none | none — appointment type = QME (not AME) |
| 3.18 | Patient | self (existing) | ON | OFF | ON | ON | No | 1 | none | none — type = Deposition |
| 3.19 | Patient | self (existing) | ON | OFF | ON | ON | No | 1 | none | none — type = Record Review |
| 3.20 | Patient | self (existing) | ON | OFF | ON | ON | No | 1 | none | none — type = Supplemental |

After each successful submit, verify:
- HTTP 200/201 with appointment GUID
- `AppAppointments` row with all FKs populated + Status=Pending(1)
- Linked rows in `AppAppointmentApplicantAttorneys` / `AppAppointmentDefenseAttorneys` / `AppAppointmentInjuryDetails` / `AppAppointmentClaimExaminers` / `AppAppointmentPrimaryInsurances` / `AppAppointmentEmployerDetails` per the toggles
- Confirmation# A##### auto-generated
- DoctorAvailability slot flipped BookingStatusId=Booked(2)

## Phase 4 -- Booking validation failures (15 scenarios)

| # | Goal | Steps | Expected |
| --- | --- | --- | --- |
| 4.1 | Inside 3-day lead time | Pick a date within 3 days of today (would be today-ish) | 403 Forbidden + BUG-009 generic message (which is the pending fix) |
| 4.2 | No availability for date | Pick a date the doctor has no slots for | Date picker disables; if URL-forced, server rejects |
| 4.3 | Simultaneous booking race | Two browser tabs submit for same slot within 1s | One succeeds, other fails with 409/400. Verify which via logs |
| 4.4 | Submit with empty AppointmentType | Bypass UI required-attr; submit empty | 400 with field validation |
| 4.5 | Submit with empty Location | Same | 400 |
| 4.6 | Submit with empty Date | Same | 400 |
| 4.7 | Submit with empty Last/First Name | Same | 400 |
| 4.8 | Invalid SSN (5 chars) | Type "12345" in SSN | UI mask blocks; server validates |
| 4.9 | Invalid phone (letters) | Type "abcdef" in phone | UI mask blocks; server validates |
| 4.10 | DOB in future | Pick a future date for DOB | UI picker maxDate blocks; server validates |
| 4.11 | DOB > 120 years ago | Pick 1850 | UI minDate blocks |
| 4.12 | AA email = existing Patient | AA email field = `SoftwareThree@gesco.com` (a Patient) | Either error OR auto-links (potential security issue per Workflow B's predicted-observations) |
| 4.13 | Empty body parts in Claim Info modal | Open modal, leave Body Parts empty, click Add | Validation blocks |
| 4.14 | Empty CE Name when CE toggle ON | Same shape | Validation blocks |
| 4.15 | 2nd injury same Date Of Injury | Click Add twice in Claim Info modal with same DOI | Either dedup or both saved (TBD) |

## Phase 5 -- Approval / Rejection (10 scenarios)

| # | Approver | Decision | Responsible User | Comments | Expected |
| --- | --- | --- | --- | --- | --- |
| 5.1 | Staff (SoftwareOne) | Approve | self | yes | Status=Approved(2), packets generated, emails fan out |
| 5.2 | Staff | Approve | Supervisor (SoftwareTwo) | yes | Same, Responsible email goes to SoftwareTwo |
| 5.3 | Staff | Approve | (none selected) | n/a | 400 + "Responsible User required" |
| 5.4 | Staff | Reject | n/a | yes (mandatory?) | Status=Rejected(3), rejection email fans out, no packets generated |
| 5.5 | Staff | Reject | n/a | empty | If comments required: 400. Else: rejection email with no body |
| 5.6 | Staff | Approve A00001 twice | self | yes | Second approve: 400/409 (already approved) OR silent idempotent |
| 5.7 | Staff | Approve then Reject | self | yes | Second action: 400 (already approved) OR allowed transition |
| 5.8 | Supervisor | Approve | self | yes | Same as 5.1 |
| 5.9 | Admin | Approve | self | yes | Same as 5.1 (or 403 if admin lacks the permission) |
| 5.10 | Patient | Attempt to approve | n/a | n/a | 403 (Patient can't approve own appt) |

## Phase 6 -- Packet generation (8 scenarios)

Each scenario is "book + approve" then inspect packets.

| # | Variation | Verify |
| --- | --- | --- |
| 6.1 | AME, single injury, CE+Insurance ON | 3 packets generated (Patient, Doctor, AttyCE); PDFs render; CE packet pruned post-email |
| 6.2 | QME, single injury, CE+Insurance ON | 3 packets; PDF content differs from AME |
| 6.3 | Deposition | Verify packet template variation |
| 6.4 | Record Review | Verify packet template variation |
| 6.5 | Supplemental Medical Report | Verify packet template variation |
| 6.6 | Multi-injury (2 rows) | 3 packets, with both injuries listed in Patient + Doctor packets |
| 6.7 | Cumulative trauma | DOI shows From/To range in packet body |
| 6.8 | No CE / no Insurance | AttyCE packet not generated (or generated but minimal); Patient + Doctor packets unaffected |

## Phase 7 -- Document upload (12 scenarios)

| # | Uploader | File | Target | Expected |
| --- | --- | --- | --- | --- |
| 7.1 | Patient (authenticated) | small PDF (10 KB) | own Approved appt | 201 + AppointmentDocument row, blob in MinIO |
| 7.2 | Patient | small PNG | own appt | 201 |
| 7.3 | Patient | small JPG | own appt | 201 |
| 7.4 | Patient | .exe | own appt | 400 + content-type rejection |
| 7.5 | Patient | .zip | own appt | TBD — accepts or rejects |
| 7.6 | Patient | 0-byte file | own appt | 400 |
| 7.7 | Patient | 10 MB file | own appt | Either 201 or 413 (size cap) |
| 7.8 | AA | PDF | linked appt | 201 (AA has access) |
| 7.9 | DA | PDF | linked appt (after Approve) | 201 |
| 7.10 | DA | PDF | not-linked appt (out of scope) | 403 |
| 7.11 | Anonymous via verification-code link | PDF | by appt + code | 201; rate-limit on 6th burst → 429 |
| 7.12 | Anonymous with WRONG code | PDF | by appt + bad code | 403 + Document:UnauthorizedVerificationCode |

## Phase 8 -- Cross-actor scope (8 scenarios)

| # | Actor | Out-of-scope appt | URL probe | Expected |
| --- | --- | --- | --- | --- |
| 8.1 | DA | A00001 (no DA link) | direct /appointments/view/{A00001} | 403 + UI "no permission" (already verified WC) |
| 8.2 | AA | A00002 (DA-only, no AA) | direct URL | 403 |
| 8.3 | CE | A00003 (no CE link) | direct URL | 403 |
| 8.4 | Patient | another Patient's appt | direct URL | 403 |
| 8.5 | Patient | non-existent GUID | direct URL | 404 (or 403?) |
| 8.6 | Anonymous (no auth) | any appt | direct URL | redirect to login |
| 8.7 | DA | List API filter bypass | `GET /api/app/appointments?identityUserId=<patient>` | filtered to DA-scope, not respecting query param |
| 8.8 | Cross-tenant probe | Tenant A user, Tenant B appt | direct URL | 404 (tenant filter strips) — Phase 1A is single-tenant; skip |

## Execution plan

Sequential execution, one phase at a time. After each phase:
- Commit findings + screenshots
- Update this plan file with "Status: VERIFIED" / "Status: BUG-NNN filed" annotation per scenario
- Brief Adrian on findings before starting next phase

Total scope: ~90 scenarios. Budget estimate ~6-8 hours of testing time (real-inbox checks add latency between scenarios).

## Verification artefact

Per-phase log captured in `tests/screenshots/2026-05-14-canonical/hardening/{phase}/` with one screenshot per scenario (where relevant) + this plan's status table updated.

## Out of scope (defer to later passes)

- Workflow G (Supervisor signature + bill marking)
- Workflow H (IT Admin host-scope tour)
- Workflow I (Doctor Management full surface)
- Workflow F (anonymous verification-code upload) -- folded into Phase 7.11/7.12 but the rate-limit detail bigger scope
- Multi-tenant (Phase 1A is single-tenant)
- Browser cross-cutting concerns (i18n, RTL, accessibility, dark mode)
- Performance / load testing
- HIPAA log-redaction audit (separate sweep)

## Approval

Approved by Adrian 2026-05-14. Open questions answered in "Approval directives" above. Execution begins immediately with Prep 0 → Round 1 → Round 2.
