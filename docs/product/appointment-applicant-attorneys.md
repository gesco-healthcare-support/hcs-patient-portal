[Home](../INDEX.md) > [Product Intent](./) > Appointment Applicant Attorneys

# Appointment Applicant Attorneys -- Intended Behavior

**Status:** draft -- Phase 2 T6, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** [UNKNOWN -- pending T6 interview]

> Captures INTENDED behaviour for the `AppointmentApplicantAttorney` join entity that links an Appointment to an ApplicantAttorney record and an IdentityUser. Cross-reference the sibling files [applicant-attorneys.md](applicant-attorneys.md) (the attorney record itself) and [appointment-accessors.md](appointment-accessors.md) (access-grant / ACL entity that may overlap semantically).

## Purpose

`AppointmentApplicantAttorney` captures the formal legal relationship between an appointment and the applicant attorney(s) involved in the case -- attorney X is party to this appointment on behalf of the injured worker. At MVP, this join entity is also **the portal access mechanism** for the attorney: any attorney linked to an appointment via this join (or the analogous link for defense attorneys, per `applicant-attorneys.md`) can view and interact with the appointment through the portal. There is no separate access-grant layer (see `appointment-accessors.md` for why). [Source: Adrian-confirmed 2026-04-24]

If / when defense attorneys land as an entity (parallel or unified under `applicant-attorneys.md`), the same join-entity pattern applies to them -- either via this table with a side flag, via a mirror `AppointmentDefenseAttorney` join, or via a unified `AppointmentAttorney` join. The exact shape is an architecture decision left to implementation.

## Personas and goals

Cross-reference: see [appointments.md](appointments.md) and [applicant-attorneys.md](applicant-attorneys.md).

## Intended workflow

### Creating a join record

[UNKNOWN -- queued for interview: when is an `AppointmentApplicantAttorney` record created? Candidate triggers: (a) attorney submits a booking for their client, (b) patient / booker types in attorney info on the booking form, (c) defense attorney identifies the applicant attorney in their own booking. Likely some combination, but precise intent is open.]

### Multiple attorneys on one appointment

[UNKNOWN -- queued for interview: can an appointment have multiple `AppointmentApplicantAttorney` records (e.g., co-counsel)? Or is it one-to-one per appointment?]

### Relationship to AppointmentAccessor

[UNKNOWN -- core interview question: does creating an `AppointmentApplicantAttorney` automatically create an `AppointmentAccessor` grant (so the attorney can view/edit the appointment in the portal), or are the two entities separately managed?]

## Business rules and invariants

### Confirmed

- **One applicant attorney per appointment at MVP.** No co-counsel support. If a real-world case has co-counsel, only the lead attorney is named on the portal record; the other attorney works through them. [Source: Adrian-confirmed 2026-04-24]
- **Join record membership IS the portal access mechanism for the attorney.** No separate access-grant layer. An attorney linked via this join sees and can interact with the appointment through the portal; non-linked users do not. [Source: Adrian-confirmed 2026-04-24]
- **Attorney firm info is pre-filled for returning attorneys.** Applicant attorneys (and defense attorneys, per the symmetric intent in `applicant-attorneys.md`) have saved firm profiles that pre-populate on each new booking. [Source: Adrian-confirmed 2026-04-24]
- **No ad-hoc grants.** Paralegals, assistants, or similar helpers do not get their own access; they work through the attorney's login. [Source: Adrian-confirmed 2026-04-24]

### Open

- [UNKNOWN -- queued for Adrian / manager: when is an `AppointmentApplicantAttorney` record created, exactly? Is it automatic on booking submit (when the booker specifies the attorney on the form), or is there a separate "add attorney to appointment" action? Relates to the inline booking form captured in appointments.md.]

## Integration points

- **Appointments** -- each join record references exactly one Appointment.
- **ApplicantAttorneys** -- each join record references exactly one ApplicantAttorney record.
- **AppointmentAccessors** -- relationship is [UNKNOWN] (see above).
- **Notifications** -- the attorney's IdentityUser email is a recipient on every all-parties notification per `appointments.md` ex-parte rule.

## Edge cases and error behaviors

[UNKNOWN -- to resolve during interview.]

## Success criteria

First-pass sketch:

- A booker submitting an appointment with an applicant attorney named produces exactly one `AppointmentApplicantAttorney` record linking that attorney, the appointment, and the attorney's user.
- The same attorney can be linked to many appointments over time (attorney represents the client across several cases or IMEs).
- No appointment has more than one applicant-attorney join record at MVP (no co-counsel).
- An attorney's portal view of "my appointments" is driven by their join records: they see exactly the appointments where they are linked, nothing else.

## Known discrepancies with implementation

- `[observed, not authoritative]` No Angular UI exists for managing join records; they are created programmatically through the Appointments booking flow. Intent on whether a manual "add an attorney to this appointment" UI is needed is [UNKNOWN].
- `[observed, not authoritative]` The join has THREE required FKs (AppointmentId, ApplicantAttorneyId, IdentityUserId), which is unusual -- the IdentityUserId seems redundant with the ApplicantAttorney.IdentityUserId. Intent on why IdentityUserId is duplicated is [UNKNOWN]. Likely a legacy artifact.
- `[observed, not authoritative]` No tests exist for AppointmentApplicantAttorneys.

## Outstanding questions

[UNKNOWN entries roll up to OUTSTANDING-QUESTIONS.md.]

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
