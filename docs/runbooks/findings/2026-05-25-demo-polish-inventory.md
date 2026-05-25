---
title: Demo polish inventory -- label / title / message / visibility findings
date: 2026-05-25
status: triaged
audience: Adrian (presenter)
related: 2026-05-25-tuesday-demo-script.md, 2026-05-24-tuesday-demo-prep-handoff.md
---

## Triage outcome (2026-05-25)

Adrian reviewed the must-fix list after live testing + external pattern
research. Decision:

- **F4-01 (SSN visibility per role):** approved -- implement.
- **F2-01 (dashboard placeholder cards):** declined -- close.
- **F3-01 (approve modal subtitle):** declined -- close.
- **F3-03 (Responsible User label):** declined -- close.

Nice-to-have and defer items remain closed.

# Demo polish inventory

Walk of the 5 Tuesday demo flows + SSN visibility audit. Each finding
records:

- **Where:** file:line and which demo step surfaces it.
- **Current:** what the user sees today (verbatim).
- **Why change:** justification (parity break, role hygiene, ambiguity,
  typo).
- **Severity:** must-fix-for-demo / nice-to-have / defer.

Triage happens after inventory completes (task #63).

---

## Flow 1 -- New-user registration (AuthServer/Account/Register)

The registration form is ABP's bundled Razor page. The Firm Name
conditional logic is layered via `wwwroot/global-scripts.js`.

### F1-01 -- (none surfaced) -- form renders ABP defaults

ABP's Register page labels (Email, Username, Password, Name, Surname,
"User type" dropdown, Firm Name when applicable) are framework-owned
and read clean. Nothing user-facing to flag here for the demo.

**Severity:** no-op.

---

## Flow 2 -- Dashboard + Appointments list

`angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.html`.

### F2-01 -- 8 placeholder dashboard cards always read 0

**Where:** tenant-dashboard.component.html:67-160 (8 cards marked
`placeholder-card`).

**Current:** Billed This Month, No-Show This Month, Rescheduled This
Month, Cancelled This Week, Checked In Today, Checked Out Today, Total
Doctors, Total Patients -- all show literal `0` with no caption.

**Why change:** In a demo, a wall of `0`s reads as broken software, not
"future feature." The Pending Change Requests card already carries
`(populated when W3 ships)` as a caption -- the other 7 should follow
the same convention so the audience knows they're roadmap, not bugs.

**Severity:** must-fix-for-demo (visual).

### F2-02 -- "Approaching Legal Deadline" subtitle uses uncommon citation

**Where:** tenant-dashboard.component.html:62.

**Current:** Subtitle reads `CCR Sec. 31.5 / 60 days`.

**Why change:** Audience may not know CCR (California Code of
Regulations). Either spell it out or move the cite to a tooltip.

**Severity:** nice-to-have.

---

## Flow 3 -- Approve a pending appointment

`angular/src/app/appointments/appointment/components/approve-confirmation-modal.component.html`.

### F3-01 -- Approve modal subtitle reads like filler

**Where:** approve-confirmation-modal.component.html:5.

**Current:** `<h6>Please approve an appointment request from here.</h6>`

