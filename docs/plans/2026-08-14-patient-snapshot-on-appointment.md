---
feature: Freeze patient demographics onto the appointment as a legal record
date: 2026-08-14
status: draft
base-branch: main
related-issues: []
---

# Item 5 -- Patient snapshot on the appointment

## Goal

Stop a patient edit from retroactively changing what previously booked appointments report, by
recording the patient's demographics on the appointment at booking time.

## Context & decisions

Adrian's principle, verbatim: _"Previous appointment details should not be changed based on this
new appointment, that old appointment should stay as it is because that is a log/legal trail of
what has happened till now."_

That principle already holds for everything except the patient:

- **Attorneys** -- `Appointment` carries ~20 denormalised columns and `PartyDetailResolver.cs:103-108`
  reads from the appointment, not the shared master.
- **Employer, Primary Insurance, Claim Examiner** -- per-appointment child rows, each with its
  own `AppointmentId`.
- **Patient** -- read LIVE. `PartyResolver.cs:47-59` does
  `_patientRepository.FindAsync(appointment.PatientId)`, and `PacketTokenResolver.cs:150-175`
  does the same for generated documents.

So today, editing a patient rewrites what every one of their prior appointments reports, to the
Case Tracker and on any regenerated packet. **This is a pre-existing defect**, not something the
booking work introduces; the booking work only makes it easier to trigger.

Resolved decisions:

1. **Decision: records read the snapshot, contact reads live.** The Case Tracker payload and
   generated packets are the legal record of what was served, so they freeze. Notification
   emails and recipient resolution keep reading the live patient, because a reminder must reach
   the address the patient has today. Treating both the same is how this gets built wrong.
2. **Decision: copy the SSN too**, because `PacketTokenResolver.cs:160` already renders it on
   generated documents; without it a frozen packet still moves when the patient record is
   corrected. It duplicates PHI into a table that already holds names, dates of birth and
   addresses for the same person, under the same tenant isolation.
3. **Decision: no backfill.** Existing appointments keep null columns and fall back to the live
   patient. We do not know what a patient's details were when a May appointment was booked, and
   stamping today's values onto a record whose purpose is being a legal trail would assert a
   history we cannot support.
4. **Decision: write on booking AND on appointment edit**, mirroring how `Appointment.PatientEmail`
   already behaves. The property being protected is "an edit to the shared patient never reaches
   a prior appointment", not immutability -- a typo caught at booking should still be fixable on
   that booking.
5. **Decision: `Patient.Id` is untouched.** `samePersonGroupKey` is computed from it and is what
   tells the Case Tracker two claims belong to the same person.

## All needed context

| Fact                                       | Anchor                                                                                                   |
| ------------------------------------------ | -------------------------------------------------------------------------------------------------------- |
| The existing precedent, already a snapshot | `Appointment.cs:55` `PatientEmail`                                                                       |
| Written on create                          | `AppointmentsAppService.cs:884`                                                                          |
| Written on appointment update              | `AppointmentsAppService.cs:1115`                                                                         |
| Never synced from a patient edit           | no writer in `Application/Patients/` -- verified                                                         |
| Case Tracker payload reads live            | `PartyResolver.cs:47-59`                                                                                 |
| Packets read live, including SSN           | `PacketTokenResolver.cs:150-175`, SSN at `:160`                                                          |
| Reads to LEAVE live (contact/routing)      | `AppointmentRecipientResolver.cs`, `BookingSubmissionEmailHandler.cs`, `DocumentEmailContextResolver.cs` |
| Attorney denormalisation to mirror         | `Appointment.cs:58-90+`                                                                                  |
| Patient columns (18)                       | `Domain/Patients/Patient.cs`                                                                             |
| Unit column history                        | `Patient.ApptNumber` is the unit; `Patient.Address` held units on older rows                             |

Gotchas:

- **Dual-context migrations are mandatory.** `Appointment` is configured in BOTH
  `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`, so the columns need a migration
  in `Migrations/` AND `TenantMigrations/`. Verify the columns in SQL in both a host and an
  office database; do not trust the generator.
- `DocumentEmailContextResolver.cs:169` currently reads `patient?.Email ?? appointment.PatientEmail`
  -- it prefers LIVE and treats the existing snapshot as fallback. Under decision 1 that reader
  stays live, so this line is already correct. Do not "fix" it to prefer the snapshot.
- The fallback in decision 3 means every switched reader needs a null check. Put the fallback in
  one place rather than repeating it at each call site.
- SSN has an audited reveal endpoint (`PatientsAppService.GetFullSsnAsync`) and is masked to the
  last four for all callers on read DTOs. Copying it to the appointment must not create a second,
  unaudited read path.

