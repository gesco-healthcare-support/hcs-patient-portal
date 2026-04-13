[Home](../INDEX.md) > [Issues](./) > Incomplete Features

# Incomplete Features

Seven features are either entirely missing, present only as placeholders, or wired up in the backend with no corresponding frontend. These represent the primary functional gaps between the current codebase and a production-ready application.

> **Test Status (2026-04-02)**: FEAT-01 confirmed via B11.1.1 (no status transition mechanism; all 13 statuses accepted at creation but immutable afterward). FEAT-02 confirmed via B11.6.1 (Claim Examiner has no specific endpoints or UI). See [TEST-EVIDENCE.md](TEST-EVIDENCE.md).

---

## FEAT-01: Appointment Status Workflow Has No Implementation

**Severity:** High
**Status:** Open

### Description

`AppointmentStatusType` defines 13 distinct statuses representing the full lifecycle of a workers' comp IME appointment:

```
Pending, Approved, Rejected, NoShow, CancelledNoBill, CancelledLate,
RescheduledNoBill, RescheduledLate, CheckedIn, CheckedOut, Billed,
RescheduleRequested, CancellationRequested
```

None of the following exist in the current codebase:

- A state machine defining which transitions are valid (e.g., `Pending → Approved`, not `Pending → Billed`)
- Server-side enforcement of valid transitions
- Role-based permission checks on who can trigger which transition (e.g., only a Doctor can approve; only an Admin can mark Billed)
- Any API endpoint or application service method for transitioning status
- Any Angular UI for viewing the current status or triggering a transition

The only place status is set is at appointment creation time via `AppointmentCreateDto.AppointmentStatus`, which has no validation -- any user can create an appointment with any status. The status field is then frozen (see [BUG-02](BUGS.md#bug-02-appointment-status-changes-are-never-persisted)).

### What Needs to Be Built

1. A state machine definition (valid transitions and permitted roles per transition).
2. A `TransitionAppointmentStatusAsync(Guid appointmentId, AppointmentStatusType newStatus)` domain service method on `AppointmentManager` that enforces the transition rules.
3. A corresponding application service method and API endpoint (`POST /api/app/appointments/{id}/status`).
4. Angular UI components to display the current status and trigger transitions appropriate to the current user's role.
5. Fix `AppointmentUpdateDto` to include `AppointmentStatus` (see [BUG-02](BUGS.md#bug-02-appointment-status-changes-are-never-persisted)).

### Open Question

What is the intended transition graph? Who can approve, reject, check in, check out, and bill? This must be answered before implementation can begin (see [OVERVIEW.md Open Questions #1](OVERVIEW.md#open-questions)).

---

## FEAT-02: Claim Examiner Has No UI or Workflow

**Severity:** High
**Status:** Open

### Description

`ExternalUserType.ClaimExaminer` (value `2`) is seeded as a role on every tenant via `ExternalUserRoleDataSeedContributor`. A Claim Examiner can register via the external signup flow. However:

- There is no distinct dashboard, menu, or home page for the Claim Examiner role.
- The `HomeComponent` role-branching logic treats Claim Examiner identically to an unauthenticated user -- it falls through to the default case and renders the same view as a Patient.
- There are no permission assignments specific to Claim Examiner in `CaseEvaluationPermissions`.
- No business logic references the Claim Examiner role after registration.

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/ExternalSignups/ExternalUserType.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs`
- `angular/src/app/home/home.component.ts`

### What Needs to Be Built

1. Define what a Claim Examiner can and cannot do in the system (product decision).
2. Add Claim Examiner-specific permissions to `CaseEvaluationPermissions`.
3. Add role detection in `HomeComponent` (alongside the existing `isPatientUser` and `isAttorneyUser` checks).
4. Build the Claim Examiner view/dashboard.

### Open Question

Is Claim Examiner a future persona (planned but not yet designed) or is it expected to be functional? See [OVERVIEW.md Open Questions #7](OVERVIEW.md#open-questions).

---

## FEAT-03: Tenant Dashboard Is a Placeholder

**Severity:** Medium
**Status:** Open

### Description

The tenant-level dashboard component contains no real content. Its HTML template is a single placeholder string and its TypeScript class body is empty.

### Affected Files

- `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.html`

```html
Add your Tenant related charts/widgets to this page !
```

- `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.ts`

```typescript
ngOnDestroy(): void {}
// No other implementation
```

The host dashboard (`host-dashboard.component`) is fully implemented with ABP commercial audit and SaaS widgets (error rate, execution duration, edition usage, latest tenants). The tenant (Doctor) dashboard has no equivalent.

### What Needs to Be Built

Typical dashboard widgets for a Doctor tenant would include:

- Today's appointments (count and list)
- Upcoming availability slots
- Recent appointment status changes (e.g., newly requested reschedules or cancellations)
- Appointment volume over time (chart)

The ABP commercial widget system (`@volo/abp.ng.account`) and `WidgetService` are already available in the Angular project.

---

## FEAT-04: AppointmentEmployerDetail and AppointmentAccessor Have No Angular Modules

**Severity:** Medium
**Status:** Open

### Description

Both `AppointmentEmployerDetail` and `AppointmentAccessor` have complete backend implementations:

- Domain entity, manager, repository interface, EF Core repository
- Application service (full CRUD)
- HTTP API controller
- DTOs and permissions
- ABP-generated Angular proxy services (under `angular/src/app/proxy/`)

Neither has an Angular feature module directory in `angular/src/app/`. There is no standalone list page, no detail modal, and no route defined for either entity.

`AppointmentApplicantAttorney` is in a similar state but is partially managed inline via `AppointmentController.UpsertApplicantAttorneyForAppointment` and is accessible through the `AppointmentViewComponent`. `AppointmentEmployerDetail` and `AppointmentAccessor` have no Angular entry point at all.

### What Needs to Be Built

For each entity, either:

**Option A -- Standalone management page:** Create an Angular feature module following the existing ABP Suite pattern (see [Component Patterns](../frontend/COMPONENT-PATTERNS.md)) with a list page, create/edit modal, and route registration.

**Option B -- Inline within AppointmentViewComponent:** Embed management of these child entities directly in the appointment detail view, similar to how `AppointmentApplicantAttorney` is handled today.

Option B is more consistent with the UX pattern already established for this application, where employer details and accessors are naturally scoped to a single appointment.

---

## FEAT-05: Email System Is Not Wired Up

**Severity:** Medium
**Status:** Open

### Description

`Volo.Abp.Emailing` is included in the domain module, but in `DEBUG` mode the real email sender is replaced with `NullEmailSender`:

```csharp
// CaseEvaluationDomainModule.cs
#if DEBUG
context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
```

No email templates, no trigger points for sending emails, and no SMTP configuration exist in any `appsettings.json`. The ABP `TextTemplateManagement` module is installed (which is typically used for email templates), but no custom templates have been created.

### Impact

Users receive no email confirmation when they register, no notification when their appointment is approved or rejected, and no reminder before their appointment date. For a workers' comp scheduling platform, appointment confirmation emails are likely a compliance requirement.

### What Needs to Be Built

1. Configure an SMTP provider (or Azure Communication Services / SendGrid) in `appsettings.json` for non-DEBUG environments.
2. Remove or guard the `#if DEBUG` `NullEmailSender` replacement so staging and production environments use real email.
3. Create email templates using ABP's `TextTemplateManagement` module for:
   - External user registration confirmation
   - Appointment booking confirmation (sent to patient and attorney)
   - Appointment status change notifications (approved, rejected, cancelled)
4. Add email-sending calls at appropriate points in `ExternalSignupAppService` and the appointment status workflow (once [FEAT-01](#feat-01-appointment-status-workflow-has-no-implementation) is implemented).

---

## FEAT-06: No CI/CD Pipeline

**Severity:** Medium
**Status:** Open

### Description

The repository contains Docker Compose files, Dockerfiles, and a Helm chart directory (`etc/helm/`), but no automated build or deployment pipeline exists.

### What Exists

- `etc/docker-compose/docker-compose.yml` -- Full stack for local Docker deployment (Angular nginx, HttpApi.Host, AuthServer, DbMigrator, SQL Server, Redis)
- `Dockerfile` and `Dockerfile.local` for each deployable service
- `etc/helm/` -- Kubernetes Helm chart skeleton
- `scripts/build-images-locally.ps1` -- Manual local image build helper

### What Is Missing

- `.github/workflows/` -- No GitHub Actions workflows
- `azure-pipelines.yml` -- No Azure DevOps pipeline
- `Jenkinsfile` -- No Jenkins pipeline
- Any automated trigger to build, test, and deploy on push

### What Needs to Be Built

A minimal CI/CD pipeline should:

1. On every pull request: restore, build, and run the 13 existing backend tests.
2. On merge to `main`: build Docker images, push to a container registry, and deploy to a target environment.
3. Manage secrets via the chosen platform's secret store (not committed files -- see [SEC-01](SECURITY.md#sec-01-secrets-committed-to-source-control)).

Open question: What is the target deployment platform (Azure, AWS, on-prem)? See [OVERVIEW.md Open Questions #3](OVERVIEW.md#open-questions).

---

## FEAT-07: Near-Zero Test Coverage

**Severity:** Medium
**Status:** Open

### Description

The test suite contains 13 unique test methods across the entire application. Only the `Doctors` entity and the vestigial `Books` entity have any test coverage. Every core feature -- appointment booking, patient management, doctor availability generation, external signup, and tenant creation -- is completely untested.

### Current Coverage

| Entity / Feature | Backend Tests | Angular Tests |
|---|---|---|
| Doctors | 5 methods | None |
| Books (scaffold, not used) | 3 methods | None |
| Appointments | 0 | None |
| Patients | 0 | None |
| Doctor Availabilities | 0 | None |
| External Signup | 0 | None |
| Tenant / Doctor Creation | 0 | None |
| All other entities | 0 | None |

### What Needs to Be Built

**Priority 1 -- Application service tests** (highest value given the existing test infrastructure):
- `AppointmentsAppServiceTests` -- booking, slot availability validation, confirmation number generation
- `DoctorAvailabilitiesAppServiceTests` -- slot generation, conflict detection, preview
- `ExternalSignupAppServiceTests` -- registration flow, role assignment, email uniqueness

**Priority 2 -- Domain service tests:**
- `AppointmentManagerTests` -- status transition validation (once [FEAT-01](#feat-01-appointment-status-workflow-has-no-implementation) is implemented)

**Priority 3 -- Angular component tests:**
- `AppointmentAddComponent` -- multi-step form validation, slot selection, patient lookup
- `HomeComponent` -- role-based routing logic

The existing `TestBase` project provides the necessary infrastructure. Refer to [Testing Strategy](../devops/TESTING-STRATEGY.md) for patterns.

---

## Related Documentation

- [Issues Overview](OVERVIEW.md) -- All issues by category and severity
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md) -- Intended status model
- [User Roles & Actors](../business-domain/USER-ROLES-AND-ACTORS.md) -- Role definitions
- [Component Patterns](../frontend/COMPONENT-PATTERNS.md) -- How to build new Angular feature modules
- [Testing Strategy](../devops/TESTING-STRATEGY.md) -- Test infrastructure and patterns
- [Docker & Deployment](../runbooks/DOCKER-DEV.md) -- Docker Compose development setup
