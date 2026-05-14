# Userflow testing findings — index

Each entry below links to a per-bug file in `bugs/`. One bug per file is the canonical structure; this index is the entry point.

**Session:** main-worktree userflow testing per `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md`.

**Stack under test:** `W:\patient-portal\main` on branch `main`. Running on **canonical ports** (4200 / 44368 / 44327 / 1434 / ...). The fix session at `C:\src\patient-portal\replicate-old-app` runs serially after each PR cycle — see [[OBS-9]] for the abandoned parallel-stack approach.

**Ticket template:** `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md` Part 11. Each bug file's frontmatter mirrors that template.

---

## Bugs

| ID | Severity | Status | Title |
| --- | --- | --- | --- |
| [BUG-001](bugs/BUG-001-user-enumeration.md) | high | fixed PR #197 | Register form leaks user enumeration |
| [BUG-002](bugs/BUG-002-native-alert.md) | medium | fixed PR #197 | Register-error uses native window.alert() |
| [BUG-003](bugs/BUG-003-403-vs-400.md) | medium | fixed PR #197 | Register endpoint returns HTTP 403 for duplicate email |
| [BUG-004](bugs/BUG-004-patient-preselected.md) | low | fixed PR #197 | User Type pre-selects Patient |
| [BUG-005](bugs/BUG-005-signup-enabled-empty.md) | low | fixed PR #197 | Sign Up button enabled before form valid |
| [BUG-006](bugs/BUG-006-verify-email-url.md) | blocker | fixed PR #197 | Email-verification SPA hits wrong endpoint |
| [BUG-007](bugs/BUG-007-lookup-select-cd.md) | blocker | fixed PR #198 | appointment-add dropdowns render empty (lookup-select CD) |
| [BUG-008](bugs/BUG-008-put-me-concurrency.md) | medium | needs-rehydration | PUT /me concurrency stamp on submit retry |
| [BUG-009](bugs/BUG-009-leadtime-internal-error.md) | medium | needs-rehydration | "internal error" for BookingDateInsideLeadTime |
| [BUG-010](bugs/BUG-010-smtp-silent-fail.md) | medium | needs-rehydration | Synthetic-user SMTP silently fails |
| [BUG-011](bugs/BUG-011-reset-password-spa-fallthrough.md) | high | needs-rehydration | Password-reset link falls through to OAuth |
| [BUG-012](bugs/BUG-012-firmname-required.md) | medium | open | AA/DA Firm Name `required` attribute missing |
| [BUG-013](bugs/BUG-013-cors-confirmuser.md) | high | open | /Account/ConfirmUser Verify button blocked (CORS + antiforgery) |
| [BUG-014](bugs/BUG-014-hardcoded-email-urls.md) | medium | open | Hardcoded SPA/AuthServer URLs in email templates |
| [BUG-015](bugs/BUG-015-dynamic-env-unused.md) | medium | open | dynamic-env.json never read by SPA |
| [BUG-016](bugs/BUG-016-openiddict-subdomain.md) | medium | open | OpenIddict RedirectUris missing subdomain wildcards |
| [BUG-018](bugs/BUG-018-smtp-misleading-error.md) | medium | open | SMTP rate-limit logged as misleading "Configure ACS credentials" |
| [BUG-020](bugs/BUG-020-smtp-password-decrypt-noise.md) | medium | open | `Abp.Mailing.Smtp.Password` decrypt round-trip throws on plaintext config |

## Seed gotchas

| ID | Status | Title |
| --- | --- | --- |
| [SEED-1](bugs/SEED-1-software-three-six-auto-seeded.md) | fixed PR #197 | SoftwareThree/Four/Five/Six auto-seeded |
| [SEED-2](bugs/SEED-2-demo-doctor-seed-missing.md) | not-implemented | DemoDoctor seed contributor missing |

## Observations

| ID | Status | Title |
| --- | --- | --- |
| [OBS-1](bugs/OBS-1-register-form-field-inventory.md) | open | Register form field inventory (OLD vs NEW) |
| [OBS-2](bugs/OBS-2-stub.md) | stub | (needs rehydration) |
| [OBS-3](bugs/OBS-3-stub.md) | stub | (needs rehydration) |
| [OBS-4](bugs/OBS-4-stub.md) | stub | (needs rehydration) — CE page firm-name visibility |
| [OBS-5](bugs/OBS-5-stub.md) | stub | (needs rehydration) |
| [OBS-6](bugs/OBS-6-stub.md) | stub | (needs rehydration) |
| [OBS-7](bugs/OBS-7-stub.md) | stub | (needs rehydration) |
| [OBS-8](bugs/OBS-8-firmname-aa-da-only.md) | open | Firm Name AA/DA-only visibility matrix |
| [OBS-9](bugs/OBS-9-port-pinning-env.md) | superseded | Two-stack port-pinning attempt |
| [OBS-10](bugs/OBS-10-appointment-types-vs-plan.md) | open | Appointment types seeded vs plan expectation (Re-Eval/Consultation missing) |
| [OBS-11](bugs/OBS-11-base64-format-exception-logs.md) | open | Repeated Base-64 FormatException log noise during packet generation |
| [OBS-12](bugs/OBS-12-reschedule-cancel-ui-gap.md) | open | Reschedule + Cancellation UI not built (gated on W3) |
| [OBS-13](bugs/OBS-13-smtp-host-is-m365-reseller.md) | open | `securemailprotocol.com` is an M365 reseller; inherits EXO rate limits |
| [OBS-14](bugs/OBS-14-auth-vs-api-email-split.md) | open | AuthServer handles auth-emails; API host handles appointment-emails (architectural split) |

---

## Already verified end-to-end (do not retest)

| Flow | Outcome |
| --- | --- |
| Patient register → verify → login | PR #197 fixes confirmed live |
| Patient self-book happy path | A00001 (AME) — doc uploaded |
| Clinic Staff approves Patient-booked appointment | A00002 approved by `staff@falkinstein.test` |
| Packet auto-generation on approval | Patient DOCX 457 KB → PDF 530 KB + Doctor DOCX 1.17 MB → PDF 582 KB via Gotenberg |
| Packet-attached email | PatientPacket email with PDF attachment delivered to real Gmail inbox |
| Patient ad-hoc document upload | 66 KB PNG uploaded to A00001 |
| Patient forgot-password (API path) | API-direct call worked; UI link broken ([[BUG-011]]) |
| AA register/verify/login | SoftwareFour@gesco.com |
| AA book on behalf of fresh patient | A00003 with AppointmentApplicantAttorneys link |
| DA register/verify | SoftwareFive@gesco.com |
| CE register/verify | SoftwareSix@gesco.com |

**Note 2026-05-14:** the test DB was wiped during the port-pinning attempt ([[OBS-9]]). The above flows must be re-walked against the canonical-port stack after the BUG-012..016 fixes land — confirmation that the original fixes (PR #197 + #198) hold up under a fresh boot, plus verification of the new fixes.

---

## Conventions

- File names: `BUG-NNN-short-slug.md`, `SEED-N-slug.md`, `OBS-N-slug.md`.
- Each file starts with YAML frontmatter (id, title, severity, status, found, flow, component).
- Cross-links use the slug syntax `[[BUG-NNN]]` so future migration to a wiki/Obsidian-style tool is friction-free.
- Stub files exist for the entries referenced in earlier conversation context but lacking full content. Rehydrate when re-encountered.
