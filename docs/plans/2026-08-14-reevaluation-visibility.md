---
feature: Show which appointments are re-evaluations
date: 2026-08-14
status: draft
base-branch: main
related-issues: []
---

# Item 4 -- Re-evaluation visibility

## Goal

Make it visible everywhere an appointment is seen that it is a re-evaluation rather than a first
evaluation, and let staff filter the list by that distinction.

## Context & decisions

`Appointment.EvaluationKind` is already a persisted column, already written correctly on create
(`EvaluationKindPolicy.FromLifecycleFlow`), already carried across a reschedule
(`Approval.cs:531`), and already sent to the Case Tracker as `EVAL` / `RE_EVAL`. It is exposed
**nowhere** to the client: no Application.Contracts DTO carries it, so it is not in the generated
proxy, and the Angular app never references it. The browser genuinely cannot know.

The wizard already distinguishes the two while booking -- "Request a Re-evaluation",
"Follow-up evaluation" (`wizard-copy.util.ts`). The distinction vanishes on submit.

Resolved decisions:

1. **Decision: expose it on the read DTOs only, never on `AppointmentCreateDto`**, because the
   server derives the kind from the lifecycle flow; letting a client declare itself a re-eval
   would bypass the Approved-source gate that governs re-evaluations.
2. **Decision: a badge beside the appointment type**, so a row reads "PQME [Re-evaluation]".
   Satisfies both halves of the requirement -- type stays clear AND the kind is called out --
   without adding a column to an already-wide list.
3. **Decision: surfaces are the internal list, both detail views, the staff schedule calendar
   and notification emails.** Packets and generated documents are excluded, because they are the
   heaviest templates to change and are shared with an external system.
4. **Decision: add a filter to the existing list filters**, because staff who handle follow-ups
   differently need to work with them as a set, not just recognise them one at a time.

## All needed context

| Fact                                            | Anchor                                                                                 |
| ----------------------------------------------- | -------------------------------------------------------------------------------------- |
| Enum, `Evaluation = 1` / `ReEvaluation = 2`     | `Domain.Shared/Appointments/EvaluationKind.cs`                                         |
| Persisted property, defaults `Evaluation`       | `Appointment.cs:177`                                                                   |
| Write policy                                    | `EvaluationKindPolicy.cs:19`, called at `AppointmentsAppService.cs:906`                |
| Carried across a reschedule                     | `Approval.cs:531`                                                                      |
| Wire mapping already in place                   | `IntakePayloadBuilder.cs:109` via `EvaluationKindWire`                                 |
| Read DTO                                        | `Application.Contracts/Appointments/AppointmentDto.cs`                                 |
| Nav-properties DTO                              | `Application.Contracts/Appointments/AppointmentWithNavigationPropertiesDto.cs`         |
| List template renders `a.appointmentType?.name` | `internal-appointments.component.html`                                                 |
| List component                                  | `internal-appointments.component.ts`                                                   |
| Detail views                                    | `internal-appointment-detail.component.ts`, `external-appointment-detail.component.ts` |
| Shared view template                            | `appointment-view.component.html`                                                      |
| Pill rendering precedent                        | `StatusPillPolicy.cs` and the status pill in the list template                         |

Gotchas:

- `angular/src/app/proxy/` is GENERATED. Never hand-edit. Regenerate with `abp generate-proxy`,
  which is a **dotnet global tool** at `~/.dotnet/tools/abp` -- not an npm package.
- The list filter needs a parameter threaded through the app service AND the repository query;
  it is not a client-side filter over a loaded page.
- Notification email templates are seeded per tenant and have caused deploy friction before.
  A template change needs the seeder to re-run against every office database.

## Tasks

### T1 -- expose the field on the read DTOs

approach: code

MODIFY `Application.Contracts/Appointments/AppointmentDto.cs`: add
`public EvaluationKind EvaluationKind { get; set; }`. Verify the object mapping picks it up
(Riok.Mapperly). Do NOT add it to `AppointmentCreateDto`.

acceptance (EARS): WHEN an appointment is read through the API, THE SYSTEM SHALL include its
evaluation kind.

### T2 -- regenerate the proxy

approach: code

RUN `abp generate-proxy` (the dotnet global tool) so `angular/src/app/proxy` picks up the new
field and the enum. Commit the generated output unmodified.

acceptance (EARS): WHEN the Angular app reads an appointment DTO, THE SYSTEM SHALL expose the
evaluation kind without any hand-edited proxy file.

### T3 -- the badge

approach: test-after

CREATE a small shared badge (component or template fragment) that renders only when the kind is
`ReEvaluation`. MODIFY `internal-appointments.component.html` to render it beside
`a.appointmentType?.name`, and both detail views plus `appointment-view.component.html` to render
it beside the type.

pattern: the existing status pill in the list template.

acceptance (EARS):

- WHERE an appointment is a re-evaluation, THE SYSTEM SHALL display a re-evaluation badge beside
  its appointment type in the internal list and in both detail views.
- WHERE an appointment is a first evaluation, THE SYSTEM SHALL display no badge.

### T4 -- staff schedule calendar

approach: test-after

MODIFY the staff schedule calendar so a re-evaluation is visually distinguishable. Space is
constrained, so a marker plus a legend entry rather than full text.

acceptance (EARS): WHERE a calendar event is a re-evaluation, THE SYSTEM SHALL distinguish it
from a first evaluation, and THE SYSTEM SHALL explain that distinction in a legend.

### T5 -- notification emails

approach: test-after

MODIFY the appointment notification templates so the body states the appointment is a
re-evaluation when it is. Confirm the seeder re-runs across office databases.

acceptance (EARS): WHEN a notification is sent for a re-evaluation, THE SYSTEM SHALL say so in
the message body.

### T6 -- list filter

approach: tdd

MODIFY the appointment list input DTO, app service and repository query to accept an optional
evaluation-kind filter. MODIFY the list UI to offer it alongside the existing filters.

pattern: the existing typed `appointmentStatus` filter on the repository query, used by
`GetPendingCountAsync` (`AppointmentsAppService.cs:~720`).

acceptance (EARS):

- WHEN the filter is set to re-evaluations, THE SYSTEM SHALL return only re-evaluations.
- WHEN the filter is unset, THE SYSTEM SHALL return both kinds.

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
cd /c/src/patient-portal/main && export CHROME_BIN=<chrome path> && npx ng test --watch=false --browsers=ChromeHeadless
```

Run the FULL Angular suite here, not a scoped one: a spec that pins an exact list column set or
an exact email body will break on this change, and those live outside the appointments folder.

## Risk / rollback

Blast radius: read paths and templates only -- no schema change, no write-path change. The email
template edit is the riskiest part because templates are seeded per office.

Rollback: revert the PR, then re-run the seeder to restore the previous template bodies.
