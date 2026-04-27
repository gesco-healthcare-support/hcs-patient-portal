[Home](../INDEX.md) > [Issues](./) > Data Integrity

# Data Integrity Issues

<!-- Last reorganized 2026-04-24 against docs/product/ + docs/gap-analysis/ -->

Seven data integrity issues were identified. Two are critical and can cause silent data corruption (double-booked slots, duplicate confirmation numbers) under normal concurrent usage. All issues listed here can result in incorrect or unrecoverable data in the production database.

> **Test Status (2026-04-02)**: DAT-01 partially confirmed (EF transaction prevented double-booking in test, but no distributed lock exists). DAT-02 not reproduced in serial testing. DAT-03 confirmed via E6 and B11.2.1 (slot stays Booked after DELETE). DAT-05 confirmed via B10.1 and B11.1.1 (status immutable after creation). SQL integrity checks: 0 orphaned records, 0 null CreatorIds, 0 duplicate confirmation numbers. See [TEST-EVIDENCE.md](TEST-EVIDENCE.md). Live test inventory as of 2026-04-24: 113 [Fact] + 2 [Theory] = 115 methods across 17 files; entities with tests are Appointments, DoctorAvailabilities, Doctors, Patients, Books, AppointmentAccessors, ApplicantAttorneys, Locations.

---

## DAT-01: Race Condition on Slot Booking

**Severity:** Critical
**Status:** Open

### Description

`AppointmentsAppService.CreateAsync` performs a check-then-act sequence on `DoctorAvailability.BookingStatusId` without any locking. Two concurrent requests for the same slot can both pass the `Available` check before either writes the `Booked` state, resulting in a double-booked slot.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` lines 219--249:

```csharp
// Line 219: check
if (doctorAvailability.BookingStatusId != BookingStatus.Available)
    throw new UserFriendlyException("This slot is no longer available.");

// ... build appointment ...

// Line 248: act (no lock held between check and act)
doctorAvailability.BookingStatusId = BookingStatus.Booked;
await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);
```

`DoctorAvailability` does not use optimistic concurrency (`ConcurrencyStamp` is present on `Appointment` but not on `DoctorAvailability`). The `Medallion.Threading.Redis` distributed lock library is already registered in the host module but is not used here.

### Impact

Two patients can simultaneously book the same appointment slot. Both bookings succeed, two `Appointment` records point to the same `DoctorAvailabilityId`, and the doctor has a double-booked calendar entry with no system-level indication of the conflict.

### Recommended Fix

Wrap the check-then-act in a distributed lock keyed to the `DoctorAvailabilityId`:

```csharp
await using (await _distributedLock.AcquireAsync($"slot:{doctorAvailabilityId}"))
{
    var doctorAvailability = await _doctorAvailabilityRepository.GetAsync(doctorAvailabilityId);
    if (doctorAvailability.BookingStatusId != BookingStatus.Available)
        throw new UserFriendlyException(L["SlotNoLongerAvailable"]);

    // ... create appointment ...

    doctorAvailability.BookingStatusId = BookingStatus.Booked;
    await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);
}
```

Alternatively, add a `[ConcurrencyCheck]` attribute to `DoctorAvailability.BookingStatusId` and handle `DbUpdateConcurrencyException` to retry with a user-friendly message.

---

## DAT-02: Duplicate Confirmation Numbers Possible

**Severity:** Critical
**Status:** Open

### Description

`GenerateNextRequestConfirmationNumberAsync` (lines 254--281 of `AppointmentsAppService.cs`) reads the current maximum confirmation number from the database and increments it by one. This is a read-max-then-increment pattern without any locking. Two concurrent booking requests will both read the same maximum value and generate the same confirmation number.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` lines 254--281

### Impact

