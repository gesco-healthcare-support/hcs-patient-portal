[Home](../INDEX.md) > [Product Intent](./) > Appointment Employer Details

# Appointment Employer Details -- Intended Behavior

**Status:** draft -- Phase 2 T7, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** [UNKNOWN -- pending T7 interview]

> Captures INTENDED behaviour for `AppointmentEmployerDetail` records -- the patient's employer information associated with an appointment for a workers'-compensation IME. Every claim source-tagged; code is never cited as intent authority. Observations from code appear ONLY in Known Discrepancies.

## Purpose

`AppointmentEmployerDetail` carries the patient's employer information associated with a workers'-compensation IME appointment -- employer name, occupation, and (optionally) contact and address fields. Exactly one employer record per appointment. Captured on the booking form as part of the patient-side intake. Employer info is required on every booking; there is no "skip employer" path at MVP, even for retired, self-employed, or otherwise non-conventionally-employed patients (the relevant employer for the claim is still named). Whether the employer is a notification recipient depends on the case -- some cases make the employer a formal party, others do not. Once a request is submitted, the employer info becomes part of the legal record and is locked from edit except via the Gesco-side proper-process path. [Source: Adrian-confirmed 2026-04-24]

## Personas and goals

Cross-reference: see `appointments.md` for the booker personas. Employer-record-specific goals captured here.

### The patient and their booker

The patient (or whoever is booking on their behalf) provides the employer info as part of the booking form. The employer itself is not a portal user. [Source: inferred from current code; confirm during interview]

### Doctor's office and case parties

Read employer info from the appointment's data for case context. [UNKNOWN -- queued for Adrian: is the employer's information a relevant field on the all-parties notification emails, or is it just data the doctor's office reads internally?]

### The employer themselves

**Sometimes a party, sometimes not.** When the employer is the self-insured payer or otherwise directly involved in the workers'-comp claim, they are a formal notification recipient (the ex-parte pattern extends to them, same as any other case party). When a carrier or TPA handles the claim end-to-end and the employer is off the active case, they are just data on file for the record. The appointment itself needs to carry a per-booking flag indicating which case this is, and the notification logic branches on that flag. [Source: Adrian-confirmed 2026-04-24]

## Intended workflow

### Capture during booking

The employer information is entered on the booking form by whoever is booking (patient, applicant attorney, defense attorney, or claim examiner, per the booker list in `appointments.md`). It is captured once, at the time the appointment request is submitted. [Source: inferred from current code; confirm during interview]

### Edits post-submit

**Locked, same strict process as all other form data.** Once the appointment request is submitted -- and the all-parties notification email has fired -- the employer info entered on the form becomes part of the legal record. Any change after that requires a Gesco-side admin running the change through the proper process. Neither the booker nor the practice-side doctor's admin can self-edit. [Source: Adrian-confirmed 2026-04-24]

**This is a universal rule**, not an employer-specific one: any data captured on a submitted appointment form -- patient, attorneys, insurance + adjustor, employer, appointment type, location, etc. -- is locked at submit; all post-submit changes to any field require the Gesco-side proper-process path. The rule was implied in `patients.md` (patient-data post-submit changes) and now confirmed uniformly here. See the T4/T5 tension resolution note in `patients.md`. [Source: Adrian-confirmed 2026-04-24]

## Business rules and invariants

### Confirmed

- **Employer information is required on every booking, no exceptions.** The form does not allow submit without employer info. Patients who are retired, self-employed, or otherwise without a current conventional employer still name the employer relevant to the claim (typically the former or at-injury employer). EmployerName and Occupation are minimally required; other fields remain optional at MVP. [Source: Adrian-confirmed 2026-04-24]
- **Whether the employer is a notification recipient depends on the case.** Self-insured employers and employers otherwise directly involved in the claim are notification recipients (ex-parte rule extends to them). Employers off the active case (when a carrier or TPA handles the claim end-to-end) are just data on file. The per-booking flag that determines this is [UNKNOWN -- queued for Adrian]. [Source: Adrian-confirmed 2026-04-24]
- **Employer data is locked at request-submit; post-submit changes require Gesco-side admin only.** Universal rule; see `appointments.md` business rules and the T4/T5 tension resolution note in `patients.md`. [Source: Adrian-confirmed 2026-04-24]

