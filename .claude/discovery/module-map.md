# Module Map — Patient Portal
Generated: 2026-04-07 (incremental update)

## Features Inventory

15 entity features have CLAUDE.md files. 2 cross-cutting modules discovered without CLAUDE.md.

| Feature | Complexity | Has Entity | Has API | Has UI | Has Gotchas | CLAUDE.md Lines | ROI Score |
|---|---|---|---|---|---|---|---|
| Appointments | HIGH | Yes (aggregate root) | 14 endpoints | Yes (3 pages) | 3 gotchas | 187 | 5 |
| Doctors | HIGH | Yes (aggregate root) | 10 endpoints | Yes (modal) | 4 gotchas | 165 | 5 |
| DoctorAvailabilities | HIGH | Yes (aggregate root) | 11 endpoints | Yes (modal) | 4 gotchas | 161 | 5 |
| Patients | HIGH | Yes (aggregate root) | 10 endpoints | Yes (pages) | 3 gotchas | 168 | 5 |
| ApplicantAttorneys | MEDIUM | Yes (aggregate root) | 8 endpoints | Yes (modal) | 2 gotchas | 152 | 4 |
| Locations | MEDIUM | Yes (aggregate root) | 7 endpoints | Yes (modal) | 2 gotchas | 146 | 4 |
| AppointmentTypes | MEDIUM | Yes (aggregate root) | 6 endpoints | Yes (modal) | 2 gotchas | 102 | 4 |
| AppointmentAccessors | MEDIUM | Yes (entity) | 5 endpoints | No (API only) | 1 gotcha | 78 | 3 |
| AppointmentApplicantAttorneys | LOW | Yes (join entity) | 4 endpoints | No (API only) | 1 gotcha | 69 | 3 |
| AppointmentEmployerDetails | LOW | Yes (entity) | 4 endpoints | No (API only) | 1 gotcha | 74 | 3 |
| AppointmentLanguages | LOW | Yes (lookup) | 4 endpoints | Yes (modal) | 1 gotcha | 78 | 3 |
| AppointmentStatuses | LOW | Yes (lookup) | 4 endpoints | Yes (modal) | 1 gotcha | 83 | 3 |
| States | LOW | Yes (lookup) | 3 endpoints | Yes (modal) | 1 gotcha | 87 | 3 |
| WcabOffices | LOW | Yes (lookup) | 5 endpoints | Yes (modal) | 1 gotcha | 102 | 3 |
| Books | DEMO | Yes (entity) | 5 endpoints | Yes (modal) | 1 gotcha | 65 | 3 |
| **ExternalSignups** | **MEDIUM** | No (uses IdentityUser) | 4 endpoints | No (referenced by Angular) | **NEW** | — | **5** |
| **Users (Extended)** | **LOW** | No (extends ABP) | Inherits ABP | No | **NEW** | — | **3** |

## Feature Groups

### Core Scheduling
- Appointments (the central aggregate)
- DoctorAvailabilities (time slots)
- Doctors (physician profiles)
- Patients (patient demographics)

### Supporting Entities
- ApplicantAttorneys (attorney contacts)
- AppointmentAccessors (access grants)
- AppointmentApplicantAttorneys (join: appointment-attorney)
- AppointmentEmployerDetails (employer info per appointment)

### Host-Scoped Lookups
- AppointmentLanguages, AppointmentStatuses, AppointmentTypes
- Locations, States, WcabOffices

### Cross-Cutting (discovered in incremental update — no CLAUDE.md yet)
- ExternalSignups (self-registration for patients/attorneys, [AllowAnonymous], creates IdentityUser + Patient)
- Users/UserExtendedAppService (extends IdentityUserAppService, syncs Doctor on user update)

### Demo
- Books (ABP scaffolding sample — has tests)

## Section Coverage Across Feature CLAUDE.md Files

| Section | Present In | Missing From |
|---|---|---|
| File Map | 15/15 | — |
| Entity Shape | 15/15 | — |
| Multi-tenancy | 15/15 | — |
| Known Gotchas | 15/15 | — |
| Links | 15/15 | — |
| MANUAL markers | 15/15 | — |
| Relationships | 12/15 | AppointmentStatuses, Books, States |
| Mapper Configuration | 12/15 | AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentLanguages |
| Permissions | 13/15 | States, WcabOffices |
| Business Rules | 14/15 | Books |
| Inbound FKs | 10/15 | AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails, Books, WcabOffices |
| Angular UI Surface | 12/15 | AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails |
