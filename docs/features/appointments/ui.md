<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md on 2026-04-08 -->

# Appointments — UI

> Synced from feature CLAUDE.md. Update code-derived content there.

## Angular Component Architecture

Two parallel UI flows exist for appointment management:

### Flow 1: Full-page Booking (`appointment-add.component`)
- **Route:** `/appointments/add`
- **Purpose:** Multi-section form for new appointment booking by patients, attorneys, or staff
- **Sections:** Appointment details (type, location, date/time picker), Patient demographics, Employer details, Applicant attorney (toggleable), Authorized users (inline table + modal)
- **Forms:** Reactive `FormBuilder` with ~30 controls
- **Key behavior:**
  - Slot picker: fetches `DoctorAvailability` filtered by location + type + `BookingStatus.Available`
  - 3-day minimum booking rule enforced in datepicker (`markAppointmentDateDisabled`)
  - Available dates highlighted with `available-day` CSS class on custom day template
  - Role-based branching: `isExternalUserNonPatient` shows patient search/select; `isApplicantAttorney` auto-loads attorney details
  - On submit: creates patient (if needed) → creates appointment → creates employer details → upserts attorney → creates accessors (sequential chain)

### Flow 2: Admin List + Modal (`appointment.component` + `appointment-detail.component`)
- **Route:** `/appointments` (list), modal overlay for create/edit
- **Purpose:** Grid view with advanced filters and quick CRUD via modal
- **List component:** Extends `AbstractAppointmentComponent` — ngx-datatable with columns for panel number, date, confirmation, status, patient, type, location
- **Modal component:** `AppointmentDetailViewService` manages form state with `FormBuilder`
- **Permission checks:** `*abpPermission` on Create/Edit/Delete actions; `isActionButtonVisible` computed from Edit OR Delete grants

### Flow 3: View/Edit Page (`appointment-view.component`)
- **Route:** `/appointments/view/:id`
- **Purpose:** Read/edit existing appointment with full patient, employer, attorney, and accessor management
- **Forms:** Plain objects with `ngModel` two-way binding (inconsistent with Flows 1 and 2)
- **Key behavior:** Loads appointment with nav props, then separately loads employer details, attorney, and accessors. Save is sequential: patient → appointment → employer → attorney.

## Routes

| Path | Component | Guards | Notes |
|---|---|---|---|
| `/appointments` | `AppointmentComponent` | `authGuard`, `permissionGuard` | List page with advanced filters |
| `/appointments/add` | `AppointmentAddComponent` | (registered in `app.routes.ts`) | Full booking form |
| `/appointments/view/:id` | `AppointmentViewComponent` | `authGuard` only | **No permissionGuard** — any authenticated user can access |

## Services

| Service | Scope | Purpose |
|---|---|---|
| `AppointmentViewService` | Component-provided | List data management — hookToQuery, delete with confirmation |
| `AppointmentDetailViewService` | Component-provided | Modal form state — build form, create/update submission |
| `AppointmentService` (proxy) | Root singleton | Auto-generated REST client for `/api/app/appointments` |

Both view services extend abstract base classes (`AbstractAppointmentViewService`, `AbstractAppointmentDetailViewService`) for code reuse.

## ABP Patterns Used

- `abp-page`, `abp-page-toolbar-container` — page layout
- `abp-advanced-entity-filters` — collapsible filter panel
- `abp-lookup-select` — dropdown lookups for FKs (type, location, patient, user, availability, state, language)
- `abp-modal` with `[busy]` and `[(visible)]` — modal lifecycle
- `*abpPermission` — conditional rendering by permission
- `abpLocalization` pipe — all display text
- `abpUtcToLocal` pipe — date display
- `ListService` — pagination/sorting/filtering hook
- `ConfirmationService` — delete confirmation dialog
- `authGuard` / `permissionGuard` — route protection
- `provideAppInitializer` + `RoutesService` — menu registration

## Menu Configuration

Defined in `appointment-base.routes.ts`:
- Path: `/appointments`
- Name: `::Menu:Appointments` (localized)
- Icon: `fas fa-file-alt`
- Required policy: `CaseEvaluation.Appointments`
- Layout: `eLayoutType.application`

## Known UI Issues

1. **`console.log` in production** — `appointment-add.component.ts` ~line 1413 (date check debug logging)
2. **`requestConfirmationNumber`** — hardcoded default `"A"` on line 183; server overrides anyway
3. **Form inconsistency** — add page uses reactive forms, view/edit page uses ngModel
4. **No permission guard on view route** — `/appointments/view/:id` only checks authentication, not appointment-level authorization

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