## Tasks

### T1 -- add the columns

approach: code

MODIFY `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs`: add nullable
snapshot columns beside the existing `PatientEmail` -- `PatientFirstName`, `PatientMiddleName`,
`PatientLastName`, `PatientDateOfBirth`, `PatientSocialSecurityNumber`, `PatientPhoneNumber`,
`PatientCellPhoneNumber`, `PatientPhoneNumberTypeId`, `PatientStreet`, `PatientApptNumber`,
`PatientCity`, `PatientStateId`, `PatientZipCode`, `PatientGenderId`,
`PatientInterpreterVendorName`. Doc-comment the block with WHY (legal trail) and that null means
"booked before this shipped, read the live patient".

pattern: the attorney denormalisation block at `Appointment.cs:58-90+`.

acceptance (EARS): The system shall persist a nullable per-appointment copy of each listed
patient field.

### T2 -- EF config and dual-context migrations

approach: code

MODIFY both `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs` in the
`Appointment` config block. Generate ONE migration per set, named `Added_PatientSnapshotFields`,
into `Migrations/` and `TenantMigrations/` respectively. No backfill SQL (decision 3).

pattern: the 4c/4e dual migrations, e.g. `Added_ChangeRequestConsentRounds` present in both sets
with different timestamps.

acceptance (EARS):

- WHEN the migrations run, THE SYSTEM SHALL add the columns to the host database AND to every
  office database.
- WHEN the migration runs, THE SYSTEM SHALL leave existing rows null.

### T3 -- write the snapshot

approach: tdd

MODIFY `AppointmentsAppService.cs`: populate the snapshot from the resolved patient at create
(beside `:884`) and at update (beside `:1115`).

pattern: the adjacent `appointment.PatientEmail = input.PatientEmail;` assignments.

acceptance (EARS):

- WHEN an appointment is created, THE SYSTEM SHALL store the patient's current demographics on
  it.
- WHEN an appointment is updated, THE SYSTEM SHALL refresh that stored copy.
- WHEN a patient record is edited directly, THE SYSTEM SHALL NOT alter any appointment's stored
  copy.

### T4 -- one shared read helper

approach: tdd

CREATE a single resolver (Domain) that returns the patient values for an appointment: the
snapshot when present, the live patient when null. Every record-side reader calls it, so the
fallback exists once.

acceptance (EARS):

- WHERE an appointment has a stored copy, THE SYSTEM SHALL return the stored values.
- WHERE it does not, THE SYSTEM SHALL return the live patient's values.

### T5 -- switch the record readers

approach: tdd

MODIFY `PartyResolver.cs:47-59` (Case Tracker payload) and `PacketTokenResolver.cs:150-175`
(generated packets) to use T4's resolver. Leave `AppointmentRecipientResolver`,
`BookingSubmissionEmailHandler` and `DocumentEmailContextResolver` reading the live patient
(decision 1).

acceptance (EARS):

- WHEN a patient's address is edited after an appointment was booked, THE SYSTEM SHALL continue
  to report the booked-time address to the Case Tracker for that appointment.
- WHEN a packet is regenerated after such an edit, THE SYSTEM SHALL render the booked-time
  values.
- WHEN a notification is sent, THE SYSTEM SHALL use the patient's current contact details.

### T6 -- contract note

approach: code

MODIFY `docs/integration/case-tracker-api-contract.md` to record that `data.patient` reflects the
appointment as booked and no longer changes when the patient record is later corrected.

acceptance (EARS): The contract shall state that patient data on an appointment is
booked-time, not current.

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

Then confirm the columns landed in BOTH databases -- a dual-context entity has silently reached
only one set before, and `dotnet build` cannot detect it. Resolve the container name first
(`docker ps --format '{{.Names}}' | grep -i sql`), then, noting that `MSYS_NO_PATHCONV=1` and the
`bash -c` wrapper are both required or Git Bash rewrites the leading `/opt` path:

```bash
MSYS_NO_PATHCONV=1 docker exec -i <sql-container> bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d <database>' <<'SQL'
SELECT COUNT(*) AS SnapshotColumns FROM sys.columns
WHERE object_id = OBJECT_ID('AppAppointments') AND name LIKE 'Patient%';
GO
SQL
```

## Risk / rollback

Blast radius: the Case Tracker payload and every generated packet -- the two things an external
party and a court actually see. A defect here shows up on documents rather than on screen.

Mitigation: no backfill means existing appointments keep their current behaviour, so the change
only affects bookings made after it ships.

Rollback: revert the code, and drop the columns via a down-migration in both sets. Because
nothing was backfilled and the readers fall back to live data, reverting restores exactly the
prior behaviour with no data loss.
