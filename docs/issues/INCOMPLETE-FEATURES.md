[Home](../INDEX.md) > [Issues](./) > Incomplete Features

<!-- Last reorganized 2026-04-24 against docs/product/ + docs/gap-analysis/ -->

# Incomplete Features

Eight features are either entirely missing, present only as placeholders, or wired up in the backend with no corresponding frontend. These represent the primary functional gaps between the current codebase and a production-ready application.

> **Test Status (2026-04-02)**: FEAT-01 confirmed via B11.1.1 (no status transition mechanism; all 13 statuses accepted at creation but immutable afterward). FEAT-02 confirmed via B11.6.1 (Claim Examiner has no specific endpoints or UI). See [TEST-EVIDENCE.md](TEST-EVIDENCE.md).
>
> **Test Status (2026-04-16)**: FEAT-08 confirmed via Docker cold-start E2E test. See [Docker E2E Validation](../verification/DOCKER-E2E-VALIDATION.md).

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

- A state machine defining which transitions are valid (e.g., `Pending -> Approved`, not `Pending -> Billed`)
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
**Status:** **Fixed** -- verified 2026-04-17. 17 GitHub Actions workflows now present under `.github/workflows/`:

- `ci.yml` -- full backend + frontend build, test, lint, format pipeline
- `release.yml` -- semantic versioning + GitHub releases
- `deploy-dev.yml`, `promote-staging.yml` -- deployment automation
- `codeql-pr.yml`, `security.yml`, `sonarcloud.yml` -- static analysis
- `doc-check.yml`, `commitlint.yml`, `lint-meta.json` -- QA gates
- `labeler.yml`, `pr-title.yml`, `pr-size.yml`, `auto-pr-dev.yml`, `trufflehog-pr.yml`, `dependency-review.yml`, `scorecard.yml` -- PR automation

### Historical Description

The repository contained Docker Compose files, Dockerfiles, and a Helm chart directory (`etc/helm/`), but no automated build or deployment pipeline existed.

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

1. On every pull request: restore, build, and run the existing backend tests (115 methods as of 2026-04-24; see [docs/testing/coverage-status.md](../testing/coverage-status.md)).
2. On merge to `main`: build Docker images, push to a container registry, and deploy to a target environment.
3. Manage secrets via the chosen platform's secret store (not committed files -- see [SEC-01](SECURITY.md#sec-01-secrets-committed-to-source-control)).