### Open

- What signal drives the "notify this employer" decision on the booking form? Booker picks? Derived from carrier vs. self-insured status? Something else?
- Are any of the currently-optional address fields (phone, street, city, zip, state) expected to be required at MVP, or all stay optional?
- When the employer IS notified, do they receive the same email content as the other case parties or a distinct employer-facing variant?
- Is employer info pre-filled for a repeat patient (same person, same employer, multiple IMEs on the same claim)?

## Integration points

### Appointments

1:1 with an appointment via `AppointmentId` FK. Created during booking flow.

### Notifications

**Employer MAY be a notification recipient, case by case.** Per the 2026-04-24 decision above: when the employer is self-insured or otherwise a legal party to the claim, they receive the all-parties notifications alongside the other recipients. When a carrier or TPA handles the claim end-to-end, the employer is not notified. [Source: Adrian-confirmed 2026-04-24]

Implementation implications:

- The `AppointmentEmployerDetail` entity needs an **Email** field (not present in current code).
- The appointment (or the employer detail) needs a **"notify employer" flag** -- either a boolean on the employer record, an enum for the employer's role, or determined from another signal (e.g., whether the insurance side is self-insured). [UNKNOWN -- queued for Adrian: what's the intended flag / signal that tells the system whether to notify the employer?]
- Notification logic branches on the flag: skip the employer's email address when the flag is off; include when on.
- When the employer IS notified, the strict email-format requirement from `appointments.md` (legal evidence of communication) applies to them too.

[UNKNOWN -- queued for Adrian: when the employer is notified, do they receive the same email content as the other parties, or a distinct employer-facing variant?]

### Downstream / Packet

Employer information is part of the appointment's data, so it would typically flow into the Packet data bundle for any downstream delivery. [Inferred from the Packet design in `appointments.md`; not currently carrying a recipient-email field for the employer.]

## Edge cases and error behaviors

[UNKNOWN -- to resolve during interview. Candidate cases:]

- Patient is unemployed / retired / self-employed. Is the form field required? What do we store?
- Patient has multiple employers (part-time, contract work). One per appointment or multiple records?
- Employer name changes (company acquisition, rename). Does the historical record stay as captured, or update?
- Occupation changes (patient promoted or switched roles). Historical or current?
- Booker doesn't know the employer's phone / address details. Hard block or allow partial info?

## Success criteria

First-pass sketch:

- Every appointment created through the booking flow has exactly one `AppointmentEmployerDetail` record; bookings cannot be submitted without one.
- The booking form refuses submit if EmployerName or Occupation is blank.
- For cases flagged "employer is a party", the employer receives the all-parties notifications in the same strict legal-evidence format as other recipients, using the employer's email captured on the form.
- For cases flagged "employer is not a party", no email is sent to the employer, but their info still appears in the case record and in the content of notifications that go to other parties.
- After the request is submitted, no UI path lets the booker or the practice-side admin edit employer data; any such edit requires a Gesco-side admin path.
- `AppointmentEmployerDetail` is tenant-scoped correctly and does not exhibit the cross-tenant visibility pattern that FEAT-09 flagged on Patient.

## Known discrepancies with implementation

- `[observed, not authoritative]` `AppointmentEmployerDetail` has no `Email` field. If the employer is meant to be a notification recipient, the entity and form both need an email field added.
- `[observed, not authoritative]` `EmployerName` and `Occupation` are the only required fields. Other fields (phone, street, city, zip, state) are all optional. Intent on which of these should be required at MVP is [UNKNOWN].
- `[observed, not authoritative]` `CreateAsync` and `UpdateAsync` on the AppService use generic `[Authorize]` instead of the specific Create / Edit permissions. Intent on who is allowed to create / edit employer details is [UNKNOWN] (likely the same as other booking-form-captured data -- whoever is submitting the booking).
- `[observed, not authoritative]` No Angular UI for AppointmentEmployerDetail -- managed programmatically during the booking flow. Intent: employer info is a section of the booking form, not a separate screen.
- `[observed, not authoritative]` No tests exist for AppointmentEmployerDetails (per `FEAT-07`).

## Outstanding questions

[UNKNOWN entries roll up to OUTSTANDING-QUESTIONS.md.]

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
