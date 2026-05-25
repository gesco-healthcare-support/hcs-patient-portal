---
feature: ssn-role-visibility
date: 2026-05-25
status: in-progress
base-branch: main
related-issues: []
related-finding: docs/runbooks/findings/2026-05-25-demo-polish-inventory.md (F4-01)
---

## Goal

Stop showing every viewer the full SSN. Internal staff and record-owners
see the full value; everyone else gets last-4 only.

## Context

Live-verified on 2026-05-25:

- `angular/src/styles.scss:15` applies `-webkit-text-security: disc` to
  every input carrying the `app-ssn-redacted` class. Applied in 4
  places (booking demographics, appointment view, patient detail,
  patient profile). Computed style confirmed via Playwright.
- `PatientDto.SocialSecurityNumber` is returned in full to every
  authenticated caller -- the redaction is purely client-side CSS,
  trivially defeated via dev tools.
- OLD app `P:\PatientPortalOld` shows SSN in plain text everywhere
  (no redaction at all). The current CSS redaction is a NEW-stack
  deviation introduced as a half-measure -- it neither matches OLD
  parity nor delivers real protection.

External pattern guidance (researched 2026-05-25):

- NIST SP 800-122: even last-4 SSN is PII; masking must be paired
  with role gates, not used alone.
- HIPAA Minimum Necessary: SSN access restricted to billing /
  registration roles; patients always see their own.
- USWDS SSN pattern: never obfuscate during entry -- users cannot
  verify what they typed.
- Bank of America / banking norm: default mask to last-4 for display.

Adrian directive 2026-05-25: "should not be visible generally,
internal roles need to see it to verify, users should see it so they
know what they typed."

## Approach

**Two-layer change, all real protection on the server side:**

1. **Server (real control):** A small `SsnVisibility` helper redacts
   the SSN to last-4 on the wire when the caller is an external role
   AND does not own the record. Applied at the AppService mapping
   boundary -- never trust the client.
2. **Client (cleanup):** Drop the `.app-ssn-redacted` CSS class
   everywhere. Once the server returns the right value, hiding it
   client-side is counter-productive (it hides the SSN from the very
   people authorized to see it: internal staff and record-owners).
   Keep the ngx-mask digit-grouping for visual structure -- that is
   not redaction, just formatting.

The redacted form on the wire uses the literal token
`XXX-XX-LAST4` where `LAST4` is the actual last four digits of the
stored value. Synthetic example for tests:
`XXX-XX-NNNN` (using the letter N as placeholder to satisfy the PHI
scanner -- real fixtures use synthetic numerics from the seed pool).

**Alternatives rejected:**

- *Client-only reveal toggle (eye icon)*: doesn't change what's on
  the wire; external attorneys still get full SSN over HTTP, the
  toggle is theater. Reject.
- *Strict OLD parity (no redaction at all)*: matches OLD verbatim,
  but OLD's plain-text-SSN was almost certainly an oversight, not a
  designed feature. Per CLAUDE.md "Clear bug -- fix it" rule, harden
  rather than replicate. Document in parity-flags.
- *Full HIPAA-grade access audit + per-field permissions*: out of
  scope for the Tuesday demo; reserve for a post-parity hardening
  pass.

## Tasks

- T1: Server-side SSN redaction helper + apply to read paths.
  - approach: tdd
  - files-touched:
    - new: src/HealthcareSupport.CaseEvaluation.Application/Patients/SsnVisibility.cs
    - new: test/HealthcareSupport.CaseEvaluation.Application.Tests/Patients/SsnVisibility_Tests.cs
    - src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs
    - src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs
    - (verify any other read path that returns PatientWithNavigationPropertiesDto)
  - acceptance:
    - Patient calling GetWithNavigationProperties on own record -> full SSN.
    - Clinic Staff / Staff Supervisor / IT Admin -> full SSN.
    - Applicant Attorney / Defense Attorney / Claim Examiner on
      someone else's record -> SSN returned as last-4 only with
      mask prefix.
    - Null / empty SSN -> returns unchanged.
    - Unit tests cover all 4 role buckets + null case.

- T2: Drop the SSN-redaction CSS class everywhere; delete the rule.
  - approach: code
  - files-touched:
    - angular/src/styles.scss (delete the rule at line 15)
    - angular/src/app/appointments/sections/appointment-add-patient-demographics.component.html:134
    - angular/src/app/appointments/appointment/components/appointment-view.component.html:306
    - angular/src/app/patients/patient/components/patient-detail.component.html:151
    - angular/src/app/patients/patient/components/patient-profile.component.html:169
  - acceptance:
    - `npx ng build --configuration development` passes.
    - SSN inputs render plain digits (still mask-formatted by ngx-mask).
    - Grep `app-ssn-redacted` returns zero matches.

- T3: Document the parity deviation.
  - approach: code
  - files-touched:
    - new: docs/parity/_parity-flags.md
  - acceptance:
    - File exists with one entry: SSN-role-redaction, OLD source
      cite, status `resolved` (this PR implements the hardening).

## Risk / Rollback

- Blast radius:
  - Server-side: any read path that returns `PatientDto` /
    `PatientWithNavigationPropertiesDto`. If T1 misses a path,
    external roles still see full SSN on that path. Mitigated by
    applying the redact-at-mapping pattern at every AppService method
    that returns these DTOs.
  - Client-side: dropping the redaction class removes visible
    obfuscation everywhere. Until T1 ships in the same PR, external
    attorneys briefly see plain SSN. So T1 + T2 must ship together;
    do not merge halfway.
- Rollback: revert the PR. The CSS rule is in styles.scss; the helper
  is a single class. Both are surgical edits.

## Verification

After all 3 tasks merge:

1. `docker compose up -d --build` (fresh stack).
2. Login as `patient1@gesco.com` -> open A00001 -> SSN field shows
   full digits (plain text, no CSS redaction).
3. Login as `appatty1@gesco.com` -> open A00001 -> SSN field shows
   the last-4-only form.
4. Login as `clistaff1@gesco.com` -> open A00001 -> SSN field shows
   full digits.
5. Network tab: confirm the API response for an external-attorney
   request returns the last-4 form, not the full value. Real defense
   in depth: the wire never carries what the role cannot see.
6. xUnit suite for SsnVisibility_Tests is green.
