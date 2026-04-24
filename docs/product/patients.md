[Home](../INDEX.md) > [Product Intent](./) > Patients

# Patients -- Intended Behavior

**Status:** draft -- Phase 2 T5, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** the patient (injured worker) is the primary data subject; the doctor's admin is the primary operator for patient records at the practice level; Gesco-side staff have oversight access.

> This document captures INTENDED behaviour for the Patients feature -- what a Patient record is in the portal, how patients are onboarded, how they self-serve, and how their data is scoped for privacy. It does NOT describe what the code currently does (that is `src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md` and `docs/features/patients/overview.md`). Every claim carries a source tag. Code is never cited as authoritative for intent. Observations from code appear ONLY in the Known Discrepancies section, tagged `[observed, not authoritative]`.

## Purpose

Patient records carry the demographic, contact, claim-relevant, and language information for injured workers whose IMEs are scheduled through the portal. Patient is both a **data entity** (the subject of appointments and the source of information carried in the all-parties notification emails that serve as legal evidence of communication) and a **limited portal user** (invited per-tenant, with a narrow self-service view scoped to their own appointments at that tenant). A Patient record is strictly tenant-scoped: the same real person getting IMEs at two different practices is represented as two separate Patient records, one per tenant. Patient self-editing of profile data is prohibited after the first appointment request is submitted, because the data is part of the legal record from that point forward. [Source: Adrian-confirmed 2026-04-24 for tenant scoping and post-submit self-edit restriction]

## Personas and goals

Cross-references: persona definitions live in [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md); Patient-as-booker goals and flow are captured in [appointments.md](appointments.md). This section captures Patient-record-specific goals not already there.

### Patient (injured worker)

The patient is both the data subject and a portal user. Already covered in `appointments.md`:

- Onboarding via email invite with the tenant pre-selected (not an open tenant choice).
- Limited dashboard: two action buttons (Book an appointment / Book a reevaluation) + list of own appointments at this specific tenant.
- Booking form captures the patient's own information, insurance + adjustor info, and applicant + defense attorney info.
- Self-represented exception path with popup + warning banner.

New-to-T5 feature-specific goals:

- [UNKNOWN -- queued for Adrian: can the patient edit their own profile outside of the booking form? E.g., correct a typo in their phone number, update address after moving, add interpreter needs.]
- [UNKNOWN -- queued for Adrian: what happens when a patient needs an IME at a DIFFERENT practice later -- do they get a new invite to the new tenant, or is their existing account reusable across tenants?]

### Doctor's admin (operator)

Admin access to patient records at their practice. Already covered for appointments; patient-record-specific authority:

