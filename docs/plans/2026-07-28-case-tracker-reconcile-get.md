---
feature: Case Tracker reconcile GET (integration Part 4)
date: 2026-07-28
status: complete
base-branch: main
related-issues: []
---

## Goal

Expose a token-gated read endpoint that returns one appointment's complete intake payload, so the Case
Tracker can recover a dead-lettered push and refresh a case on open.

## Context & decisions

Parts 1-3 are merged (#393, #395, docs #396 -> main `060c6b6f`). The push is the delivery path; this
endpoint is the BACKSTOP the contract's revised section F describes. It is the last remaining protocol
surface between the two systems.

Resolved decisions (no open questions remain):

- Decision: the route carries the office -- `api/integration/offices/{tenantId}/appointments/{appointmentId}`
  -- and the handler calls `ICurrentTenant.Change(tenantId)` explicitly, because the portal is
  database-per-office and this call has NO other way to identify the database. Tenant resolvers are
  `CurrentUser` then subdomain only; QueryString / Cookie / Route / Header resolvers were deliberately
  cleared (ADR-006/007) so `?__tenant=` cannot override a URL. An explicit route parameter consumed in
  code does not re-open that hole, whereas re-registering a resolver would. Case Tracker already holds
  `tenant.tenantId` from every push, so this costs them nothing but a URL change.
- Decision: keep the `api/integration/...` prefix and add NO rate limiter, per the agreed contract,
  even though every other anonymous endpoint in this repo sits under `api/public/...` behind a scoped
  limiter. Adrian's call after the trade-off was put to him. Consequence recorded under Risk.
- Decision: the response is a `ContentResult` serialized by `IntakePayloadSerializer`, not a returned
  object, because the contract promises a byte-identical shape to the push and that is the only way to
  guarantee it by construction. Returning an object would depend on ABP MVC's casing and
  success-wrapping behaviour, which was NOT verified and could change globally later.
- Decision: gated by the existing `CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled`
  setting, returning 404 when off, because this is unauthenticated PHI and must not be reachable
  before an office is deliberately switched on. Note the gate is read AFTER the tenant change, so it
  is that office's value -- an office that is not live cannot be read even if another is.
- Decision: the logic lives in a DOMAIN service, not an application service, because
  `ConventionalControllers.Create(typeof(CaseEvaluationApplicationModule).Assembly)`
  (`CaseEvaluationHttpApiHostModule.cs:549`) auto-exposes every app service. An app service would get
  a SECOND route that bypasses the token check entirely -- a hole. Domain services are not exposed.
- Decision: 404 covers both "no such appointment" and "integration disabled", because distinguishing
  them would tell an unauthenticated caller whether an appointment id exists.

Coordination: the `{tenantId}` segment CHANGES the URL in contract section F. Their developer must be
told before they code against the old one. Task T7 covers the contract edit; sending the note is
Adrian's.

## All needed context

| Piece | Anchor |
|---|---|
| Payload builder (reuse; already designed for this caller) | `Domain/Integration/CaseTracker/Payload/IIntakePayloadBuilder.cs:19` -- `BuildAsync(appointmentId, ct)` -> `IntakeEnvelope` |
| Response envelope, already the Gesco `{data,meta,errors}` shape | `Domain/Integration/CaseTracker/Payload/IntakeEnvelope.cs` |
| Serializer that produces the push bytes | `Domain/Integration/CaseTracker/IntakePayloadSerializer.cs` -- `Serialize(IntakeEnvelope)` |
| Token-const + config-key pattern to mirror | `Domain/Integration/CaseTracker/CaseTrackerClient.cs:27,29` -- `IntakeTokenHeaderName`, `TokenConfigurationKey = "CaseTracker:IntakeToken"` |
| Setting read pattern | `Domain/Integration/CaseTracker/IntegrationOutboxDrainService.cs:52` -- `_settingProvider.IsTrueAsync(...)` |
| Setting const | `Domain/Settings/CaseEvaluationSettings.cs:37` |
| Explicit-route controller pattern | `HttpApi/Controllers/Integration/CaseTrackerPushController.cs:16-19` |
| Tenant-change pattern (proven in Hangfire jobs) | `_currentTenant.Change(tenantId)` as used by the Part 1 drain job |
| Contract spec for this endpoint | `docs/integration/case-tracker-api-contract.md` section F |

Gotchas:

- `ControllerNameAttribute` is in **`Asp.Versioning`**, NOT an ABP namespace.
- Do NOT add `[IgnoreAntiforgeryToken]`; Sonar flags it CRITICAL (S4502) and 43 of 50 controllers omit
  it. A GET does not need it regardless.
- `ICurrentTenant.Change` returns an `IDisposable` scope -- it MUST be disposed (`using`), or the
  ambient tenant leaks into the rest of the request.
- An unknown `tenantId` must not surface as a 500. Verify behaviour and map it to 404.
- The PHI scanner hook rejects 8+ consecutive digits; use hex-with-letters GUIDs in fixtures.
- Behaviours already fixed by the contract, not to be re-decided: 200 with data for a CANCELLED
  appointment; 404 only for unknown (terminal for them); the `documents` array is the complete,
  never-paginated set; `objectKey` is mutable for a stable `id`.

## Tasks (implementation blueprint)

### T1 - Integration token constants

- what: CREATE `Domain/Integration/CaseTracker/CaseTrackerIntegrationConsts.cs` with
  `IntegrationTokenHeaderName = "X-Integration-Token"` and
  `TokenConfigurationKey = "CaseTracker:IntegrationToken"`.
- pattern: `CaseTrackerClient.cs:27,29`
- approach: code
- acceptance: The system shall expose the inbound header name and config key as constants, so the
  controller and its tests cannot disagree about either.

### T2 - Constant-time token validator

- what: CREATE `Domain/Integration/CaseTracker/IntegrationTokenValidator.cs` (`ITransientDependency`)
  with `bool IsValid(string? presented)`: reads the configured token via `IConfiguration`, returns
  FALSE when the configured token is null/empty (fail closed), and compares with
  `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes, length-checking first.
- pattern: the config read in `CaseTrackerClient`; `ISettingProvider` usage in
  `IntegrationOutboxDrainService.cs:52`
- approach: tdd
- acceptance: WHEN the presented token equals the configured token, THE SYSTEM SHALL return true.
  WHEN it differs in any byte or in length, THE SYSTEM SHALL return false. IF no token is configured,
  THEN THE SYSTEM SHALL return false for every input, including null and empty. THE SYSTEM SHALL NOT
  write the configured or presented token to any log.

### T3 - Reconcile domain service

- what: CREATE `Domain/Integration/CaseTracker/CaseTrackerReconcileService.cs` (`ITransientDependency`)
  with `Task<IntakeEnvelope?> GetAsync(Guid tenantId, Guid appointmentId, CancellationToken)`. Order:
  open `using _currentTenant.Change(tenantId)`; return null if
  `!await _settingProvider.IsTrueAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)`;
  else `await _payloadBuilder.BuildAsync(appointmentId, ct)`, returning null when the appointment does
  not exist. Deliberately a DOMAIN service -- see the decision above.
- pattern: `IntegrationOutboxDrainService` for the setting gate; the Part 1 drain job for the tenant scope
- approach: tdd
- acceptance: WHEN the appointment exists in that office and the setting is on, THE SYSTEM SHALL return
  its envelope. WHILE the setting is off for that office, THE SYSTEM SHALL return null regardless of
  whether the appointment exists. IF the appointment id is unknown, THEN THE SYSTEM SHALL return null.
  THE SYSTEM SHALL dispose the tenant scope before returning, leaving the ambient tenant unchanged.

### T4 - Reconcile controller

- what: CREATE `HttpApi/Controllers/Integration/CaseTrackerReconcileController.cs`, `[AllowAnonymous]`,
  `[Route("api/integration")]`, `[ControllerName("CaseTrackerReconcile")]`, with
  `[HttpGet("offices/{tenantId}/appointments/{appointmentId}")]`. Body: reject with 401 when
  `IntegrationTokenValidator.IsValid(Request.Headers[...])` is false; else call the domain service;
  404 when it returns null; otherwise 200 with
  `Content(IntakePayloadSerializer.Serialize(envelope), "application/json")`. No
  `[IgnoreAntiforgeryToken]`.
- pattern: `CaseTrackerPushController.cs:16-19` (and its `using Asp.Versioning;`)
- approach: tdd
- acceptance: WHEN the header is missing, empty or wrong, THE SYSTEM SHALL respond 401 and SHALL NOT
  query any office database. WHEN the token is valid and the appointment exists, THE SYSTEM SHALL
  respond 200 with a body byte-identical to what the push would send for that appointment. WHEN the
  appointment is unknown OR the office has the integration disabled, THE SYSTEM SHALL respond 404 with
  no body distinguishing the two cases. WHERE the appointment is cancelled, THE SYSTEM SHALL still
  respond 200 carrying the cancelled status.

### T5 - Unknown-office handling

- what: MODIFY `CaseTrackerReconcileService.GetAsync` to handle an unrecognised `tenantId` without a
  500 -- determine empirically whether `ICurrentTenant.Change` plus a repository read throws or yields
  empty, and map the failure to null with a warning that logs the tenant id only.
- pattern: the per-office try/catch in `Jobs/CaseTrackerReconciliationJob.cs`
- approach: test-after
- acceptance: WHEN `tenantId` matches no office, THE SYSTEM SHALL respond 404, SHALL NOT return a 500,
  and SHALL log the rejected tenant id without any patient field.

### T6 - Tests

- what: CREATE `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Integration/CaseTracker/IntegrationTokenValidatorTests.cs`
  and `CaseTrackerReconcileServiceTests.cs`. Cover: valid/invalid/missing/empty token, unconfigured
  token, the setting-off path, unknown appointment, unknown office, tenant-scope disposal, and that
  the serialized body equals `IntakePayloadSerializer.Serialize` of the same envelope. Synthetic data
  only.
- pattern: `IntegrationOutboxDrainServiceTests.cs` substitute harness; `DocumentAcceptedHandlerTests.cs`
  for the `IRepository` + `ISettingProvider` fakes
- approach: tdd
- acceptance: The system shall cover token validation including the fail-closed path, the setting gate,
  both 404 causes, and byte-identity with the push serializer.

### T7 - Contract + config documentation

- what: MODIFY `docs/integration/case-tracker-api-contract.md` section F: change the URL to
  `GET {portalBase}/api/integration/offices/{tenantId}/appointments/{appointmentId}`, state that
  `tenantId` is the `tenant.tenantId` they already receive in every push, flip the section F STATUS to
  BUILT, and note the 401-on-bad-token and 404-when-disabled behaviours. MODIFY the appsettings
  template to document the `CaseTracker:IntegrationToken` key WITHOUT a value.
- pattern: the section J built/not-built split added in #396
- approach: code
- acceptance: THE SYSTEM SHALL document the final URL including the office segment, and SHALL NOT
  contain the token value in any committed file.

## Deviations recorded during build (2026-07-28)

1. **The controller lives in HttpApi.Host, not HttpApi.** T4 specified
   `HttpApi/Controllers/Integration/`, but that project references ONLY Application.Contracts, while
   `CaseTrackerReconcileService`, `IntakeEnvelope` and `IntakePayloadSerializer` all live in Domain --
   so it does not compile there. Host can see Domain through Application, and `HomeController` sets the
   precedent for a controller in Host. The alternatives were both worse: an application service would
   be auto-exposed by `ConventionalControllers.Create` at a second route with NO token check (the exact
   hole the plan's domain-service decision exists to avoid), and hoisting the envelope plus serializer
   into Domain.Shared would refactor already-merged Part 1 code across three layers to satisfy a
   layering technicality. The plan's core decision -- domain service, not app service -- is unchanged.
2. **The config key is documented in code, not appsettings.** T7 said to add the key to the appsettings
   template. Part 1 documented `CaseTracker:BaseUrl` and `CaseTracker:IntakeToken` only in code
   docstrings and never added a `CaseTracker` section to any committed appsettings file, so this
   follows suit (`CaseTrackerIntegrationConsts` plus contract §F). Committing a secret's key name with
   an empty value also invites someone to fill it in there rather than in a secret store.
3. **T4 gained a test file the plan did not list.** T6 named only two test files. The controller's
   401-before-any-database-work behaviour is a security property in T4's acceptance, so it needed
   direct coverage:
   `test/HealthcareSupport.CaseEvaluation.Application.Tests/Controllers/Integration/CaseTrackerReconcileControllerTests.cs`
   drives the controller with a fake `HttpContext` over the REAL validator and service -- no web host
   needed -- and asserts byte-identity against `IntakePayloadSerializer`.

## Validation loop

From the repo root, in order:

```bash
dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
```
```bash
dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```
```bash
dotnet test HealthcareSupport.CaseEvaluation.slnx
```
```bash
python .claude/scripts/verify_structure.py
```

Done-bar: all four green (structure check 0 FAIL), and no fixture contains real-looking patient data.
Note the EF test project takes 8-10 minutes; that is normal.

## Risk / rollback

Blast radius: additive. One new anonymous GET, one new domain service, one validator, no migration and
no change to any existing path. Nothing existing behaves differently.

The real risk is the endpoint itself: it returns a patient's full name, date of birth, email and phone
to any caller holding a static shared token, over a route with NO rate limiter (Adrian's decision after
the trade-off was raised). Mitigations in the plan: fail closed when the token is unconfigured, the
per-office setting gate so nothing is readable before go-live, 404 that does not reveal whether an
appointment exists, and the token never logged. Residual: a leaked or brute-forced token permits
unthrottled enumeration of appointment payloads. Revisit if the token is ever shared beyond their
service, and require TLS before real data flows.

Rollback: revert the PR. Code-only, no migration. If the endpoint needs disabling without a deploy,
switch `CaseTrackerPushEnabled` off for the office -- which also stops outbound pushes, so it is a
blunt instrument; a dedicated setting would be the follow-up if that turns out to matter.
