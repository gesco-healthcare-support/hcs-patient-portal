[Home](../INDEX.md) > [Issues](./) > Architecture & Code Quality

# Architecture & Code Quality

Seven architectural concerns and code quality issues were identified. None of these cause immediate runtime errors, but several create misleading behaviour, maintenance risk, or build technical debt that will compound as the codebase grows.

> **Test Status (2026-04-02)**: ARC-03 confirmed via B6.2 (Patient registration defaults Gender=Male, DateOfBirth=UtcNow). See [TEST-EVIDENCE.md](TEST-EVIDENCE.md).

---

## ARC-01: Vestigial Books Entity from ABP Scaffold

**Severity:** Medium
**Status:** Open

### Description

The ABP Framework project template includes a `Books` tutorial entity to demonstrate the DDD pattern. This scaffold was never removed from the codebase. The following artefacts all exist in a production healthcare application:

| Artefact | Location |
|---|---|
| `Book.cs` entity | `src/.../Domain/Books/Book.cs` |
| `BookType.cs` enum (Dystopia, ScienceFiction, etc.) | `src/.../Domain.Shared/Books/BookType.cs` |
| `IBookAppService`, `BookDto`, `BookCreateUpdateDto` | `src/.../Application.Contracts/Books/` |
| `BookAppService.cs` | `src/.../Application/Books/` |
| `BookController.cs` | `src/.../HttpApi/Controllers/` |
| `BookStoreDataSeederContributor.cs` (seeds "1984" and "The Hitchhiker's Guide") | `src/.../Domain/` |
| `EfCoreBookRepository.cs` | `src/.../EntityFrameworkCore/Books/` |
| Angular proxy (`proxy/books/`) | `angular/src/app/proxy/books/` |
| `BookAppService_Tests`, `EfCoreBookAppService_Tests` | `test/` |

The only items missing are an Angular feature module and UI -- confirming this was never intended to be part of the application.

### Impact

- The `BookStoreDataSeederContributor` runs on every `DbMigrator` execution and inserts book records into the production database.
- The `BookController` exposes live API endpoints (`GET/POST/PUT/DELETE /api/app/books`) in production.
- New developers reading the codebase waste time understanding whether `Books` is a real domain concept.
- The test suite's 3 `BookAppService` tests inflate the apparent test count without covering any real business logic.

### Recommended Fix

Delete all `Books`-related files across every layer, remove the data seed contributor registration, create a migration to drop the `AppBooks` table, and regenerate the Angular proxy. This is a safe, mechanical removal with no business impact.

---

## ARC-02: Business Logic in the Application Service Layer, Not Domain

**Severity:** Medium
**Status:** Open

### Description