- [UNKNOWN -- queued for Adrian: can the doctor's admin create a patient record manually (without waiting for the patient to respond to an invite)? E.g., for a phone-intake scenario where the admin books on behalf of the patient.]
- [UNKNOWN -- queued for Adrian: can the doctor's admin edit a patient's profile after creation (e.g., correct typos, update contact info)?]

### Host admin / Gesco-side staff

Oversight + break-glass authority covered in `appointments.md`. Patient-record-specific: [UNKNOWN -- queued for Adrian: does host admin have cross-tenant patient visibility by design (to resolve escalations), or is cross-tenant visibility strictly forbidden?]

## Intended workflow

### Patient onboarding (auto-create during booking)

When a booker submits an appointment request for a patient who doesn't yet have an account, the system auto-creates a Patient record + an IdentityUser with the "Patient" role and sends an invite email with a setup link.

[UNKNOWN -- queued for Adrian: credential handling for auto-created patients. Q-12 in docs/issues/research flags that every auto-created patient gets the same hardcoded default password today, which is a critical security defect. Intent is almost certainly invite-token-flow (no password set at creation; patient sets their own password via the invite link), but needs explicit confirmation.]

### Self-service profile

**The patient's profile is read-only to the patient after the appointment request is submitted.** Once the request fires (and the all-parties notification emails go out), the information carried in that email becomes a **legal document**, and the underlying data cannot be changed without a proper process. The patient cannot self-edit any of that data. [Source: Adrian-confirmed 2026-04-24]

If the patient needs a change (phone number update, address correction, interpreter need, typo fix), they must contact **Gesco-side admins** (not the practice-side doctor's admin) to request the change. Gesco-side admins then run the change through the proper process. [Source: Adrian-confirmed 2026-04-24]

**Tension with T4 Appointments (to resolve in T10 Auth-and-Roles):** the T4 session recorded that the practice-side doctor's admin can "make changes to the appointment form data when a booker requests a change (e.g., updated contact info, corrected attorney info)". The T5 answer above restricts patient-data changes to Gesco-side admins specifically. Possible reconciliations: (a) the T4 authority applies only pre-request-submit, not after; (b) non-patient fields (attorney info, employer info) can be changed practice-side, patient-identity fields cannot; (c) T4 was over-scoped. This boundary will be pinned down explicitly in the T10 Auth-and-Roles cross-cutting session. [Flag surfaced 2026-04-24]

### Admin-initiated patient creation (inline only)

The doctor's admin creates patients **inline with the booking form**, not through a separate patient-management screen. When a caller phones or emails the practice wanting to book an appointment, the admin opens the booking form, types in the patient's information as part of the appointment, and submits -- the patient record is auto-created alongside the appointment. There is no standalone "create patient" path for pre-registration at MVP. [Source: Adrian-confirmed 2026-04-24]

## Business rules and invariants

### Confirmed

- **One Patient record per tenant.** Each tenant has its own Patient records, strictly tenant-scoped. The same real person who books at two different tenants has two separate Patient records (one per tenant), potentially with separate user accounts. Tenant-scoped users see ONLY their own tenant's patients. [Source: Adrian-confirmed 2026-04-24]
- **FEAT-09 is an intent divergence, not a design choice.** The current code's failure to enforce tenant scoping on Patient (Patient has `TenantId` but does NOT implement `IMultiTenant`) is a HIPAA-relevant bug to close, not a deliberate cross-tenant visibility model. The fix -- Patient implements `IMultiTenant`, ABP's auto-filter engages, host-admin paths wrap in `IDataFilter.Disable<IMultiTenant>` for cross-tenant oversight -- matches the intent directly. [Source: Adrian-confirmed 2026-04-24]
- **Patient creation by the doctor's admin is inline with the booking form only.** The admin creates a patient record as part of submitting an appointment for a phone / email caller. There is no standalone "manage patients" screen for pre-registration at MVP. The current code's admin-CRUD on `/patients` is outside MVP scope. [Source: Adrian-confirmed 2026-04-24]
- **Patient cannot self-edit profile data after the appointment request is submitted.** Patient-data changes post-submit require Gesco-side admins (NOT the practice-side doctor's admin) running the change through a proper process. Rationale: the all-parties notification email on request-submit becomes a legal document; underlying data is an evidence record. [Source: Adrian-confirmed 2026-04-24]
- **SSN is optional on the booking form at MVP.** Social Security Number is collected if the booker has it but not required to submit a request. Rationale: attorneys book on behalf of clients (or against opposition's client) and do not necessarily have the SSN at booking time; requiring SSN would block bookings that otherwise should go through. Firm policy on whether SSN should EVER be mandatory remains open for manager confirmation. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION for firm policy]

### Open

- Patient fields at MVP: 22+ fields exist in code. Which are actually required vs. optional vs. removed at MVP?
- SSN handling: is SSN required, and how is it protected (encryption at rest, masking in UI)?
- Credentials for auto-created patients: when the invite fires (on request submit / on approval / both), what the invite contains, TTL, re-invite behaviour.
- Self-service edit boundaries: which fields the patient can edit vs. lock?
- Manual patient creation by the doctor's admin (outside the booking flow): supported or not?

## Integration points

### Upstream

[UNKNOWN -- queued for Adrian: does the patient's information flow in from Digital Forms (the upstream product), from the booker typing it in, or from a combination? Tied to Q11 in OUTSTANDING-QUESTIONS.md.]

### Downstream

Patient data is referenced by:

- **Appointment** -- each appointment has a required `PatientId` FK.
- **Packet (email notifications)** -- patient demographics are part of the data emailed to case parties on every event.
- **Doctor's office intake workflow** -- when reviewing a pending request, office staff read the patient's info off the form.

## Edge cases and error behaviors

[UNKNOWN -- queued for Adrian. Candidate cases:]

- Same real person books an IME at a second tenant later. Create a new patient record or reuse the old one?
- Patient's email address changes (moves email providers). How does the invite / login still work for the old account?
- Patient books with a typo'd email; invite goes nowhere; admin has to correct it. How?
- Patient never responds to the invite. How long does the record sit around? Archive eventually?
- Two different bookers try to create patients with the same email simultaneously. Which one wins?
- Patient account exists but they forgot their password. Standard forgot-password flow, or a specific workers'-comp pattern?

## Success criteria

First-pass sketch (to tighten as open items resolve):

- A patient invited to a specific tenant via email can log in and see only their own appointments at that tenant. Attempting to see other tenants' data -- via the portal UI or direct API call -- returns nothing.
- Auto-creating a patient record during an appointment request does NOT produce a shared hardcoded password (Q12 gap closed). Either a per-user random password + force-change, or an invite-token flow, ships at MVP.
- The booking form accepts a submit without an SSN; validation does not block attorney-initiated bookings for missing SSN.
- Patients cannot self-edit their profile data after the first appointment request is submitted; attempting to submit edits via the profile page is rejected (or the profile page is read-only post-submit).
- Doctor's admin can create a patient record inline with the booking form during phone / email intake without going through a separate "manage patients" screen.
- The cross-tenant visibility leak (FEAT-09) is closed: `Patient` implements `IMultiTenant` and ABP's auto-filter engages; host-admin paths that need cross-tenant visibility use `IDataFilter.Disable<IMultiTenant>` deliberately.

## Known discrepancies with implementation

Pending Phase 3 cross-reference pass. Candidate entries surfaced during evidence load:

- `[observed, not authoritative]` `Patient` has a nullable `TenantId` column but does NOT implement `IMultiTenant`. ABP's automatic tenant filter does not apply. Any authenticated user with `Patients.Default` permission can call `GET /api/app/patients` and see every tenant's patients. This is the known HIPAA-relevant **cross-tenant visibility leak** (tracked as `FEAT-09`). **Intent divergence (pending confirmation):** every signal from T2-Appointments (invite per tenant, patient's view is scoped to one tenant, tenant pre-decided) implies the intent is tenant-scoped visibility -- i.e., `FEAT-09` IS an intent/code gap to close, not a design choice.
- `[observed, not authoritative]` `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync` assigns the hardcoded `CaseEvaluationConsts.AdminPasswordDefaultValue` (typically `1q2w3E*`) to every auto-created patient. Every patient across every tenant gets the same password. Combined with SEC-05 (relaxed password policy), any patient account is trivially compromised. **Intent divergence:** the T2-Appointments confirmed invite-based onboarding pattern implies the intent is an invite-token flow (patient sets their own password on first click), not a shared default. See `docs/issues/research/Q-12.md`.
- `[observed, not authoritative]` `SocialSecurityNumber` is stored as a plaintext nullable string. No encryption at rest, no masking in UI. Intent on SSN handling is [UNKNOWN] but HIPAA-adjacent.
- `[observed, not authoritative]` Patient entity has 22+ fields including some redundant-looking pairs (`Address` vs `Street`) and some orphan-looking ones (`ApptNumber`, `InterpreterVendorName`, `OthersLanguageName`, `RefferedBy`). MVP intent on which fields are required / optional / removed is [UNKNOWN].
- `[observed, not authoritative]` Field name typo: `RefferedBy` should be `ReferredBy`. Not an intent question, just a code / schema fix.
- `[observed, not authoritative]` Booking and profile methods (`GetOrCreatePatientForAppointmentBookingAsync`, `UpdateMyProfileAsync`, etc.) only require `[Authorize]` (any authenticated user), not Patients-specific permissions. Intent on who specifically can call these is [UNKNOWN].
- `[observed, not authoritative]` No tests exist for Patients (per `FEAT-07`).

## Outstanding questions

Each bare `[UNKNOWN]` above rolls up to [OUTSTANDING-QUESTIONS.md](OUTSTANDING-QUESTIONS.md) once surfaced as manager-facing.

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