Two appointments receive the same `RequestConfirmationNumber` (e.g., `A00042`). Both records persist successfully because there is no unique database constraint on this column (see [DAT-07](#dat-07-missing-unique-constraints)). The confirmation number is the primary reference identifier communicated to patients and attorneys. Duplicate numbers cause confusion in correspondence, billing, and legal documentation.

### Recommended Fix

1. Apply a distributed lock around the read-increment-write sequence (same lock as [DAT-01](#dat-01-race-condition-on-slot-booking) if both occur in the same transaction).
2. Add a unique index on `Appointment.RequestConfirmationNumber` (see [DAT-07](#dat-07-missing-unique-constraints)) so that if the lock is ever missed, the database rejects the duplicate and the application can retry.

```csharp
await using (await _distributedLock.AcquireAsync("confirmation-number-sequence"))
{
    var nextNumber = await GenerateNextRequestConfirmationNumberAsync();
    // ... create appointment with nextNumber ...
}
```

---

## DAT-03: Reschedule Does Not Release the Old Slot

**Severity:** High
**Status:** Open

### Description

`AppointmentsAppService.UpdateAsync` and `AppointmentManager.UpdateAsync` accept a new `DoctorAvailabilityId` but do not:

1. Set the previously booked slot back to `BookingStatus.Available`.
2. Validate that the new slot is `Available`.
3. Set the new slot to `BookingStatus.Booked`.

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` line 312
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` lines 39--58

### Impact

- The old slot is permanently stuck at `Booked` and will never appear as available for other patients.
- The new slot remains `Available` and can be double-booked by another patient concurrently.
- Over time, all rescheduled slots are permanently removed from the available pool even though no appointment occupies them.

Per [docs/product/appointments.md](../product/appointments.md) (Modifications subsection), Adrian's stated intent (best-guess 2026-04-23, NEEDS CONFIRMATION) is that cancellation returns the original slot to `Available` and reschedule releases the old slot and books the new one. **Caveat: whether cancel/reschedule is in MVP scope at all is itself pending manager confirmation.** [docs/product/doctor-availabilities.md](../product/doctor-availabilities.md) Known Discrepancies explicitly classifies the older `business-domain/DOCTOR-AVAILABILITY.md` claim that `Booked -> Available` happens on cancellation as "aspirational, not observed." The integrity gap remains real, but the canonical resolution depends on the cancel/reschedule MVP-scope decision.

<!-- TODO: product-intent input needed -- pending Q1 (cancel/reschedule in MVP) and Q19 (booked-slot edit policy) in docs/product/OUTSTANDING-QUESTIONS.md. -->

### Recommended Fix

In `AppointmentManager.UpdateAsync`, before changing `appointment.DoctorAvailabilityId`:

```csharp
// 1. Release the old slot
var oldSlot = await _doctorAvailabilityRepository.GetAsync(appointment.DoctorAvailabilityId);
oldSlot.BookingStatusId = BookingStatus.Available;
await _doctorAvailabilityRepository.UpdateAsync(oldSlot);

// 2. Validate and claim the new slot
var newSlot = await _doctorAvailabilityRepository.GetAsync(newDoctorAvailabilityId);
if (newSlot.BookingStatusId != BookingStatus.Available)
    throw new UserFriendlyException(L["SlotNoLongerAvailable"]);
newSlot.BookingStatusId = BookingStatus.Booked;
await _doctorAvailabilityRepository.UpdateAsync(newSlot);

appointment.DoctorAvailabilityId = newDoctorAvailabilityId;
```

Apply the same distributed lock strategy as [DAT-01](#dat-01-race-condition-on-slot-booking).

---

## DAT-04: Non-Transactional Tenant Creation Leaves Orphaned Tenants

**Severity:** High
**Status:** Open

### Description

`DoctorTenantAppService.CreateAsync` uses two separate `UnitOfWork` blocks -- both with `isTransactional: false` -- to create the SaaS tenant and then create the Doctor profile. If the second unit of work fails (e.g., due to a validation error, database constraint, or transient network issue), the tenant record exists in the database but has no associated Doctor entity, no Doctor identity user, and no Doctor role assignment.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs`

```csharp
// First UoW: creates the ABP SaaS tenant
using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
{
    tenant = await base.CreateAsync(input);
    await uow.CompleteAsync();
}

// Second UoW: creates Doctor profile -- if this fails, tenant is orphaned
using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
{
    await CreateDoctorForTenantAsync(tenant, input);
    await uow.CompleteAsync();
}
```

### Impact

A partially created tenant cannot log in (no Doctor user), cannot be provisioned further without manual database intervention, and will appear in the tenant list as a valid tenant while being non-functional.

### Recommended Fix

Wrap both operations in a single outer transactional unit of work:

```csharp
using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
{
    tenant = await base.CreateAsync(input);
    await CreateDoctorForTenantAsync(tenant, input);
    await uow.CompleteAsync();
}
```

If ABP requires the tenant to be committed before the inner `CurrentTenant.Change()` call can access its scope, implement a compensation / rollback step: if `CreateDoctorForTenantAsync` throws, delete the tenant record before re-throwing the exception.

---

## DAT-05: Disconnected Status Representations

**Severity:** High
**Status:** Open

### Description

Two parallel representations of appointment status exist in the system and are completely disconnected from each other:

1. **`AppointmentStatusType` enum** (`Domain.Shared/Enums/AppointmentStatusType.cs`) -- used as the actual stored value on the `Appointment` entity (`appointment.AppointmentStatus`).
2. **`AppointmentStatus` domain entity** (`Domain/AppointmentStatuses/AppointmentStatus.cs`) -- a configurable lookup table with full CRUD API and Angular UI (`/appointment-statuses`), managed as reference data.

The `Appointment` entity has no foreign key to the `AppointmentStatus` table. Administrators can create, rename, or delete `AppointmentStatus` records in the UI, but these changes have zero effect on how the system stores or evaluates appointment status. Conversely, the 13 values in `AppointmentStatusType` enum are invisible to the admin UI.

### Impact

- Misleading administration UI: admins can edit "appointment statuses" believing they are configuring the system, but those edits are ignored.
- If the intention was to drive status display names from the lookup table (e.g., for localisation or customer-specific labels), that is not implemented.
- Future developers will be confused about which representation to use when building the status workflow.

### Question for Original Developer

Was `AppointmentStatus` intended to be a FK-based lookup table (replacing the enum), or was the enum always the authoritative representation and the lookup table is dead code? The answer determines whether to remove the table or wire it up as a FK.

Per [docs/product/appointments.md](../product/appointments.md) Known Discrepancies, the 13-state `AppointmentStatusType` enum is described as the authoritative status representation for both MVP and long-term intent (with an additional `AwaitingMoreInfo`-style value to add for the request/review flow). This favors Option A. Confirmation from manager / original developer still desirable before deletion.

<!-- TODO: product-intent input needed -- explicit decision on whether the configurable AppointmentStatus lookup table is intentional or dead code. -->

### Recommended Fix Options

**Option A -- Remove the lookup table:** Delete `AppointmentStatus` entity, its CRUD services, its Angular module, and its DB migration table. Use the enum exclusively. Aligns with `docs/product/appointments.md` enum-as-authoritative framing.

**Option B -- Wire up the lookup table as a FK:** Remove the `AppointmentStatusType` enum, add a FK column `AppointmentStatusId` to `Appointment`, seed the 13 values into the lookup table via a data seed contributor, and manage status labels through the admin UI.

---

## DAT-06: Missing Database Indexes on Frequently-Queried FK Columns

**Severity:** Medium
**Status:** Open

### Description

The EF Core entity configuration in `CaseEvaluationDbContext.cs` does not define `HasIndex()` calls for several FK columns that are filtered or joined in every core application query. EF Core's convention-based index creation does not cover all scenarios.

### Affected File

`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`

### Missing Indexes

| Table | Column | Used In |
|---|---|---|
| `AppAppointments` | `PatientId` | Every appointment list query, booking lookup |
| `AppAppointments` | `IdentityUserId` | Patient-facing appointment list filter |
| `AppAppointments` | `DoctorAvailabilityId` | Slot-to-appointment join |
| `AppAppointments` | `AppointmentStatus` | Status-based filtering and reporting |
| `AppDoctorAvailabilities` | `LocationId` | Primary filter in all availability queries |
| `AppDoctorAvailabilities` | `AvailableDate` | Date range queries during booking |
| `AppDoctorAvailabilities` | `BookingStatusId` | `Available` slot filter in every booking request |
| `AppAppointmentAccessors` | `AppointmentId` | Accessor lookup per appointment |
| `AppAppointmentAccessors` | `IdentityUserId` | Attorney appointment list filter |
| `AppAppointmentApplicantAttorneys` | `AppointmentId` | Child record lookup |
| `AppApplicantAttorneys` | `IdentityUserId` | Attorney-by-user lookup in external signup flow |

### Recommended Fix

Add explicit `HasIndex()` calls in the `OnModelCreating` configuration for each of the above columns:

```csharp
// Example additions to CaseEvaluationDbContext.OnModelCreating
b.HasIndex(x => x.PatientId);
b.HasIndex(x => x.IdentityUserId);
b.HasIndex(x => new { x.AvailableDate, x.BookingStatusId, x.LocationId });
```

Create an EF Core migration after adding the index definitions.

---

## DAT-07: Missing Unique Constraints

**Severity:** Medium
**Status:** Open

### Description

Two columns that function as unique business identifiers have no unique database constraint, allowing duplicates to be silently inserted.

### Affected File

`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`

### Missing Constraints

| Table | Column | Reason Uniqueness Is Required |
|---|---|---|
| `AppAppointments` | `RequestConfirmationNumber` | This is the primary business reference number communicated to patients and attorneys. Duplicates cause billing and legal documentation errors. |
| `AppPatients` | `Email` | Used as the de-facto lookup key in `GetPatientByEmailForAppointmentBookingAsync` and `GetOrCreatePatientForAppointmentBookingAsync`. Duplicate emails cause incorrect patient record merges. |

### Recommended Fix

Add unique index definitions in the EF Core configuration and create a migration:

```csharp
// In AppAppointments configuration
b.HasIndex(x => x.RequestConfirmationNumber).IsUnique();

// In AppPatients configuration
b.HasIndex(x => x.Email).IsUnique();
```

Note: the `Patient.Email` unique index should be scoped to the tenant using ABP's multi-tenancy data filter to avoid cross-tenant conflicts. Per [docs/product/patients.md](../product/patients.md), patient records are strictly tenant-scoped (the same real person at two tenants is two separate Patient records), so a tenant-scoped unique index aligns with confirmed product intent.

---

## Related Documentation

- [Issues Overview](OVERVIEW.md) -- All issues by category and severity
- [Confirmed Bugs](BUGS.md) -- Logic errors including inverted conflict detection
- [Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md) -- Slot booking lifecycle
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md) -- Status transitions
- [EF Core Design](../database/EF-CORE-DESIGN.md) -- DbContext and entity configuration
- [Domain Services](../backend/DOMAIN-SERVICES.md) -- AppointmentManager and slot management