`AppointmentsAppService` performs business rule enforcement that belongs in the domain layer (`AppointmentManager`). The application service is responsible for orchestration (calling domain services, mapping DTOs, authorising requests), not for encoding rules.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`

### Misplaced Logic

| Logic | Current Location | Should Be In |
|---|---|---|
| Slot availability check (`BookingStatus.Available` guard) | `AppointmentsAppService.CreateAsync` lines 219--224 | `AppointmentManager.CreateAsync` |
| FK existence validation (PatientId, LocationId, AppointmentTypeId, etc.) | `AppointmentsAppService.CreateAsync` lines 228--244 | `AppointmentManager.CreateAsync` |
| Confirmation number generation | `AppointmentsAppService.GenerateNextRequestConfirmationNumberAsync` lines 254--281 | `AppointmentManager` or a dedicated domain service |
| Slot release on cancel (currently absent -- see [DAT-03](DATA-INTEGRITY.md#dat-03-reschedule-does-not-release-the-old-slot)) | Should be in `AppointmentManager.UpdateAsync` | `AppointmentManager.UpdateAsync` |

### Impact

- Domain rules can be bypassed by calling the repository directly (e.g., in tests or other services).
- `AppointmentsAppService` injects 9 separate repositories, making it difficult to test and reason about.
- When the status workflow ([FEAT-01](INCOMPLETE-FEATURES.md#feat-01-appointment-status-workflow-has-no-implementation)) is built, there is no canonical place to enforce rules -- both the service and manager are incomplete.

### Recommended Fix

Move FK validations and slot business rules into `AppointmentManager.CreateAsync` and `AppointmentManager.UpdateAsync`. The application service should only resolve the DTO's Guid lookups, call the manager, and return the mapped result.

---

## ARC-03: Hardcoded Placeholder Values for Gender and Date of Birth

**Severity:** High
**Status:** Open

### Description

Two separate code paths default required patient demographic fields to placeholder values that are never valid:

**1. External signup registration:**

`src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` lines 208--221:

```csharp
await _patientManager.CreateAsync(
    ...,
    genderId: Gender.Male,              // hardcoded -- every patient registers as Male
    dateOfBirth: DateTime.UtcNow.Date,  // hardcoded -- every patient's DOB is today
    ...
);
```

The external signup DTO (`ExternalUserSignUpDto`) does not include `Gender` or `DateOfBirth` fields, so patients cannot provide this information at registration.

**2. Doctor / tenant creation:**

`src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs`:

```csharp
gender: Gender.Male  // hardcoded -- every doctor is created as Male
```

### Impact

- Every patient in the database has `Gender = Male` and `DateOfBirth = <registration date>` regardless of their actual demographics.
- Every doctor has `Gender = Male` regardless of their actual gender.
- These fields appear in reports, forms, and correspondence. Incorrect demographics in a workers' compensation medical context has legal implications.
- A database containing hundreds of patients will require a data migration to correct.

### Recommended Fix

1. Add `Gender` and `DateOfBirth` fields to `ExternalUserSignUpDto` with appropriate validation attributes.
2. Collect the values in the Angular external signup form (`angular/src/app/home/` sign-up flow).
3. Pass the collected values through to `ExternalSignupAppService.RegisterAsync`.
4. For the Doctor, add `Gender` to the tenant creation input DTO or derive it from the provided user profile.
5. After deploying the fix, run a data migration or present existing users with a profile completion prompt.

---

## ARC-04: Role Name Strings Duplicated Across 8+ Files With No Shared Constant

**Severity:** Medium
**Status:** Open

### Description

The role name strings `"Patient"`, `"Applicant Attorney"`, `"Defense Attorney"`, `"Doctor"`, and `"Claim Examiner"` are hardcoded as string literals in at least 8 separate files across both the backend and Angular frontend. There is no shared constant or enum value used consistently.

### Known Occurrences

| File | Strings Used |
|---|---|
| `ExternalSignupAppService.cs` lines 64--69 | `"Patient"`, `"Applicant Attorney"`, `"Defense Attorney"` |
| `ExternalSignupAppService.cs` line 239 (local `ToRoleName`) | All four external role names |
| `DoctorTenantAppService.cs` | `"Doctor"` |
| `ExternalUserRoleDataSeedContributor.cs` | All four external role names |
| `appointment-view.component.ts` lines 215--216 | `"patient"`, `"applicant attorney"` |
| `appointment-add.component.ts` lines 276--278 | `"patient"` |
| `home.component.ts` | All external role names |
| `app.component.ts` | Role names for menu visibility |

### Impact

A typo in any one location silently breaks role-based logic. A role rename requires finding and updating every hardcoded occurrence. The `ToRoleName(ExternalUserType)` helper in `ExternalSignupAppService` is not accessible to `DoctorTenantAppService` or any Angular code.

### Recommended Fix

**Backend:** Add a static class `CaseEvaluationRoleNames` (in `Domain.Shared`) with `public const string Patient = "Patient"` etc., mirroring the pattern used by ABP's own `IdentityRoles` class.

**Frontend:** Add a `roleNames` constant object to a shared file in `angular/src/app/shared/` and import it in every component that branches on role names.

---

## ARC-05: AppointmentAddComponent Is Eagerly Loaded

**Severity:** Low
**Status:** Open

### Description

`AppointmentAddComponent` is statically imported at the top of `app.routes.ts`, which causes it to be included in the root Angular bundle regardless of whether the user navigates to the booking page.

### Affected File

`angular/src/app/app.routes.ts` lines 13--14:

```typescript
// Static import -- included in root bundle for all users
import { AppointmentAddComponent } from './appointments/appointment-add.component';

// Route definition wraps it in Promise.resolve() but the damage is done at import time
{ path: 'appointments/add', loadComponent: () => Promise.resolve(AppointmentAddComponent), ... }
```

`AppointmentAddComponent` is the largest component in the application (~1,400 lines). All other components use genuine lazy loading:

```typescript
// Correct lazy-loading pattern used by all other routes
{ path: 'appointments/view/:id', loadComponent: () => import('./appointments/...').then(m => m.AppointmentViewComponent) }
```

### Impact

Every page load -- including the anonymous login page -- downloads and parses the appointment booking component's JavaScript. For unauthenticated users and admin users who never book appointments, this is wasted bandwidth and parse time.

### Recommended Fix

Change the import and route definition to use the lazy pattern:

```typescript
// Remove the static import at the top of the file

// Change the route to:
{ path: 'appointments/add', loadComponent: () => import('./appointments/appointment-add.component').then(m => m.AppointmentAddComponent), canActivate: [authGuard] }
```

---

## ARC-06: DTO Validation Attributes Missing on Availability Input DTOs

**Severity:** Low
**Status:** Open

### Description

`DoctorAvailabilityGenerateInputDto`, `DoctorAvailabilityDeleteBySlotInputDto`, and `DoctorAvailabilityDeleteByDateInputDto` carry no `[Required]` or `[Range]` validation attributes on any of their fields. All validation is deferred to runtime checks inside the application service methods.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityGenerateInputDto.cs`