Open question: What is the target deployment platform (Azure, AWS, on-prem)? See [OVERVIEW.md Open Questions #3](OVERVIEW.md#open-questions).

---

## FEAT-07: Test Coverage Gaps (Mostly Obsolete)

**Severity:** Medium (downgraded from "near-zero" -- see status below)
**Status:** Mostly Obsolete -- substantial backend coverage shipped via Tier-1 work. Remaining gap: Angular component tests and a handful of backend entities.

### Description

The original "13 unique test methods" claim is stale by +102. As of 2026-04-24 the backend test suite contains **115 test methods across 17 files** (113 `[Fact]` + 2 `[Theory]`), covering 8 entities: `Appointments`, `DoctorAvailabilities`, `Doctors`, `Patients`, `Books`, `AppointmentAccessors`, `ApplicantAttorneys`, `Locations`.

The canonical live coverage numbers live in [docs/testing/coverage-status.md](../testing/coverage-status.md) (forthcoming, PR-5). This issue file reflects a historical snapshot only.

### Current Coverage (2026-04-24)

| Entity / Feature | Backend Tests | Angular Tests |
|---|---|---|
| Appointments | Covered | None |
| DoctorAvailabilities | Covered | None |
| Doctors | Covered | None |
| Patients | Covered | None |
| Books (scaffold, not used) | Covered (legacy) | None |
| AppointmentAccessors | Covered | None |
| ApplicantAttorneys | Covered | None |
| Locations | Covered | None |
| External Signup | 0 | None |
| Tenant / Doctor Creation | 0 | None |
| Other entities (AppointmentEmployerDetail, AppointmentApplicantAttorney, reference-data lookups) | 0 | None |

See [docs/testing/coverage-status.md](../testing/coverage-status.md) for canonical per-method counts.

### What Still Needs to Be Built

**Priority 1 -- Remaining backend application service tests:**
- `ExternalSignupAppServiceTests` -- registration flow, role assignment, email uniqueness
- `DoctorTenantAppServiceTests` -- tenant + doctor co-creation, transactional rollback
- `AppointmentEmployerDetailsAppServiceTests`, `AppointmentApplicantAttorneysAppServiceTests` -- child-entity CRUD

**Priority 2 -- Domain service tests:**
- `AppointmentManagerTests` -- status transition validation (once [FEAT-01](#feat-01-appointment-status-workflow-has-no-implementation) is implemented)

**Priority 3 -- Angular component tests** (still entirely absent):
- `AppointmentAddComponent` -- multi-step form validation, slot selection, patient lookup
- `HomeComponent` -- role-based routing logic
- Doctor-availability generate/edit components

The existing `TestBase` project provides the necessary backend infrastructure. Refer to [Testing Strategy](../devops/TESTING-STRATEGY.md) for patterns.

---

## FEAT-08: Swagger OAuth Does Not Work From Browser in Docker

**Severity:** Medium
**Status:** Open -- **Confirmed via Docker E2E testing (2026-04-16, ISSUE-005)**

### Description

When the stack runs in Docker, the Swagger UI's built-in "Authorize" button cannot complete the OAuth flow because the OpenID Connect metadata URL it retrieves points at a Docker-internal hostname that the browser cannot resolve.

### Test Evidence

```
Docker E2E test (2026-04-16, Playwright MCP):
  Navigate to http://localhost:44327/swagger
  Click "Authorize" -> select CaseEvaluation_Swagger client
  Browser console:
    net::ERR_NAME_NOT_RESOLVED @ http://authserver:8080/.well-known/openid-configuration
  Result: OAuth authorize flow cannot complete. Swagger UI stays unauthenticated.
```

### Root Cause

In `docker-compose.yml`, the `api` service is configured with a split-horizon OIDC setup:

- `AuthServer__Authority: http://localhost:44368` -- public authority URL used to validate JWT issuer claims (what tokens carry).
- `AuthServer__MetaAddress: http://authserver:8080` -- internal Docker DNS hostname used for backend-to-backend OIDC metadata fetch during ASP.NET JwtBearer handler startup.

`CaseEvaluationHttpApiHostModule.ConfigureSwagger` (lines 181-194) passes `AuthServer:MetaAddress` to `AddAbpSwaggerGenWithOidc`, which writes the Docker-internal URL into the generated OpenAPI security scheme. Swagger UI runs in the user's browser, not inside the Docker network, so `authserver` is not resolvable.

Angular is unaffected because `docker/dynamic-env.json` hard-codes the browser-reachable URLs (`http://localhost:44368` for `oAuthConfig.issuer`).

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` -- `ConfigureSwagger` method (around lines 181-194) reads the MetaAddress directly
- `docker-compose.yml` -- `api` service env section (around lines 85-103) sets both Authority and MetaAddress

### Impact

- Manual API exploration via Swagger UI requires a manually obtained bearer token (via curl + the OIDC token endpoint, or copied from a browser devtools Network tab after an Angular login)
- New developers cannot use Swagger's convenient authorize flow when running the stack in Docker
- Blocks the documented "use Swagger to test API endpoints" path in [DOCKER-DEV.md](../runbooks/DOCKER-DEV.md)
- Local (non-Docker) dev is unaffected -- Authority and MetaAddress both resolve to `localhost:44368`

### What Needs to Be Built

Two viable approaches:

1. **Add a separate Swagger-only authority URL.** Introduce `AuthServer__SwaggerMetaAddress` (new env var) defaulting to the Authority URL when unset. Update `ConfigureSwagger` to prefer `SwaggerMetaAddress` over `MetaAddress`. In docker-compose.yml, set it to `http://localhost:44368` so the browser can resolve it while the backend continues to use the internal hostname.
2. **Proxy the OIDC metadata through the API.** Expose a small pass-through endpoint on the API that fetches and returns the AuthServer's metadata document. Point Swagger at this proxied URL. More code, but preserves a single Authority env var.

Option 1 is simpler and follows the same split-horizon pattern already in use between Authority and MetaAddress. Recommended.

### Open Question

Is the Swagger UI expected to be exercised against Docker at all, or is it intended only for local-dev use? If Docker is not a first-class Swagger target, the lighter-weight fix is to document the curl + bearer token workflow explicitly in DOCKER-DEV.md instead of shipping a code fix.

---

---

## FEAT-09: Patient cross-tenant visibility leak -- AppService has no tenant filter (patient-imultitenant) {#patient-imultitenant}

**Severity:** High (HIPAA / cross-tenant data leakage)
**Status:** Open

### Description

Today, any authenticated user with the `Patients.Default` permission can call `GET /api/app/patients` and see patients from every tenant. The root cause has two layers:

1. The `Patient` entity carries a nullable `TenantId` column but does NOT implement `IMultiTenant`. ABP's automatic multi-tenant data filter does not apply to Patient queries.
2. `PatientsAppService.GetListAsync` does not add a manual `WHERE TenantId = CurrentTenant.Id` clause to compensate.

Result: tenant-scoped business users (TenantAdmin, Doctor, attorney, patient) see every tenant's patients instead of only their own. This is the HIPAA leak.

The intent is that **only the host admin** (dev/debug role, outside business usage) sees cross-tenant results. Every other caller should be tenant-scoped.

### Test encoding

`PatientsAppServiceTests.GetListAsync_FromHostContext_ReturnsPatientsFromBothTenants` passes green -- this is correct intended behaviour for host admin and stays green forever.

`PatientsAppServiceTests.GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients` ships as `[Fact(Skip=...)]` pointing at this anchor. When the fix lands, it flips live.

### What Needs to Be Built

1. `Patient` implements `IMultiTenant` (the "framework way") -- the Patient DbContext config moves out of the `IsHostDatabase()` guard and ABP's automatic filter engages. Host-admin paths that need cross-tenant visibility wrap in `using (IDataFilter.Disable<IMultiTenant>())`, the same pattern `DoctorsAppService.GetListAsync` already uses.
2. Optional but recommended: host-admin role check is added to `PatientsAppService.GetListAsync` (and similar) so the `IDataFilter.Disable` branch only fires for the host-admin role, not just any host-context caller.
3. The skipped target test `GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients` flips green.

### Why Not Yet Fixed

Pre-deployment: no production Patient data exists, so there is no data-migration cost. The fix itself is a small, targeted schema + entity change. Scheduled after Tier-1 test coverage completes so the Patient test suite gives the fix a safety net.

Prior ratio\-nale in this doc -- "migration heavy due to existing data" -- is obsolete given the pre-deployment state.

---

## FEAT-10: Test base lacks a WithCurrentUser helper (test-current-user-faking) {#test-current-user-faking}

**Severity:** Low (blocks specific test coverage, not production behaviour)
**Status:** Open

### Description

`GetMyProfileAsync` / `UpdateMyProfileAsync` on `PatientsAppService` resolve the logged-in patient via `CurrentUser.Id`. The integration-test base (`CaseEvaluationApplicationTestBase<TStartupModule>`) does not yet expose a helper to fake `ICurrentUser.Id` inside a scope, which means these endpoints cannot be exercised by tests directly. PR-1C ships the intended test shells as `[Fact(Skip=...)]` so they're visible as known gaps.

### What Needs to Be Built

1. A `WithCurrentUser(Guid userId, string[] roles)` helper on the test base (likely using ABP's `ICurrentUser` substitutable pattern or a scoped fake registered in Autofac).
2. The two skipped profile tests in `PatientsAppServiceTests` flip live.
3. Future PRs for role-aware tests (Accessor scoping, Attorney-only endpoints) reuse the helper.

### Why Not Yet Built

The profile endpoints are a small part of Patient surface; adding the faking infrastructure in PR-1C would have roughly doubled its scope. Deferred to a follow-up.

---

## FEAT-11: HostAdmin role has no formal definition (host-admin-role-formal-definition) {#host-admin-role-formal-definition}

**Severity:** Medium (architectural debt; obstructs role-based gating)
**Status:** Open

### Description

The "host admin" is conceptually the dev/debug superuser who sees every tenant's data for maintenance and debugging purposes -- not a business user. In the current codebase, this role has no formal definition. The only thing playing the part is ABP's built-in `admin` user (seeded by the framework's default `IdentityDataSeedContributor` on the host database). There is no explicit `HostAdmin` role constant, no host-level permission group that gates it, and no policy binding.

Observed symptoms:
- Host-level capabilities (cross-tenant `GetListAsync` calls that bypass the multi-tenant filter) are effectively gated only by "is the caller's `CurrentTenant.Id == null`". Any authenticated user who ends up in host context gets the capability.
- There is no `[Authorize(Policy = "HostAdmin")]` or equivalent to explicitly restrict the superuser surface.

### Impact

Without an explicit HostAdmin role, the difference between "a business user who happens to be operating in host context" and "the dev/maintainer superuser" cannot be enforced. The FEAT-09 fix (adding tenant filtering to Patients) should target only non-HostAdmin callers, which requires the role to exist.

### What Needs to Be Built

1. Introduce a `HostAdmin` role, seeded in the host database by a new per-project seed contributor.
2. Add a host-level permission group in `CaseEvaluationPermissions` (e.g., `CaseEvaluation.Host.*`) registered with `MultiTenancySides.Host`.
3. Bind the HostAdmin role to this permission group.
4. Rename the `admin` role assignment to `HostAdmin` for the dev/maintenance user, or give that user both.

Test infra already pre-models this: `IdentityUsersTestData.HostAdminId` with role constant `"admin"` today, updatable to `"HostAdmin"` once the role ships.

---

## FEAT-12: TenantAdmin role is conflated with Doctor role (tenant-admin-role-separation) {#tenant-admin-role-separation}

**Severity:** Medium (architectural debt; blocks per-tenant admin distinct from practitioner)
**Status:** Open

### Description

When a new SaaS tenant is provisioned, `DoctorTenantAppService.CreateAsync` creates the first user and assigns them the `Doctor` role. This conflates two distinct responsibilities:

1. **TenantAdmin** -- the business role responsible for administering the practice (managing staff, billing, scheduling settings). Conceptually closer to "office manager."
2. **Doctor** -- a practicing physician whose workflows are clinical (appointments, availability, evaluations). Conceptually "the practitioner."

A single role cannot correctly permission-gate both without over-granting.

### Impact

- The first user of a tenant has full privileges across both admin and clinical surfaces, with no principle-of-least-privilege option for practices that want to separate admin from practitioner.
- Future test scenarios that need "tenant admin but not a doctor" (e.g., a staff scheduler) cannot be modeled faithfully.

### What Needs to Be Built

1. Introduce a `TenantAdmin` role via `EnsureRoleAsync("TenantAdmin")` inside the tenant-provisioning flow.
2. Update `DoctorTenantAppService.CreateAsync` so the tenant-creating user gets `TenantAdmin`. Adding Doctor becomes a separate explicit action (either in the same flow for practices that are solo-practitioner, or deferred to a subsequent onboarding step).
3. Bind appropriate permissions to each role in `CaseEvaluationPermissionDefinitionProvider`.

Test infra pre-models this: `IdentityUsersTestData.TenantAdmin1UserId` with role `"TenantAdmin"` already ships in the tenant-semantics cleanup, so tests are ready when the role lands in production code.

---

## FEAT-13: Dashboard.Host permission is registered as tenant-scoped (dashboard-host-permission-scope-typo) {#dashboard-host-permission-scope-typo}

**Severity:** Low (cosmetic / latent bug; doesn't affect current behaviour)
**Status:** Open

### Description

In `CaseEvaluationPermissionDefinitionProvider.cs`, the `Dashboard.Host` permission is registered with `MultiTenancySides.Tenant` despite the `.Host` naming. This is either a typo or a stale change; the intent was surely `MultiTenancySides.Host` since the sibling `.Tenant` permission already covers tenant-side dashboard access.

Harmless today because no code actually checks `Dashboard.Host`, but it will cause confusion the moment someone tries to gate a host-side dashboard endpoint with it -- the permission will be scoped to the wrong side.

### What Needs to Be Built

Change the registration of `Dashboard.Host` from `MultiTenancySides.Tenant` to `MultiTenancySides.Host` in the provider. One-line fix, zero behaviour change today.

---

## FEAT-14: SQLite test DB does not enforce foreign-key constraints (test-fk-enforcement) {#test-fk-enforcement}

**Severity:** Medium (blocks specific delete-constraint tests; does not affect production)
**Status:** Open

### Description

`CaseEvaluationEntityFrameworkCoreTestModule.CreateDatabaseAndGetConnection()` opens a single persistent `SqliteConnection` with `"Data Source=:memory:;Foreign Keys=True"` and then explicitly runs `PRAGMA foreign_keys = ON;` after `Open()`. Despite both opt-ins, child-row FK violations (e.g., deleting a `Location` that is referenced by a `DoctorAvailability` or `Appointment`) do NOT raise inside tests. The delete succeeds, leaving orphan rows that would be impossible against production SQL Server with the `NoAction` FKs configured in the DbContext.

### Symptoms Encoded in Phase B-6 Tier-1 PR-1D

- `LocationsAppServiceTests.DeleteAsync_WhenLocationReferencedByDoctorAvailability_Throws` -- skipped.
- `LocationsAppServiceTests.DeleteAsync_WhenLocationReferencedByAppointment_Throws` -- skipped.

Both bodies are complete, including `autoSave: true` on the child `InsertAsync` calls so the rows persist to the DB before the delete commit; each test flips live the instant test-infra FK enforcement starts working.

### Suspected Cause

ABP / EF Core appear to wrap or pool the shared connection such that the per-connection FK opt-in is bypassed -- either by opening a separate pooled instance for each DbContext or by resetting PRAGMA state on transitions. The manual PRAGMA runs once on the test-startup connection; if subsequent DbContexts see a logically separate wrapper, the FK-on flag is lost.

### What Needs to Be Built

One of the following:

1. Hook `connection.StateChange` in the test module and re-issue `PRAGMA foreign_keys = ON;` on every transition to `Open`. Lightweight; guard against recursion.
2. Customize `AbpDbContextOptions.Configure` to invoke the PRAGMA inside the DbContext `OnConfiguring` override or via an EF Core interceptor so every context that touches the connection enforces FK.
3. Let EF Core open/close the connection lifecycle itself (surrender manual `Open()` in the module) so its built-in per-connection PRAGMA hook fires -- requires reworking the "keep the in-memory DB alive" pattern.

### Why Not Yet Fixed

Scope: the shared test module affects every EF Core test in the solution. A partial fix that silently breaks existing tests would be worse than the current documented gap. Deferred to a dedicated test-infra PR after Tier-1 completes; Tier-2 plans depend on this being in place so delete-constraint and cascade tests can ship live rather than skipped.

---

## Related Documentation

- [Issues Overview](OVERVIEW.md) -- All issues by category and severity
- [Appointment Lifecycle](../business-domain/APPOINTMENT-LIFECYCLE.md) -- Intended status model
- [User Roles & Actors](../business-domain/USER-ROLES-AND-ACTORS.md) -- Role definitions
- [Component Patterns](../frontend/COMPONENT-PATTERNS.md) -- How to build new Angular feature modules
- [Testing Strategy](../devops/TESTING-STRATEGY.md) -- Test infrastructure and patterns
- [Docker & Deployment](../runbooks/DOCKER-DEV.md) -- Docker Compose development setup