**Why change:** "from here" adds nothing. Title above already says
"Approve appointment request." Subtitle should either state the
consequence ("This will notify the patient and generate the document
packet.") or be removed.

**Severity:** must-fix-for-demo (Flow 3 is the headline approval demo).

### F3-02 -- Approve modal: literal English not localized

**Where:** approve-confirmation-modal.component.html:5, 13, 21, 34, 39,
46, 53.

**Current:** "Responsible User", "Select", "Loading...", "Any
comments?", placeholder "Please enter comments regarding this
appointment", "Close", "Approve" -- all hard-coded English. Compare
with reject-appointment-modal.component.html which uses
`'::Appointment:Modal:RejectTitle' | abpLocalization` consistently.

**Why change:** Consistency with the reject modal + future-proof for
i18n. Reject modal is the right pattern.

**Severity:** nice-to-have (functional in English).

### F3-03 -- Approve label "Responsible User" is opaque

**Where:** approve-confirmation-modal.component.html:13.

**Current:** `Responsible User *` with no helper text.

**Why change:** Adjacent OLD parity flows refer to this concept as
"Primary Doctor" or "Assigned Provider" depending on context. The
audience won't infer that "Responsible User" means "the doctor the
appointment is approved against." Either rename to "Doctor" or add
helper text "Doctor who will see the patient."

**Severity:** must-fix-for-demo (Flow 3 is the headline).

---

## Flow 4 -- Document upload + packet regenerate (BUG-036 demo)

`angular/src/app/appointments/appointment/components/appointment-view.component.html`.

### F4-01 -- SSN field is blanket-redacted for everyone, no role override, no reveal

**Where:**

- `angular/src/app/appointments/sections/appointment-add-patient-demographics.component.html:134`
- `angular/src/app/appointments/appointment/components/appointment-view.component.html:306`
- `angular/src/app/patients/patient/components/patient-detail.component.html:151`
- `angular/src/app/patients/patient/components/patient-profile.component.html:169`
- CSS: `angular/src/styles.scss:15` (`.app-ssn-redacted` applies
  `-webkit-text-security: disc`).

**Current:** All 4 SSN inputs have `class="form-control app-ssn-redacted"`
unconditionally. CSS forces every character to render as a disc
regardless of who is viewing. There is no reveal toggle.

**Why change:** Three competing access patterns, all currently broken:

1. **Patient entering their own SSN at booking:** cannot see what they
   typed to verify accuracy -> typos go undetected, packet later fails
   verification.
2. **Clinic staff confirming the SSN on intake:** cannot read the SSN
   on screen to compare against the patient's physical ID.
3. **External attorney viewer:** SHOULD see the SSN redacted (currently
   correct, but only by accident -- the same blanket rule that breaks
   #1 and #2 happens to be right for #3).

The right model is role-conditional:

- Default: redacted with a Show/Hide toggle.
- Internal staff (Clinic Staff, Staff Supervisor, IT Admin): visible by
  default, optional hide toggle.
- Patient viewing their own row: visible by default in the form they're
  filling out.

**Severity:** must-fix-for-demo (the user named this exact issue).

### F4-02 -- "Unit #" label is plain English instead of localized

**Where:**

- `appointment-add-patient-demographics.component.html:148`
- `appointment-view.component.html:325`

**Current:** `<label>Unit #</label>` hard-coded. Every adjacent label
goes through `abpLocalization`.

**Why change:** Inconsistent. Should be `{{ '::Unit' | abpLocalization }}`
with a key added to `en.json`.

**Severity:** nice-to-have.

### F4-03 -- Interpreter Yes/No labels not localized

**Where:** appointment-add-patient-demographics.component.html:203, 213.

**Current:** literal `Yes` / `No`. Same in claim-information modal
(:119, :129).

**Why change:** Consistency only. Functionally fine.

**Severity:** defer.

---

## Flow 5 -- Invite an external user

`angular/src/app/external-users/components/invite-external-user.component.html`.

### F5-01 -- "Back to home" CTA is misleading

**Where:** invite-external-user.component.html:5.

**Current:** Top-right secondary button says `Back to home`. After
clicking it navigates to `/` -- which for a Staff Supervisor lands on
the Dashboard, not a "home" page.

**Why change:** Either rename to `Back to dashboard` (accurate for the
roles that can reach this page) or `Cancel` if it's intended as a form
escape hatch.

**Severity:** nice-to-have.

### F5-02 -- (positive finding) form is otherwise solid

Email + Role labels are clear, helper text explains the internal-vs-
external distinction, success card is unambiguous. Demo this flow as-is.

---

## Cross-flow -- Claim Information modal (Flow optional, but shows on booking)

`angular/src/app/appointments/sections/appointment-add-claim-information.component.html`.

### FC-01 -- Claim Examiner placeholders are uppercase noise

**Where:** :375, :391, :407, :422, :437, :448, :463.

**Current:** `placeholder="NAME"`, `placeholder="EMAIL"`,
`placeholder="PHONE NUMBER"`, etc. -- all-caps single words.

**Why change:** All-caps placeholders are 1990s tax-form aesthetic and
clash with the rest of the form's sentence-case labels. Either drop the
placeholders (the labels already say "Name *", "Email *", etc.) or
lowercase them.

**Severity:** nice-to-have (only surfaces if Flow optional-1 booking
demo runs).

### FC-02 -- "Include" toggle label is ambiguous

**Where:** :235, :357 (Insurance, Claim Examiner sub-sections).

**Current:** Each sub-section's header has a checkbox labeled
`Include`. No tooltip; no helper text.

**Why change:** "Include in what?" Should be `Include insurance details`
/ `Include claim examiner details` so the audience reads the toggle
correctly.

**Severity:** nice-to-have.

---

## Cross-flow -- Branding / app title

### FB-01 -- (verified clean) "Appointment Portal" parity rename is live

Tab title + AuthServer + SPA top-nav all read "Appointment Portal" per
last night's hand-off verification. No action.

---

## Summary table (severities)

| ID | Title | Severity |
|---|---|---|
| F2-01 | 8 dashboard placeholder cards read 0 with no caption | must-fix |
| F3-01 | Approve modal subtitle ("from here") is filler | must-fix |
| F3-03 | Approve modal "Responsible User" label is opaque | must-fix |
| F4-01 | SSN blanket-redacted, no role override, no reveal | must-fix |
| F2-02 | "CCR Sec. 31.5" subtitle is jargon | nice-to-have |
| F3-02 | Approve modal not localized (vs reject is) | nice-to-have |
| F4-02 | "Unit #" not localized | nice-to-have |
| F5-01 | "Back to home" is misleading | nice-to-have |
| FC-01 | Claim Examiner placeholders are SHOUTING | nice-to-have |
| FC-02 | "Include" toggles need helper text | nice-to-have |
| F1-01 | Registration page (no findings) | no-op |
| F4-03 | Interpreter Yes/No not localized | defer |
| FB-01 | Branding rename (verified clean) | no-op |

4 must-fix, 6 nice-to-have, 1 defer, 2 no-op.