```csharp
public class DoctorAvailabilityGenerateInputDto
{
    public DateOnly FromDate { get; set; }      // no [Required]
    public DateOnly ToDate { get; set; }        // no [Required]
    public TimeOnly FromTime { get; set; }      // no [Required]
    public TimeOnly ToTime { get; set; }        // no [Required]
    public Guid LocationId { get; set; }        // no [Required]
    public int AppointmentDurationMinutes { get; set; }  // no [Range]
}
```

Compare with `ExternalUserSignUpDto` which uses `[Required]` and `[EmailAddress]` consistently.

### Impact

- The generated OpenAPI / Swagger schema marks all fields as optional, misleading API consumers and the Swagger UI.
- ABP's model-level validation pipeline never fires for these DTOs; if service-layer guards are missed, null or zero values reach the database.
- A `[Range(min: 1, max: 480)]` on `AppointmentDurationMinutes` would prevent accidentally generating thousands of slots for a 0-minute duration.

### Recommended Fix

Add data annotation attributes to all three DTOs, following the pattern in `ExternalUserSignUpDto`:

```csharp
[Required]
public DateOnly FromDate { get; set; }

[Range(1, 480, ErrorMessage = "Duration must be between 1 and 480 minutes.")]
public int AppointmentDurationMinutes { get; set; }
```

---

## ARC-07: Hardcoded English Strings in User-Visible Messages Bypass Localisation

**Severity:** Low
**Status:** Open

### Description

User-facing status and error messages in `AppointmentAddComponent` and `DoctorAvailabilitiesAppService` are hardcoded English strings. They are displayed directly without passing through the Angular localisation pipe or the ABP `L["Key"]` accessor, making them impossible to translate without code changes.

### Affected Files

**Angular -- `angular/src/app/appointments/appointment-add.component.ts`:**

```typescript
// Lines 299, 314, 682, 687, 690, 741, 744 -- raw English strings assigned to bound template variables
this.patientLoadMessage = 'To create a new patient, First Name, Last Name, Email and Date of Birth are required.';
this.patientLoadMessage = 'Patient loaded. You can edit details below if needed.';
this.patientLoadMessage = 'No patient found with this email. Fill in the form below to create a new patient.';
```

**Backend -- `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs`:**

```csharp
// Lines 177, 181, 185, 300, 305 -- UserFriendlyException with raw English strings
throw new UserFriendlyException("From date cannot be in the past.");
throw new UserFriendlyException("From time must be before To time.");
```

All other `UserFriendlyException` calls in the same file correctly use `L["LocalisationKey"]`.

### Impact

These messages will always appear in English regardless of the user's locale setting. The rest of the application supports 20 locales via ABP's localisation system; these strings are the exception.

### Recommended Fix

**Angular:** Use the `LocalizationService` to resolve keys:

```typescript
this.patientLoadMessage = this.localizationService.instant('::PatientLoaded');
```

**Backend:** Add the missing keys to the localisation JSON files and use the `L` accessor:

```csharp
throw new UserFriendlyException(L["FromDateCannotBeInThePast"]);
```

---

## ARC-08: Missing [RemoteService(IsEnabled = false)] on 3 AppServices

**Severity:** Medium
**Status:** Open

### Description

Per [ADR-002](../decisions/002-manual-controllers-not-auto.md), every AppService in this project should carry `[RemoteService(IsEnabled = false)]` to prevent ABP from auto-generating duplicate routes alongside the manual controllers in `HttpApi/`. A code audit on 2026-04-13 found 3 of 18 concrete AppServices missing this attribute.

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs`
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs`
- `src/HealthcareSupport.CaseEvaluation.Application/Users/UserExtendedAppService.cs`

(`CaseEvaluationAppService.cs` is the abstract base class and is correctly excluded.)

### Impact

ABP may register auto-controllers for these services, creating duplicate endpoint registrations alongside the manual controllers in `HttpApi/Controllers/`. Clients routing through the auto-generated URL will bypass the manual controller's route conventions and any future customizations (serialization, authorization overrides, versioning).

### Recommended Fix

Add `[RemoteService(IsEnabled = false)]` to each affected class. Zero behavior change is expected for consumers of the manual controllers; the attribute simply prevents the auto-registration.

```csharp
[RemoteService(IsEnabled = false)]
public class DoctorTenantAppService : CaseEvaluationAppService, IDoctorTenantAppService
{
    // ...
}
```

### Related

- [ADR-002: Manual Controllers, Not Auto](../decisions/002-manual-controllers-not-auto.md)
- [Application Layer CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Application/CLAUDE.md)

---

## Related Documentation

- [Issues Overview](OVERVIEW.md) -- All issues by category and severity
- [DDD Layers](../architecture/DDD-LAYERS.md) -- Domain vs Application layer responsibilities
- [Application Services](../backend/APPLICATION-SERVICES.md) -- AppService patterns and conventions
- [Component Patterns](../frontend/COMPONENT-PATTERNS.md) -- Angular feature module structure
- [Routing & Navigation](../frontend/ROUTING-AND-NAVIGATION.md) -- Lazy loading and route guards
