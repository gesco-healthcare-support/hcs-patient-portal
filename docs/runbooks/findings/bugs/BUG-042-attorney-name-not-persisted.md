---
id: BUG-042
title: Attorney "Name" captured at booking is dropped on persist; appointment view shows attorney details only when the attorney is a registered IdentityUser
severity: high
status: open
found: 2026-05-27 (userflow audit; live-replicated as stafsuper1 + DB-verified)
flow: appointment-booking, appointment-view
component: angular/src/app/appointments/sections/appointment-add-attorney-section.component.html; src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs (UpsertApplicantAttorneyForAppointmentAsync, GetAppointmentApplicantAttorneyAsync, GetAppointmentDefenseAttorneyAsync); src/.../Domain/ApplicantAttorneys/ApplicantAttorney.cs + DefenseAttorneys/DefenseAttorney.cs
parity: regression -- OLD stored a single `attorneyName`; NEW dropped the column
---

# BUG-042 - Attorney name dropped on persist; view requires a registered IdentityUser

## Symptom

Booked A00001 (QME, Demo Clinic North) entering Applicant Attorney
"Aria Stone / appatty1@gesco.com / Stone & Associates" and Defense
Attorney "Dana Defense / defatty1@gesco.com / Shield Defense Group".
Later, viewing the appointment:

- **Applicant Attorney section renders completely blank** (First/Last/
  Email/Firm all empty) despite the data being entered at booking.
- **Defense Attorney section renders populated** -- but shows
  **"Gregory Stone"**, the name from defatty1's later self-registration,
  **not** the "Dana Defense" typed at booking.

Replicated live 2026-05-27 as `stafsuper1` viewing A00001 (screenshot
`aa-da-sections.png`). The asymmetry is: defatty1 registered (via the
invite flow); appatty1 never registered.

## Root cause (confirmed: code + DB + live)

Three compounding defects in how an attorney's name flows through the
system:

1. **Booking captures the name into the wrong control and only one
   half of it.** `appointment-add-attorney-section.component.html:24-30`
   renders a single field labelled **"Name *"** but binds it to
   `{prefix}FirstName`. There is no Last Name input. "Aria Stone" lands
   entirely in `applicantAttorneyFirstName`; `applicantAttorneyLastName`
   is never collected.

2. **Persist drops the name entirely.**
   `UpsertApplicantAttorneyForAppointmentAsync`
   (`AppointmentsAppService.cs` ~1064-1124) and its DA twin call
   `ApplicantAttorneyManager.CreateAsync(stateId, identityUserId,
   firmName, firmAddress, phoneNumber, webAddress, faxNumber, street,
   city, zipCode, concurrencyStamp, email)` -- there is **no name
   parameter**. The master tables confirm it: `AppApplicantAttorneys`
   and `AppDefenseAttorneys` have columns FirmName / FirmAddress /
   WebAddress / PhoneNumber / FaxNumber / Street / City / ZipCode /
   StateId / Email / IdentityUserId -- and **no FirstName / LastName /
   Name column at all** (`sys.columns` verified 2026-05-27).

3. **Display reads the name only from the linked IdentityUser, and
   returns null when there is none.**
   `GetAppointmentApplicantAttorneyAsync` (`AppointmentsAppService.cs:
   1033-1061`) and `GetAppointmentDefenseAttorneyAsync` (1250-1278) both
   begin with `if (item?.ApplicantAttorney == null || item?.IdentityUser
   == null) return null;` and then set `FirstName = u.Name`, `LastName =
   u.Surname`, `Email = u.Email` from the IdentityUser. So:
   - Unregistered attorney (link `IdentityUserId IS NULL`) -> endpoint
     returns null -> section blank, even though FirmName + Email ARE
     persisted on the master.
   - Registered attorney -> shows the **registered** identity's name,
     not the booking-typed name.

The Angular view's `bindApplicantAttorneyFromResponse`
(`appointment-view.component.ts:1098-1124`) mirrors this: it binds from
`appointmentApplicantAttorney.applicantAttorney` + `.identityUser`
(needs both), else falls back to `GET /{id}/applicant-attorney` (the
null-returning endpoint above).

### DB ground truth (2026-05-27)

```
AppAppointmentApplicantAttorneys: 2 rows, both IdentityUserId = NULL
AppAppointmentDefenseAttorneys:   2 rows, both IdentityUserId = 84B591AD-... (defatty1)
AppApplicantAttorneys master:     FirmName='Stone & Associates', Email='appatty1@gesco.com', IdentityUserId=NULL  (no name column)
AppDefenseAttorneys master:       FirmName='Shield Defense Group', Email='defatty1@gesco.com', IdentityUserId=set
```

## Parity regression (OLD did this correctly)

OLD captured the attorney name as a **single** `attorneyName` field
(`P:\PatientPortalOld\patientappointment-portal\src\app\components\
appointment-request\appointments\add\appointment-add.component.html:
676-677`, label "Name", `formControlName="attorneyName"`) and stored it
on the attorney record. NEW kept the single "Name" input but rewired it
to `FirstName`, dropped the backing column, and made display depend on
the IdentityUser. This is a regression, not a new design.

## Contrast: the sibling pattern is implemented correctly

`AppAppointmentClaimExaminers` (the per-injury Claim Examiner, also a
free-text person) stores **Name, Email, PhoneNumber, Fax, Street, City,
Zip, StateId as text columns** and renders fine regardless of
registration. The attorney is the lone "person" sub-entity that lacks a
name column. `AppAppointmentAccessors` is intentionally identity-only
(picked from existing users), so that one is consistent.

## Functional impact

- Booking-entered attorney identity is silently lost. For an
  appointment whose attorney has not registered (the common case at
  booking time), the appointment view, edit screen, and any
  attorney-facing packet/email show no attorney name.
- When the attorney later registers, the view shows their *registered*
  name, which may differ from what the booker entered -- confusing for
  staff reconciling records.
- AttyCE packet (Kind=3) recipient resolution and attorney-facing emails
  depend on this data; missing names degrade those documents.

## Recommended fix (high level -- see plan)

Decided 2026-05-27 (Adrian): **store the name as split FirstName +
LastName** (not a single field), display the stored name AND firm name
always, and source the displayed name from the stored attorney record
(not the IdentityUser) so there is no booked-vs-registered divergence.

1. Add `FirstName` + `LastName` columns to `ApplicantAttorney` +
   `DefenseAttorney` entities + consts + EF migration. (The
   `ApplicantAttorneyDetailsDto`/`DefenseAttorneyDetailsDto` already
   carry FirstName/LastName; the master `ApplicantAttorneyDto` + its
   Mapperly mapper need them added so the nav-properties read path can
   bind name without an IdentityUser.)
2. Persist: add `firstName`/`lastName` params to
   `ApplicantAttorneyManager.CreateAsync/UpdateAsync` (+ DA twin) and set
   them in `Upsert{Applicant,Defense}AttorneyForAppointmentAsync`. The
   SPA already SENDS `firstName`/`lastName`
   (`appointment-add.component.ts:1815-1816`); the server just drops them.
3. Capture in the booking form: add a **Last Name** input to
   `appointment-add-attorney-section.component.html` (today it is a
   single "Name *" bound to `{prefix}FirstName`); add `{prefix}LastName`
   to the required-suffix list in `attorney-section-validators.ts`.
4. Display: `GetAppointment{Applicant,Defense}AttorneyAsync` -- return
   `attorney.FirstName`/`LastName` (fall back to IdentityUser
   name/surname only when stored is null, for legacy rows); drop the
   `IdentityUser == null` early-return (return on `ApplicantAttorney ==
   null` only). Mirror in the view's `bindApplicantAttorneyFromResponse`
   so the nav read path prefers the stored name.

## Related

- [[OBS-32]] booker-aa-section-prefill-first-name-only -- same
  single-"Name"-to-FirstName mapping, observed at booking prefill.
- [[OBS-8]] firmname-aa-da-only.
- [[BUG-030]] / [[BUG-032]] -- same "field that should be set is
  NULL/dropped" family.
