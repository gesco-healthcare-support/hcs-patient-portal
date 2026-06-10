# AppointmentAccessors -- per-appointment access-grant entity

Records which `IdentityUser` holds `View` or `Edit` rights on a single `Appointment`.
Group J (2026-06-05) made `AppointmentAccessorManager.CreateOrLinkAsync` the live create
path: `AppointmentAccessorsAppService.CreateAsync` resolves a free-typed email to a user
(or auto-provisions + invites one) and links it, restoring OLD's email-based create-or-link
flow. The booking form and appointment-view both add accessors by typing name + email +
role + rights. (Earlier Phase 11i wiring still used the slim id-based create; Group J
replaced it.)

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
`Claim Examiner`. Internal roles (Intake Staff, Doctor, IT Admin, etc.) are NOT in this
list -- holding only internal roles does NOT trigger a mismatch.

`CreateOrLinkAsync` requires the full DI constructor (4 params). Resolving via DI always
provides it. If only the slim ctor was used, the method throws `InvalidOperationException`
before touching the DB -- fail fast is intentional (see dual-ctor pattern in Domain CLAUDE.md).

## Conventions

- `EfCoreAppointmentRepository` may join against this table for attorney-scoped filtering.
  Confirm the join is still present before removing or repurposing the entity.
- Both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` configure this entity
  OUTSIDE the `IsHostDatabase()` guard -- tenant rows are the common case.
- Angular UI (Group J): the booking form's "Additional Authorized User" section
  (`sections/appointment-add-authorized-users.component`) and the appointment-view accessor
  table both POST email-based creates to `api/app/appointment-accessors`. The proxy carries
  the email-based `AppointmentAccessorCreateDto` (`email`/`firstName`/`lastName`/`role`).

## Gotchas

- **Mutations are gated by a dedicated accessor-management rule, not by feature permissions.**
  Workstream B (2026-06-10) replaced the Group J `EnsureCanEditAsync` gate on
  `Create`/`Update`/`Delete` with `AppointmentReadAccessGuard.EnsureCanManageAccessorsAsync`,
  which composes the pure `AppointmentAccessRules.CanManageAccessors` rule: deny-by-default --
  only an internal user OR the appointment creator who ALSO holds an authorized
  accessor-managing external role (Applicant / Defense Attorney today; the paralegal feature
  appends Paralegal via `BookingFlowRoles.ExternalAccessorManagerRoles`) may add or change
  accessors. This is STRICTER than appointment edit-access: the Edit-accessor pathway is
  intentionally dropped (an Edit-accessor may still complete/edit the form and submit
  change-requests via the UNTOUCHED `CanEditAsync`, but may no longer self-propagate
  accessors), and a Patient / Claim-Examiner creator is denied. The
  `CaseEvaluation.AppointmentAccessors.{Create,Edit,Delete}` permission constants are still NOT
  enforced (the access-guard model is used instead); the skipped test
  `CreateAsync_WhenCallerLacksCreatePermission_ShouldThrow` tracks that unused permission path.
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
