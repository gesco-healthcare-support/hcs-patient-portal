[Home](../INDEX.md) > [Product Intent](./) > Doctors

# Doctors -- Intended Behavior

**Status:** draft -- Phase 2 T4, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** [UNKNOWN -- the doctor themselves (medical examiner) OR the doctor's admin as their proxy; resolved during interview]

> This document captures INTENDED behaviour for the Doctors feature -- what a Doctor record is in the portal and how doctors / practices are onboarded. It does NOT describe what the code currently does (that is `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md` and `docs/features/doctors/overview.md`). Every claim carries a source tag. Code is never cited as authoritative for intent. Observations from code appear ONLY in the Known Discrepancies section, tagged `[observed, not authoritative]`.

## Purpose

The Doctors feature carries the profile and coverage information for each medical examiner providing IMEs through the portal. Each Doctor is the **subject** of appointments that bookers request and of the notifications / Packet data sent to case parties, but is most likely not a day-to-day portal user (bookings and reviews are operated by the doctor's admin staff on the examiner's behalf, pending manager confirmation). A Doctor record owns the coverage that drives which exam types and which locations the practice can serve, and is scoped 1:1 to a tenant. [Source: Adrian-confirmed 2026-04-24 for tenant cardinality; Adrian best-guess 2026-04-24 for "doctor as subject, not user" -- NEEDS CONFIRMATION per OUTSTANDING-QUESTIONS.md Q21]

## Personas and goals

Persona definitions live in [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md). Schedule-publishing goals live in [doctor-availabilities.md](doctor-availabilities.md). This section captures Doctor-record-specific goals.

### Doctor (medical examiner)

Authority: the examiner is the clinical owner of the practice. In portal terms, Doctor is an **entity** (profile record in Doctor table, surfaced to bookers and in case notifications). Whether the examiner is also a portal USER who logs in is leaning toward "no" per Adrian 2026-04-24, but not firm. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; see OUTSTANDING-QUESTIONS.md Q21]

Feature-specific goals:

