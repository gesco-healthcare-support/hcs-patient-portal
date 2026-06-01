# AppointmentAccessors -- per-appointment access-grant entity

Records which `IdentityUser` holds `View` or `Edit` rights on a single `Appointment`.
The entity exists for post-MVP ACL extensibility; Phase 11i (2026-05-04) activated the
booking-time create path via `AppointmentAccessorManager.CreateOrLinkAsync`.

## What lives here

| File | Purpose |
|---|---|
| `AppointmentAccessor.cs` | `FullAuditedEntity<Guid>, IMultiTenant` (not AggregateRoot -- link row, not domain root) |
| `AppointmentAccessorManager.cs` | `CreateAsync` / `UpdateAsync` / `CreateOrLinkAsync` (Phase 11i) |
| `AppointmentAccessorRules.cs` | Pure static logic: `ResolveOutcome` + `AccessorLinkOutcome` enum |
| `AppointmentAccessorWithNavigationProperties.cs` | Read-shape: `AppointmentAccessor + IdentityUser? + Appointment?` |
| `AppointmentAccessorAppointment.cs` | Composite-key join entity -- code-gen artefact, unregistered, inert |
| `IAppointmentAccessorRepository.cs` | Custom repo contract |

Entity shape: `TenantId : Guid?`, `AccessTypeId : AccessType (View=23, Edit=24)`,
`IdentityUserId : Guid`, `AppointmentId : Guid`. All three functional fields plus `Id`
are passed to the constructor; the manager calls the constructor only -- no post-ctor writes.

## CreateOrLinkAsync (Phase 11i booking flow)

Called during appointment booking to grant the invited party access. Steps:

1. Look up `IdentityUser` by email via `IdentityUserManager.FindByEmailAsync`.
2. Call `AppointmentAccessorRules.ResolveOutcome` to get an `AccessorLinkOutcome`:
   - `CreateUserAndLink` -- email unknown: create user with temp password, grant role, link,
     publish `AppointmentAccessorInvitedEto` (invitation email).
   - `LinkExisting` -- user already holds the requested role: link only.
   - `GrantRoleAndLink` -- user exists with no recognised external role (e.g. internal
     staff): grant role idempotently, then link.
   - `RoleMismatch` -- user exists with a DIFFERENT recognised external role: throw
     `BusinessException(AppointmentAccessorRoleMismatch)`. This is OLD-parity; do not soften.
3. Call `CreateAsync` to insert the `AppointmentAccessor` row.

`RecognizedExternalRoles`: `Patient`, `Applicant Attorney`, `Defense Attorney`,
`Claim Examiner`. Internal roles (Clinic Staff, Doctor, IT Admin, etc.) are NOT in this
list -- holding only internal roles does NOT trigger a mismatch.

`CreateOrLinkAsync` requires the full DI constructor (4 params). Resolving via DI always
provides it. If only the slim ctor was used, the method throws `InvalidOperationException`
before touching the DB -- fail fast is intentional (see dual-ctor pattern in Domain CLAUDE.md).

## Conventions

- `EfCoreAppointmentRepository` may join against this table for attorney-scoped filtering.
  Confirm the join is still present before removing or repurposing the entity.
- Both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` configure this entity
  OUTSIDE the `IsHostDatabase()` guard -- tenant rows are the common case.
- No Angular UI exists today; the 8 HTTP endpoints under `api/app/appointment-accessors`
  are server-side only.

## Gotchas

- **Permissions defined but not enforced.** `CaseEvaluation.AppointmentAccessors.{Create,Edit,Delete}`
  are registered but the AppService uses bare `[Authorize]` everywhere. Any authenticated user
  can call any endpoint. Tracked by skipped test `CreateAsync_WhenCallerLacksCreatePermission_ShouldThrow`.
  MUST be fixed before any post-MVP ACL feature wires real grants.
- `AccessType` enum values are non-sequential (`View=23`, `Edit=24`) -- legacy values. Add
  new values with explicit integer codes; do not assume zero-based ordering.
- `Check.NotNull` on `Guid` value-type params in `CreateAsync` / `UpdateAsync` is a no-op
  (value types are never null). The real guard is the `Guid.Empty` check in the AppService.
- `AppointmentAccessorAppointment` has no DbSet, no repository, no AppService consumer.
  Leave in place or remove with a product-approved cleanup; do not refactor in isolation.
- `UpdateAsync` replaces all three fields including `IdentityUserId` and `AppointmentId` --
  not just `AccessTypeId`. If a future ACL feature treats rows as immutable grants, tighten
  this before activating it.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- docs/security/AUTHORIZATION.md
- docs/decisions/002-manual-controllers-not-auto.md
