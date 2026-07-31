---
id: BUG-030
title: Internal-staff booking auto-approves on create but leaves AppointmentApproveDate NULL
severity: medium
status: fixed
fixed: 2026-05-22 (live-verified internal-staff path now stamps; external bookings still null)
last-replayed: 2026-05-23 (R3 replay confirmed: A00005 created with Status=2 + AppointmentApproveDate populated)
found: 2026-05-21 hardening HRD-P3.5
flow: internal-staff-booking
component: src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs (CreateAsync)
---

# BUG-030 - Internal-staff booking auto-approves without ApproveDate

## Symptom

HRD-P3.5: clistaff1 deep-linked to `/appointments/add`, picked existing patient1 from the "Existing Patients" dropdown, filled type=Record Review / location=Demo Clinic North / date=2026-05-25 / time=09:00 AM, plus AA (appatty1), DA (defatty1), and a claim with Insurance + CE (claimE1).

Clicking "Book an appointment" produced confirmation number A00005. DB row:
| field | value |
|---|---|
| `RequestConfirmationNumber` | `A00005` |
| `AppointmentStatus` | **2 (Approved)** |
| `AppointmentApproveDate` | **NULL** |
| `ApplicantAttorneyEmail` | `appatty1@gesco.com` |
| `DefenseAttorneyEmail` | `defatty1@gesco.com` |
| `ClaimExaminerEmail` | `claime1@gesco.com` |
| `CreatorId` | clistaff1's user id |

Compare with the other four bookings in this hardening run (patient1, appatty1, defatty1, claimE1 bookers — A00001..A00004): all four landed at `AppointmentStatus = 1 (Pending)` and required an explicit clinic-staff approval to flip to 2.

So internal-staff bookings (clinic staff, staff supervisor) skip the Pending state entirely. But the auto-approve path that sets `AppointmentStatus = 2` does NOT also stamp `AppointmentApproveDate`, leaving the row in an inconsistent state.

Suite Phase 3 Pass criteria (HARDENING-TEST-SUITE.md line 525) only checks "row exists; all four email columns non-null". A00005 passes those. The deviation is from the implied behavior at Phase 5 — `HRD-P5.5 supervisor approves internal-staff-booked` — which assumes the row is initially Pending so it can be moved to Approved. Since the row is ALREADY Approved on create, Phase 5.5's approve POST will hit the InvalidTransition guard (R2.10 pattern).

## Hypothesis

1. **Intentional auto-approve but field omitted.** The design says "internal staff don't need approval for their own bookings" (consistent with workflow where staff is creating bookings during patient phone calls). The implementer set `AppointmentStatus = Approved` but forgot to also set `AppointmentApproveDate = DateTime.UtcNow`. Fix: add the date stamp.

2. **Side-effect of `ResponsibleUserId` defaulting to creator.** When `CreatorId` has a Clinic Staff or Staff Supervisor role, a status-transition guard somewhere auto-elevates the row to Approved on create. The guard runs before any approve-date setter. Fix: refactor the auto-approve into a proper command path that stamps both fields.

3. **Bug in command pipeline.** The intended flow was Pending-on-create, but a recent change inadvertently auto-approves all rows where the booker has a non-external role. Fix: revert; require explicit approve.

Most likely (1) — auto-approve is an OLD-app convention (review CLAUDE.md primary mission re: parity) and the field omission is a forgot-to-set bug.

## Reproduction

1. Log in as `clistaff1@gesco.com` on `http://falkinstein.localhost:4200`.
2. Navigate directly to `/appointments/add` (NOT the click-nav "+ New Appointment Request" button per [[OBS-19]]).
3. Pick `patient1@gesco.com` from the "Existing Patients" dropdown.
4. Fill type=Record Review, location=Demo Clinic North, date=any future weekday slot, time=09:00 AM.
5. Fill employer, AA (appatty1), DA (defatty1), Claim modal with Insurance + CE (claimE1).
6. Click "Book an appointment".
7. SQL: `SELECT RequestConfirmationNumber, AppointmentStatus, AppointmentApproveDate FROM AppAppointments WHERE Id = <new id>` -> status=2, approve_date=NULL.