- **Doctor as subject of appointments, not necessarily as user of the portal.** Adrian's working assumption 2026-04-24 is that day-to-day portal users are the doctor's intake / admin staff (tenant-level) plus Gesco-side managers (host-level), and that examiners themselves likely do not log in. If confirmed, this means: `Doctor.IdentityUserId` stays optional or becomes effectively unused; the "Doctor role can approve / reject / send-back" claim in `appointments.md` needs revision (the actions are taken by the doctor's admin and host-side staff, not by the doctor directly); FEAT-12 role separation simplifies (the onboarding-created user is the tenant admin, not the doctor). [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION]
- **New / emerging persona flag:** Adrian 2026-04-24 referenced "the doctor's manager here at our side" as a portal user. This may be the same as the already-defined host admin, OR a more specific host-side role (a Gesco account manager with a portfolio of doctors). [UNKNOWN -- queued for Adrian / manager]
- [UNKNOWN -- queued for Adrian: what profile information matters at MVP -- is name + email + gender enough, or do we need license number, credentials, bio, photo, etc.?]

### Doctor's admin

Acts on behalf of the doctor for day-to-day portal operations. Already covered in `appointments.md` and `doctor-availabilities.md`. The Doctor-record-specific question is whether the admin CAN edit the Doctor profile itself, or only the schedule and appointments. [UNKNOWN -- queued for Adrian]

### Host admin

Creates new practices and their Doctor profiles during onboarding (tenant provisioning). May also deactivate / archive doctors when practices leave. [UNKNOWN -- queued for Adrian for onboarding flow specifics]

## Intended workflow

### Onboarding a new doctor / practice

**MVP: host-admin-initiated, manual setup.** Host admin (Gesco-side) creates the new tenant, the Doctor profile, and the initial user account directly through an admin UI. No invite-email flow for doctors at MVP (this differs from the patient onboarding flow, which is invite-based). Email invites or self-service signup are post-MVP considerations Adrian will re-evaluate later. [Source: Adrian-confirmed 2026-04-24 for MVP; pending manager validation for long-term approach; see OUTSTANDING-QUESTIONS.md Q20]

The onboarding operation creates (at minimum):

- A new SaaS tenant scoped to the doctor's practice.
- A Doctor profile record (name, email, gender, etc.) inside the tenant.
- An initial user account for the practice. [UNKNOWN -- queued for Adrian in this interview: is the initial user the doctor themselves, the doctor's admin, or both?]

Specific Doctor profile fields captured at onboarding, and the practice's AppointmentType / Location coverage choices, are [UNKNOWN -- queued for Adrian in this interview].

### Editing a doctor profile and coverage

**Coverage (appointment types + locations) is jointly managed** by both the host admin (Gesco-side) and the practice's doctor's admin (tenant-side). Either party can add or remove exam types and locations from the doctor's coverage. No gatekeeping pattern between them; both have the authority. [Source: Adrian-confirmed 2026-04-24]

**Profile fields** (name, email, gender, and any additional fields pending Q22 on case-record requirements): authority to edit is [UNKNOWN -- queued for Adrian in a future session]. Current code allows the CRUD service to update all fields; intent on whether the doctor's admin can edit the doctor's own name / email (or whether that's host-only) is unresolved.

### Deactivating / removing a doctor

[UNKNOWN -- queued for Adrian: what happens when a practice stops using the portal? Soft-delete the doctor? Archive the tenant? What happens to existing appointments?]

## Business rules and invariants

### Confirmed

- **Exactly one doctor per tenant.** Each tenant in the portal represents exactly one medical examiner. Multi-physician practices (if they exist in Gesco's customer base) are modelled as separate tenants, one per physician, not as a single multi-doctor tenant. Consequences: no `DoctorId` FK is needed on `DoctorAvailability` or `Appointment` because the tenant scope already identifies the doctor; bookers do not pick a doctor during the booking flow (the doctor is implied by the tenant they are invited into); office staff work with a single doctor's schedule at a time. [Source: Adrian-confirmed 2026-04-24]

### Open

- **Doctor login existence.** Adrian's working assumption is the examiner does NOT log in; a separate Gesco-side role ("the doctor's manager") may exist alongside host admin. Pending Q21. If confirmed, `Doctor.IdentityUserId` becomes effectively unused and FEAT-12 role separation simplifies.
- **MVP doctor profile fields.** Pending Q22 (whether name / email / gender is enough, or whether credentials, license, bio, photo, etc. are required by the case record or regulatory paperwork).
- **Email max length code typo.** Code says 49, Patient uses 50, neither matches the ASP.NET Identity convention of 256. Not a product decision -- will be fixed to match the Identity convention during MVP build per `docs/issues/research/Q-07.md`. Not blocking.
- Deactivation behaviour: what happens when a practice leaves (pending appointments, future-dated slots, archiving). [UNKNOWN -- queued for Adrian in a future session]

### Confirmed -- coverage management

- **Both host admin and doctor's admin can manage a doctor's coverage** (which appointment types the practice offers and which locations they work at). No gatekeeping pattern between them; either party has full authority to add or remove. Initial coverage is set at onboarding by the host admin; subsequent changes can be made by either side. [Source: Adrian-confirmed 2026-04-24]

## Integration points

### Upstream

[UNKNOWN -- queued for Adrian: new doctor onboarding -- where does Gesco get the data to set up a new doctor's tenant (manual entry, a contract document from the client, a form-capture record)?]

### Downstream

Doctor profile data is referenced (indirectly) by:

- **DoctorAvailability** -- slots belong to the doctor's tenant (no direct FK; implicit via tenant scope).
- **Appointment** -- appointments are booked against the doctor's slots (indirect via slot).
- **Packet** (at MVP, email notifications) -- the doctor's name, practice name, and location are part of the data sent to case parties. [Implied from appointments.md]

## Edge cases and error behaviors

[UNKNOWN -- queued for Adrian. Candidate cases:]

- Doctor's email address changes (doctor moves email providers). Cascades to IdentityUser? Notification sent?
- Doctor moves to a new office (new location). How is location history preserved for past appointments?
- Doctor leaves Gesco's roster. What happens to future-dated availability, pending appointment requests, in-flight bookings?
- Two practices have doctors with the same name -- any conflict concerns?
- Doctor credentials / license expire. Is the system aware? Does booking get blocked?

## Success criteria

First-pass sketch (to be tightened as open items resolve):

- Host admin can create a new doctor practice (tenant + Doctor record + initial admin user) from a single admin UI without needing engineering intervention.
- The newly-created tenant has its AppointmentType and Location coverage populated during onboarding; bookers see appropriate availability for the practice from day one.
- The doctor's admin can log into the new tenant and begin publishing the schedule (per `doctor-availabilities.md`) without additional setup steps.
- Either host admin or the doctor's admin can edit the practice's coverage (types and locations) after onboarding without support tickets.
- The Doctor's name and practice information appear correctly in appointment confirmations and Packet emails sent to case parties.

## Known discrepancies with implementation

Pending Phase 3 cross-reference pass. Candidate entries surfaced during evidence load:

- `[observed, not authoritative]` `DoctorConsts.EmailMaxLength = 49` (`Patient` uses 50). Almost certainly a typo per `docs/issues/research/Q-07.md`. Not an intent question -- just a code gap to fix when the MVP build passes through.
- `[observed, not authoritative]` `Doctor.IdentityUserId` is optional. Means a Doctor record can exist without a login account. Intent on whether this is deliberate (data-only doctor profiles supported) or accidental (every doctor should have a login) is [UNKNOWN].
- `[observed, not authoritative]` `DoctorTenantAppService.CreateAsync` assigns the "Doctor" role to the first user of a new tenant and treats that user as both the tenant admin and the doctor. `FEAT-12` flags this role conflation. `appointments.md` confirmed MVP intent for a separate `doctor's admin` tenant-level role; that role needs to be introduced in the onboarding flow, splitting from the Doctor role.
- `[observed, not authoritative]` No direct FK from `DoctorAvailability` or `Appointment` to Doctor. Doctor is implied via tenant scope. Intent depends on whether the tenant model remains "one doctor per tenant" (in which case no FK is needed) or becomes "multiple doctors per tenant" (in which case a DoctorId FK on availability / appointment becomes mandatory).
- `[observed, not authoritative]` Host vs tenant cascade behaviour on `DoctorAppointmentType` and `DoctorLocation` differs (Cascade vs NoAction). Intent on whether deletes should cascade is pending interview.

## Outstanding questions

Each bare `[UNKNOWN]` above rolls up to [OUTSTANDING-QUESTIONS.md](OUTSTANDING-QUESTIONS.md) once surfaced as manager-facing.

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
