---
feature: master-data-crud
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\AppointmentTypeDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\LocationDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\WcabOfficeDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DocumentModule\AppointmentDocumentTypeDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\DocumentDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\DoctorManagement\{AppointmentTypes,Locations,WcabOffices}Controller.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Document\AppointmentDocumentTypesController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\DocumentManagement\DocumentsController.cs
old-docs:
  - data-dictionary-table.md (AppointmentTypes, Locations, WcabOffices, AppointmentDocumentTypes, Documents, Languages, City, States, Countries)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ITAdmin (master data); Languages may be partly seeded
depends-on: []
required-by:
  - external-user-appointment-request           # all booking dropdowns read these
  - it-admin-package-details                    # links Documents to Packages
---

# Master data CRUD (combined audit)

## Purpose

Auxiliary lookup tables that drive dropdowns + lookups across the app. Each table follows standard CRUD with IT Admin permission gate. Combining into one audit since they share patterns.

**Strict parity with OLD.**

## Tables in scope

| Table | OLD Domain | OLD Controller | Used by |
|-------|------------|----------------|---------|
| `AppointmentTypes` | `AppointmentTypeDomain.cs` | `/api/AppointmentTypes` | Booking type dropdown; doctor preferences; system parameters max-time per type |
| `Locations` | `LocationDomain.cs` | `/api/Locations` | Booking location dropdown; doctor preferences |
| `WcabOffices` | `WcabOfficeDomain.cs` | `/api/WcabOffices` | Injury detail WCAB lookup |
| `AppointmentDocumentTypes` | `AppointmentDocumentTypeDomain.cs` | `/api/AppointmentDocumentTypes` | Document type tagging (e.g., "Medical History", "Auth", "ID") |
| `Documents` | `DocumentDomain.cs` | `/api/Documents` | Master document templates (forms in packages) |
| `Languages` | -- | `/api/AppointmentRequestLookups/languagelookups` | Patient language preference |
| `City`, `States`, `Countries` | -- | `/api/AppointmentRequestLookups/citylookups`, `stateslookups` | Address dropdowns |

## OLD behavior (binding)

### Common pattern

All master tables share the same shape:

- Standard CRUD: GET (list + by-id), POST (create), PUT/PATCH (update), DELETE (soft delete via StatusId).
- Search controllers (POST /search) for advanced filtering.
- Lookup endpoints (GET) returning trimmed data for dropdowns.
- Permission: IT Admin only for write; any authenticated user for read.

### Per-table specifics

#### `AppointmentTypes` (5 entries)

- `AppointmentTypeId, AppointmentTypeName, Description, StatusId, ReEvalId` (FK self-reference for "this type's REVAL counterpart")
- Seed: PQME (1), AME (2), PQME-REVAL (3), AME-REVAL (4), OTHER (5)
- `ReEvalId` links PQME -> PQME-REVAL and AME -> AME-REVAL

#### `Locations`

- `LocationId, LocationName, Address, City, State (FK), ZipCode, ParkingFee (decimal 5,2), StatusId, AppointmentTypeId (nullable -- default appt type for location), audit fields`
- `ParkingFee` is unusual -- may be displayed to users for fee transparency

#### `WcabOffices`

- `WcabOfficeId, WcabOfficeName, WcabOfficeAbbreviation, Address, City, StateId, ZipCode, StatusId`
- California Workers' Compensation Appeals Board offices; reference data

#### `AppointmentDocumentTypes`

- `AppointmentDocumentTypeId, DocumentTypeName, StatusId`
- Categorizes document types (e.g., "Medical Records", "Authorization", "Identification")

#### `Documents`

- `DocumentId, DocumentName, DocumentFilePath, StatusId`
- The blank-form templates emailed in package documents
- File stored on disk; path in `DocumentFilePath`

#### `Languages`, `City`, `States`, `Countries`

- Standard reference data
- `Languages` has `LanguageCode` (varchar 2 -- ISO 639-1), `Active`, `AutoTranslate` flags
- `Countries` has format settings (DateFormat, CurrencyFormat, PhoneFormat) per country

### Critical OLD behaviors

- **Soft delete via StatusId** preserves referential integrity for historical records.
- **Active filter on lookups** -- only `StatusId == Active` rows returned for dropdowns.
- **`ReEvalId` self-reference** on AppointmentTypes drives REVAL flow logic (booking audit).

## OLD code map

(Listed in `old-source` frontmatter above.)

## NEW current state

- NEW has folders for: `AppointmentTypes/`, `Locations/`, `Doctors/`, `ApplicantAttorneys/`, `DefenseAttorneys/`, etc.
- TO VERIFY: WCAB Offices, Languages, Cities, States, Countries entities + AppServices.
- Likely some are already implemented (e.g., AppointmentTypes is core; Locations).

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `AppointmentType` 5 entries seeded incl OTHER + ReEvalId links | OLD | TO VERIFY | **Verify NEW seed includes 5 types + ReEvalId FK on entity** | B |
| `Location` with ParkingFee + AppointmentTypeId | OLD | NEW: TO VERIFY ParkingFee | **Add `ParkingFee decimal(5,2)?` if missing** | I |
| `WcabOffices` master CRUD | OLD | TO VERIFY | **Verify entity + AppService + seed (TO LOAD California WCAB office list)** | B |
| `AppointmentDocumentType` master | OLD | TO VERIFY | **Verify entity + CRUD; seed common types** | I |
| `Document` master CRUD | OLD | NEW partial via `AppointmentDocumentsAppService` | **Verify standalone Document master entity exists** (separate from per-appointment AppointmentDocument) | B |
| `Language` master | OLD | TO VERIFY | **Add entity + seed common languages (English, Spanish, Mandarin, Cantonese, Vietnamese, Korean -- common in CA workers' comp)** | I |
| `Country, State, City` masters | OLD | NEW: ABP defaults? TO VERIFY | **Use ABP `RegionInfo` + custom seed for California-specific cities/states** | I |
| Soft delete | OLD `StatusId` | ABP `ISoftDelete` | None | -- |
| Permissions: IT Admin write, any-auth read | OLD | -- | **Add `CaseEvaluation.{Locations, WcabOffices, AppointmentDocumentTypes, Documents, Languages}.{Default, Create, Edit, Delete}`** | I |
| Lookup endpoints (active-only filter) | OLD | NEW per `Appointments/CLAUDE.md` has lookup methods | **Verify each lookup endpoint filters by IsActive = true** | I |

## Internal dependencies surfaced

- `it-admin-package-details.md` references `Document` master.
- Booking form lookups read from these.

## Branding/theming touchpoints

- Master data UI screens (logo, primary color, table styling).

## Replication notes

### ABP wiring

- Standard ABP entity + AppService + manual controller pattern per branch CLAUDE.md.
- Each entity: `IsActive` flag + `ISoftDelete`.
- Lookup endpoints in respective lookup controllers (already partly per `Appointments/CLAUDE.md`).
- Seed data via `IDataSeedContributor` per entity:
  - `AppointmentTypeDataSeedContributor`: seed 5 types with ReEvalId links
  - `WcabOfficeDataSeedContributor`: seed California WCAB offices (use a CSV import for the canonical list)
  - `AppointmentDocumentTypeDataSeedContributor`: seed common document types
  - `LanguageDataSeedContributor`: seed common languages

### Verification

1. IT Admin opens AppointmentTypes -> sees 5 types incl OTHER
2. Same for Locations, WcabOffices, etc.
3. External user books -> dropdowns populated from active rows only
4. IT Admin adds new Location -> visible in booking form
5. IT Admin soft-deletes -> not in dropdown but historical appointments still reference it