## Recommended fix

In the auto-approve branch (search `AppointmentStatus = AppointmentStatus.Approved` or similar in `AppointmentsAppService.cs` / `AppointmentManager.cs`), add `AppointmentApproveDate = clock.Now` alongside the status assignment. Also assign `PrimaryResponsibleUserId` and any other normally-required approval fields so the row is internally consistent with rows that flow through the Pending -> Approved transition.

Cross-check the OLD app at `P:\PatientPortalOld\PatientAppointment.Api` for the intended behavior; the convention may be older than the NEW app's wiring.

## Functional impact

- HRD-P5.5 in this run will fail with `InvalidTransition` when stafsuper1 tries to approve A00005 -- the suite expected the row to start at Pending. Workaround: skip P5.5 for this run, file this BUG, retry after fix.
- Reports/audits that join `AppAppointments` to `AppointmentApproveDate` (e.g., "approved this week") will MISS internal-staff-booked rows because their approve_date is NULL.
- Packet generation: Phase 6 likely still triggers for A00005 because the trigger watches `AppointmentStatus = 2`, not `AppointmentApproveDate IS NOT NULL`. Need to confirm in Phase 6 whether packets generate for an auto-approved row.

## Related

- [[OBS-19]] -- internal-booker uses admin CRUD modal (background on why deep-link is used). This BUG is about what happens AFTER the deep-link flow submits.
- [[BUG-031]] -- 403 on /api/app/appointment-injury-details for clinic-staff role (a sibling finding from the same HRD-P3.5 run).
- [[BUG-024]] -- reject accepts empty reason. Similar "field that should be stamped is missing" pattern.
- BUG-032 (fixed 2026-05-22 via PR #235) -- same companion-field-stamping pattern at the Reject transition. This fix is the create-time twin.

## Fix verified (2026-05-22)

Fix lives at
`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs`,
inside `CreateAsync`, immediately after the `new Appointment(...)`
call and before the `InsertAsync` call:

```csharp
if (appointmentStatus == AppointmentStatusType.Approved)
{
    appointment.AppointmentApproveDate = DateTime.UtcNow;
}
```

The fix is intentionally at the manager (not the AppService) because
the manager is the single source of truth for the create-time
entity-invariant write. Any future caller that books with
`appointmentStatus = Approved` (e.g., a programmatic seeder, an
admin-initiated booking, or a future internal API surface) inherits
the stamp without per-call boilerplate.

### Live verification matrix

Two appointments booked from the running stack via the full OIDC PKCE
flow, on freshly-seeded available `AppDoctorAvailability` slots:

| Test | Booker (role) | Expected status | Expected ApproveDate | Actual |
|---|---|---|---|---|
| A | `clistaff1@gesco.com` (Clinic Staff -- internal user) | 2 (Approved) | non-null timestamp | **2 (Approved)** + **2026-05-22 21:04:44** (stamped) |
| B | `patient@falkinstein.test` (Patient -- external user) | 1 (Pending) | NULL | **1 (Pending)** + **NULL** (correctly not stamped) |

Baseline cross-check: A00001 (a pre-fix `clistaff1` booking on the
same stack) still shows `AppointmentStatus = 2, AppointmentApproveDate
= NULL` -- the original-bug shape, untouched by this fix because the
fix only applies to NEW bookings going through `CreateAsync`.

### Unit-test suite

Application.Tests: 538/538 pass. Domain.Tests: 13/13 pass (+ 4
pre-existing Skipped). No regressions.

### Out of scope (captured here for follow-up tracking)

- **`PrimaryResponsibleUserId`** is still NULL on internal-staff
  auto-approved rows. The Phase 12 Approve transition path takes this
  via `ApproveAppointmentInput.PrimaryResponsibleUserId`; the
  internal-staff fast-path on create has no equivalent input. Defaulting
  it to the creator or another deterministic value is a product
  decision; this fix keeps the existing null behavior.
- **Domain-level state-machine guard** on `Appointment.AppointmentStatus`
  setter (per the feature CLAUDE.md warning "no domain-level state-machine
  guard"). Broader scope.
