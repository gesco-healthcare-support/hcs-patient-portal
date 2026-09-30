# Appointment Booking Flow

> Purpose: Documents the booking wizard: its steps, booking modes, slot picking, drafts, and the single
> submit call. Audience: frontend developers.

[Home](../index.md) > [Frontend](./) > Appointment Booking Flow

## Overview

Every booking -- by a patient, an attorney, a claim examiner or office staff -- goes through one page:
**`/appointments/request`**, which loads `AppointmentWizardComponent`
(`angular/src/app/appointments/wizard/appointment-wizard.component.ts`). External users reach a chrome-less
copy of the route and staff reach a copy inside the internal shell; the component is the same (see
[Routing & Navigation](ROUTING-AND-NAVIGATION.md)). `/appointments/add` no longer exists.

The wizard is split across two classes:

| Class | Holds |
|-------|-------|
| `AppointmentAddComponent` (`appointments/appointment-add.component.ts`) | The reactive form, every cascade and lookup, the booking modes, the pre-submit checks and the submit call. No route loads it directly any more; it survives as the base class. |
| `AppointmentWizardComponent` (extends it) | The stepper, the header and footer, per-step validation, draft save and resume, the leave prompt, and the review step. |

The section components under `appointments/sections/` render controls only: they receive the parent's
`FormGroup` (or a slice of its state) as inputs, and no form building or HTTP calls live in them.

## Booking modes

The mode comes from the query string (`AppointmentAddComponent` constructor, `shared/booking-mode.ts`):

| URL | Mode | What happens |
|-----|------|--------------|
| `?type=1` (or no type) | `new` | A fresh booking. The only mode with draft save and resume. |
| `?type=2` | `reval` | Re-evaluation: the booker looks up a prior approved appointment by confirmation number, and the form is prefilled from it. |
| `?type=3` (optionally `&source=<confirmation number>`) | `reBook` | Book again from an earlier appointment; with `source` it loads automatically. |
| `?mode=rerequest&source=<confirmation number>` | `reRequest` | Re-request a rejected appointment, prefilled from it. |

The three special modes refuse to submit until their source appointment has loaded. At submit the mode is
sent as `BookingSubmitMode` (`Create`, `ReSubmit`, `Reval`, `ReBook`), and the server re-checks the source.

## Steps

`STEPS` in the wizard, in order:

| # | Key | Renders |
|---|-----|---------|
| 1 | `schedule` | `AppointmentAddScheduleComponent`: appointment type, location, panel number, and the date and time picker; the source lookup for the special modes |
| 2 | `patient` | `AppointmentAddPatientDemographicsComponent` and `AppointmentAddEmployerDetailsComponent` |
| 3 | `applicant` | `AppointmentAddAttorneySectionComponent` for the applicant attorney |
| 4 | `defense` | `AppointmentAddAttorneySectionComponent` for the defense attorney |
| 5 | `insurance` | `AppointmentAddClaimPartiesSectionComponent` with `only="insurance"` |
| 6 | `examiner` | `AppointmentAddClaimPartiesSectionComponent` with `only="examiner"` |
| 7 | `claim` | `AppointmentAddClaimInformationComponent`: the injury (Claim Information) list and its modal |
| 8 | `docs` | `AppointmentAddDocumentsComponent`: documents staged for upload |
| 9 | `review` | A read-only summary of every step, plus `AppointmentAddAuthorizedUsersComponent` (when shown) and `AppointmentAddCustomFieldsComponent` |

**Continue** validates only the current step's controls (`WIZARD_STEP_CONTROLS`, via
`wizard/step-errors.util.ts`) and lists what is missing at the top of the step. Which fields are required, and
where that is enforced, is in [Appointment Required Fields](../appointment-required-fields.md).

## Who fills in the patient

| Booker | Patient step |
|--------|--------------|
| Patient | Their own profile loads from `/api/app/patients/me`; the email is read-only. |
| Attorney, claim examiner or staff | Search for an existing patient by email (a typeahead over `/api/app/appointments/patient-lookup`), or enter a new one. For an external booker the new patient's email is required only when there is no applicant attorney. |

An attorney booker's own attorney step is prefilled from their profile when it is still blank.

## Picking a slot

The date and time picker is `AvailabilityCalendarComponent` (`appointments/availability-calendar/`). Once a
location and an appointment type are chosen, it calls `GET /api/app/doctor-availabilities/lookup`, which returns
only slots that are open, still have room, and offer that type (see
[Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md)).

- Dates earlier than 3 days out, and later than 90 days out, cannot be picked (`leadDays`, `ceilingDays`).
- An external booker who picks a date more than 60 days out gets a notice to contact the office instead
  (`maxBookingDays`: 60 for external users, 90 for staff).
- These client rules only mirror the server. `BookingPolicyValidator` is authoritative and reads the office's
  `SystemParameter.AppointmentLeadTime`.

## Drafts

For a `new` booking, the wizard saves a server draft (`AppointmentDraftService`) when the booker presses
**Continue** and when they choose **Save** on the leave prompt. When the page opens it offers to resume the
booker's draft, and a successful booking discards it. Leaving a partly filled new booking triggers
`appointmentWizardCanDeactivateGuard`, which offers Save, Discard or Stay.

## Submit

`onSubmit()` in `AppointmentAddComponent`:

1. **Client checks**, each of which stops the submit with a message: the source appointment has loaded (special
   modes); a patient is chosen or the new patient's required fields are filled; the form is valid; at least one
   Claim Information entry exists; for a Panel QME marked as having a strike list, one staged document is marked
   as the strike list; every document labeled "Other" has a name. A second click while a submit is running is
   ignored.
2. **Address standardization** -- `standardizeAddressesBeforeSubmit()` offers USPS-standardized versions of the
   entered addresses; any provider error lets the submit continue.
3. **One call** -- `POST /api/app/appointments/submit` (`AppointmentsAppService.SubmitAsync`) with an
   `AppointmentSubmitDto` built in `appointments/shared/submit-payload.mapper.ts`. The server resolves or creates
   the patient, creates the appointment and writes every child group (employer, attorneys, insurance, claim
   examiner, injuries, authorized users, custom fields) in **one transaction** (`[UnitOfWork]`), so a failure
   leaves nothing behind.
4. **Documents** upload afterwards, one by one, because a file upload cannot join the database transaction. A
   failed upload is kept on the page for retry; the appointment already exists.
5. **Staff bookings are approved** straight away: `POST /api/app/appointment-approvals/{id}/approve`, tried at
   most twice, with a warning if it does not go through.
6. **Navigate** -- staff to `/appointments`, external users to `/`.

If the slot was taken or closed in the meantime, the server answers with
`CaseEvaluation:Appointment.BookingSlotFull`, `BookingSlotClosed` or `BookingSlotTypeMismatch`; the page shows
the message and clears the chosen time so the booker can pick again.

---

**Related Documentation:**

- [Application Services](../backend/APPLICATION-SERVICES.md)
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md)
- [Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md)
- [Appointment Required Fields](../appointment-required-fields.md)
