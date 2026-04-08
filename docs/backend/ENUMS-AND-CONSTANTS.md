# Enums & Constants

[Home](../INDEX.md) > [Backend](./) > Enums & Constants

---

This page consolidates all enums and max-length constants across the codebase. For per-entity details, see the feature CLAUDE.md files linked in [Domain Model](DOMAIN-MODEL.md).

## Enums

All enums live in `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/`.

| Enum | Values | Used By |
|------|--------|---------|
| `AppointmentStatusType` | Pending(1), Approved(2), Rejected(3), NoShow(4), CancelledNoBill(5), CancelledLate(6), RescheduledNoBill(7), RescheduledLate(8), CheckedIn(9), CheckedOut(10), Billed(11), RescheduleRequested(12), CancellationRequested(13) | `Appointment.AppointmentStatus` |
| `BookingStatus` | Available(8), Booked(9), Blocked(10) | `DoctorAvailability.BookingStatusId` |
| `Gender` | Male(1), Female(2), Other(3) | `Doctor.Gender`, `Patient.GenderId` |
| `PhoneNumberType` | Work(28), Home(29) | `Patient.PhoneNumberTypeId` |
| `AccessType` | View(1), Edit(2) | `AppointmentAccessor.AccessTypeId` |
| `BookType` | Undefined(0) through Poetry(8) | `Book.Type` (demo) |
| `ExternalUserType` | Patient(1), ClaimExaminer(2), ApplicantAttorney(3), DefenseAttorney(4) | ExternalSignups registration |

## Max-Length Constants (Cross-Entity Comparison)

Constants are defined per-entity in `src/.../Domain.Shared/{Feature}/{Entity}Consts.cs`.

| Field Pattern | Appointments | Doctors | Patients | Locations | ApplicantAttorneys | WcabOffices |
|--------------|-------------|---------|----------|-----------|-------------------|-------------|
| Name/FirstName | -- | 50 | 50 | 50 | 50 | 50 |
| LastName | -- | 50 | 50 | -- | 50 | -- |
| Email | -- | 49 | 100 | -- | 100 | -- |
| Address/Street | -- | -- | 100 | 100 | 100 | 100 |
| City | -- | -- | 50 | 50 | 50 | 50 |
| ZipCode | -- | -- | 15 | 15 | 15 | 15 |
| PhoneNumber | -- | -- | 30 | -- | 50 | -- |
| PanelNumber | 50 | -- | -- | -- | -- | -- |
| Confirmation | 50 | -- | -- | -- | -- | -- |
| Comments | 250 | -- | -- | -- | -- | -- |
| Abbreviation | -- | -- | -- | -- | -- | 10 |

**Notable:** `Doctor.Email` has max length 49 (not 50 or 100) -- likely a code-generation artifact. `State` has no max-length constants.

---

**Related:**
- [Domain Model](DOMAIN-MODEL.md) -- entity index with CLAUDE.md links
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md) -- AppointmentStatusType state machine
- [Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md) -- BookingStatus lifecycle
