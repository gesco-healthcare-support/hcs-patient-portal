# ADR-002: Manual Controllers Instead of ABP Auto-Controllers

**Status:** Accepted
**Date:** 2026-04-10
**Verified by:** code-inspect

## Context

ABP Framework can automatically generate REST API controllers from `IApplicationService`
interfaces via its `AbpAutoApiController` convention. This eliminates boilerplate but
gives the developer limited control over route structure, HTTP method mapping, and
per-endpoint customization.

This project has 16+ feature controllers (Doctors, Appointments, Patients, Locations,
etc.) that follow a consistent manual delegation pattern. Each AppService is decorated
with `[RemoteService(IsEnabled = false)]` to prevent ABP from auto-generating a
duplicate controller alongside the manual one.

## Decision

Every `IAppService` interface has a manually written controller in
`src/.../HttpApi/Controllers/{Feature}/{Entity}Controller.cs` that:

1. Extends `AbpController` and implements the `I{Entity}AppService` interface
2. Injects the `I{Entity}AppService` via constructor
3. Delegates each public method directly to the injected service
4. Declares explicit `[Route("api/app/{entity-plural}")]` and HTTP method attributes

Example from `DoctorController.cs` (10 endpoints):
- Class: `DoctorController : AbpController, IDoctorsAppService`
- Route: `[Route("api/app/doctors")]`
- Each method is a one-line delegation: `return _doctorsAppService.MethodAsync(input);`

All AppService implementations use `[RemoteService(IsEnabled = false)]` to suppress
ABP's auto-controller generation, avoiding duplicate route registration.

## Consequences

**Easier:**
- Full control over route structure, HTTP verbs, and URL segments per endpoint
- Ability to add custom attributes (authorization, caching, rate limiting) per action
- Explicit routes make the API surface auditable via code review
- Swagger/OpenAPI output matches the exact routes declared in code

**Harder:**
- Every new AppService method requires a corresponding controller method (boilerplate)
- Risk of the controller falling out of sync with the service interface -- mitigated by
  implementing the interface directly, so the compiler enforces method parity
- New developers must remember the `[RemoteService(IsEnabled = false)]` attribute on
  every AppService or ABP will register duplicate routes

## Alternatives Considered

1. **ABP Auto-Controllers** (`ConventionalControllerSetting`) -- Rejected because ABP's
   opinionated URL generation (`/api/app/doctor/get-list`) differs from our preferred
   RESTful patterns (`GET /api/app/doctors`). Customizing the auto-generated routes
   requires as much configuration as writing the controller manually.

2. **Minimal APIs (.NET)** -- Rejected because ABP's middleware pipeline (authorization,
   audit logging, unit of work, validation) is wired through `AbpController`. Minimal
   APIs would bypass those cross-cutting concerns unless manually reimplemented.

3. **Hybrid approach** (auto for simple CRUD, manual for complex) -- Rejected for
   consistency. Having two patterns in the same codebase increases cognitive load and
   makes it unclear which pattern to follow for new features.
