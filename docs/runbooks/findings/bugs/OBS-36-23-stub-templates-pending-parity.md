---
id: OBS-36
title: 23 of 64 active DB-managed notification templates are TODO stubs with placeholder subject + body
severity: observation
status: open
found: 2026-05-21 hardening HRD-P10.1
flow: notification-templates
component: AppNotificationTemplates DB rows + (per-template phases) parity feature work
---

# OBS-36 - 23 stub templates pending parity

> 2026-05-24: renamed from `OBS-27-23-stub-templates-pending-parity.md` to free `OBS-27` for the invite-email-empty-greeting observation that main concurrently filed during the hardening run.

## Symptom

Phase 10 rubric review of all 64 active rows in `AppNotificationTemplates` (`IsActive=1 AND IsDeleted=0`) against the 5-item rubric in HARDENING-TEST-SUITE.md Phase 10 returns:

- 9 templates PASS all 5 items.
- 32 templates have exactly 1 rubric failure (acceptable per suite criterion "0 templates fail >= 2 rubric items").
- **23 templates have 2+ rubric failures** (suite threshold for filing).

All 23 failing templates share the same failure pattern:

1. `subj-leak`: subject value is literally `[<TemplateCode>] -- TODO: parity-correct subject` -- the bracketed template code is exposed in the user-facing Subject line, and the phrase "TODO: parity-correct subject" makes the placeholder nature visible.
2. `stub-body`: BodyEmail value is literally `<p>Stub body for <TemplateCode>. Per-feature phases will replace with parity-correct content.</p>`.

A few additionally fail item 1's length check (`subj-too-long`) because `[<longer code name>] -- TODO: parity-correct subject` exceeds 60 chars.

Full list of 23 stub templates:

| # | TemplateCode | Length issue? |
|---|---|---|
| 1 | AccessorAppointmentBooked | no |
| 2 | AddInternalUser | no |
| 3 | AppointmentApproved | no |
| 4 | AppointmentApprovedStakeholderEmails | yes (>60) |
| 5 | AppointmentBooked | no |
| 6 | AppointmentCancelledByAdmin | yes |
| 7 | AppointmentCancelledDueDate | yes |
| 8 | AppointmentChangeLogs | no |
| 9 | AppointmentDueDate | no |
| 10 | AppointmentDueDateUploadDocumentLeft | yes |
| 11 | AppointmentPendingNextDay | no |
| 12 | AppointmentRejected | no |
| 13 | AppointmentRescheduleRequestByAdmin | yes |
| 14 | PatientAppointmentCancellationApproved | yes |
| 15 | PatientAppointmentRescheduleReq | yes |
| 16 | PatientAppointmentRescheduleReqAdmin | yes |
| 17 | PatientAppointmentRescheduleReqApproved | yes |
| 18 | PatientAppointmentRescheduleReqRejected | yes |
| 19 | PatientDocumentAcceptedAttachment | yes |
| 20 | RejectedJointDeclarationDocument | yes |
| 21 | RejectedPackageDocument | no |
| 22 | SubmitQuery | no |
| 23 | UserQuery | no |

## Context

This is consistent with the project's stated intent (`CLAUDE.md` PRIMARY MISSION at `W:\patient-portal\replicate-old-app\`):

> "Port the correct, ground-truth behavior of the legacy single-tenant Patient Portal at `P:\PatientPortalOld` into this codebase, using the NEW stack."

The stub bodies' embedded text "Per-feature phases will replace with parity-correct content" indicates the stubs were seeded as scaffolding awaiting parity feature work. The 9 templates that already pass and the 32 with one rubric failure are presumably from feature areas where parity has already been completed (registration, password reset, appointment-approval external + internal, reschedule request, cancel request, document upload/accept/reject, joint agreement, check-in/out, no-show, etc).

The 23 stubs likely correspond to feature areas in the OLD app (`P:\PatientPortalOld`) that have not yet had their parity audit doc written or parity implementation completed in `docs/parity/`.

## Functional impact

If any of these 23 templates is referenced by a Hangfire job in production:
- Subject in inbox: `[AccessorAppointmentBooked] -- TODO: parity-correct subject` -- looks broken to the recipient.
- Body text: `Stub body for AccessorAppointmentBooked. Per-feature phases will replace with parity-correct content.` -- clearly placeholder, useless.

Whether any of these templates fires today depends on which code paths in the running app reference them. The suite's runtime probes (Phase 5/6) confirmed that the templates actually USED by the bookings + approvals flow (`AppointmentRequestedRegistered`, `PatientAppointmentApprovedExt`, `AppointmentDocumentAddWithAttachment`, etc.) are all in the PASS or 1-failure bucket -- so the stub templates are not surfaced via the suite's happy path.

Risk: a code change that switches a Hangfire job to reference one of these stub TemplateCodes would silently send placeholder email to real recipients.

## Related

- `template-review-2026-05-21.md` in the same `docs/runbooks/findings/` directory -- full rubric table with all 64 rows.
- Suite Phase 10 rubric (HARDENING-TEST-SUITE.md line 841-857) -- the 5-item rubric source.
- CLAUDE.md PRIMARY MISSION at `W:\patient-portal\replicate-old-app\` -- the parity goal driving these stubs.
- Each of the 23 codes maps to a feature area; a parity audit doc under `docs/parity/` (currently 18 audit docs exist) should be written for each before the stub is replaced.
