# AppointmentAccessors

Per-appointment access-grant entity that records which `IdentityUser` has `View` or `Edit` rights on an individual `Appointment`. Maintained for schema-forward-compatibility only: at MVP, portal visibility for an appointment is driven exclusively by the legal-party join (`AppointmentApplicantAttorney` and equivalents), so this entity is intentionally redundant and reserved for a post-MVP ACL feature. See `docs/product/appointment-accessors.md`.

## MVP Intent (load-bearing)

- **MVP behaviour:** no ad-hoc access grants. A user sees or edits an appointment only if they are a formal legal party on the case (attorney, patient, insurance, claim adjustor, doctor's office). Paralegals and assistants log in using the party's registered credentials.
- **Why this entity still exists:** the table, the `AccessType` enum, the AppService, the controller, and the DbSet are all preserved so a post-MVP ACL feature can be added without a schema migration. Do NOT drop or refactor away.
- **Practical consequence for code reviewers:** new code SHOULD NOT take a runtime dependency on `AppointmentAccessor` rows for access decisions. The `EfCoreAppointmentRepository` may join against this table for legacy filtering paths, but the authoritative source for "can this user see this appointment?" at MVP is the legal-party join, not this entity.
- **Source of intent:** `docs/product/appointment-accessors.md` (Adrian-confirmed 2026-04-24). Several open questions remain in that doc (granting trigger, revocation, overlap with `AppointmentApplicantAttorney`); they are scoped post-MVP.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/AppointmentAccessors/AppointmentAccessorConsts.cs` | Default sorting (`CreationTime desc`) |
| Domain.Shared | `src/.../Domain.Shared/Enums/AccessType.cs` | Enum: `View=23`, `Edit=24` (non-sequential -- legacy values) |
| Domain | `src/.../Domain/AppointmentAccessors/AppointmentAccessor.cs` | Entity: `FullAuditedEntity<Guid>, IMultiTenant` (NOT AggregateRoot) |
| Domain | `src/.../Domain/AppointmentAccessors/AppointmentAccessorAppointment.cs` | Composite-key join entity (`Entity` base, no audit, no IMultiTenant) -- declared but currently unreferenced |
| Domain | `src/.../Domain/AppointmentAccessors/AppointmentAccessorManager.cs` | DomainService -- `CreateAsync` / `UpdateAsync` |
| Domain | `src/.../Domain/AppointmentAccessors/AppointmentAccessorWithNavigationProperties.cs` | Plain DTO bundling `AppointmentAccessor + IdentityUser? + Appointment?` |
| Domain | `src/.../Domain/AppointmentAccessors/IAppointmentAccessorRepository.cs` | Custom repo: `GetWithNavigationPropertiesAsync`, `GetListAsync`, `GetListWithNavigationPropertiesAsync`, `GetCountAsync` |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/AppointmentAccessorDto.cs` | Read DTO (extends `FullAuditedEntityDto<Guid>`) |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/AppointmentAccessorCreateDto.cs` | Create DTO (defaults `AccessTypeId` to `Enum.GetValues<AccessType>()[0]` = `View`) |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/AppointmentAccessorUpdateDto.cs` | Update DTO (no defaulting; all 3 fields required) |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/AppointmentAccessorWithNavigationPropertiesDto.cs` | Bundles `AppointmentAccessorDto + IdentityUserDto? + AppointmentDto?` |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/GetAppointmentAccessorsInput.cs` | Paged-and-sorted input with `FilterText`, `AccessTypeId?`, `IdentityUserId?`, `AppointmentId?` |
| Contracts | `src/.../Application.Contracts/AppointmentAccessors/IAppointmentAccessorsAppService.cs` | Service contract (8 methods) |
| Application | `src/.../Application/AppointmentAccessors/AppointmentAccessorsAppService.cs` | `CaseEvaluationAppService` -- `[RemoteService(IsEnabled = false)]`, class-level `[Authorize]`, mutation methods re-decorated with bare `[Authorize]` |
| EF Core | `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` (lines 236-244) | Entity config OUTSIDE `IsHostDatabase()` guard |
| EF Core | `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` (lines 146-154) | Identical entity config OUTSIDE host guard |
| EF Core | `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationEntityFrameworkCoreModule.cs` | Registers `EfCoreAppointmentAccessorRepository` |
| HttpApi | `src/.../HttpApi/Controllers/AppointmentAccessors/AppointmentAccessorController.cs` | 8 endpoints under `api/app/appointment-accessors` (controller IS exposed despite AppService `IsEnabled = false`) |
| Tests | `test/.../Application.Tests/AppointmentAccessors/AppointmentAccessorsAppServiceTests.cs` | 10 `[Fact]` tests (1 `Skip=` for the permission-gap, see Known Gotchas) |

## Entity Shape

```
AppointmentAccessor : FullAuditedEntity<Guid>, IMultiTenant
  TenantId       : Guid?       (nullable -- created host-side rows have TenantId = null)
  AccessTypeId   : AccessType  (View=23, Edit=24 -- non-sequential legacy values)
  IdentityUserId : Guid        (FK -> IdentityUser; required)
  AppointmentId  : Guid        (FK -> Appointment; required)
```

No status/state machine -- `AccessType` is a flat enum, not a state.

The constructor takes all 3 functional fields plus `Id`. Manager `CreateAsync` calls the constructor only -- no post-construction property writes. Constructor is complete.

```
AppointmentAccessorAppointment : Entity (composite key)
  AppointmentAccessorId : Guid (key part)
  AppointmentId         : Guid (key part)
  GetKeys()             : returns the composite tuple
```

Currently has no DbSet, no DbContext config, no repository, no AppService consumer. It is a code-gen artefact left in the Domain folder; treat as inert until/unless an ACL feature wires it up.

## Relationships

| FK Property | Target Entity | Required | Delete Behaviour | Configured In |
|---|---|---|---|---|
| `IdentityUserId` | `IdentityUser` | Yes | `NoAction` | Both DbContexts (outside `IsHostDatabase` guard) |
| `AppointmentId` | `Appointment` | Yes | `NoAction` | Both DbContexts (outside `IsHostDatabase` guard) |

`AppointmentAccessorWithNavigationProperties` is the read-shape projection used by the AppService; it is a plain class (not an entity), populated by the EF Core repository.

## Multi-tenancy

- **`IMultiTenant`: Yes.** Entity declares `IMultiTenant` and exposes a `Guid? TenantId`.
- **DbContext placement: BOTH contexts, outside the `IsHostDatabase()` guard.** Confirmed at `CaseEvaluationDbContext.cs:236-244` and `CaseEvaluationTenantDbContext.cs:146-154`. The configuration is byte-identical between the two contexts.
- **Tenant filter behaviour (verified by tests):**
  - `GetListAsync_FromTenantAContext_ReturnsOnlyAccessor1` -- TenantA caller sees only TenantA rows.
  - `GetListAsync_FromTenantBContext_ReturnsOnlyAccessor2` -- TenantB caller sees only TenantB rows.
  - `GetListAsync_FromHostContextWithFilterDisabled_ReturnsAccessorsFromBothTenants` -- disabling `IDataFilter<IMultiTenant>` from host context returns rows from both tenants.
  - `CreateAsync_PersistsNewAccessor_AsHostScoped` -- creating from host context produces a row with `TenantId = null`, hidden from tenant callers but visible to host.

## Mapper Configuration

Riok.Mapperly partial classes in `src/.../Application/CaseEvaluationApplicationMappers.cs`:

| Class | Source -> Destination | Strategy | AfterMap? |
|---|---|---|---|
| `AppointmentAccessorToAppointmentAccessorDtoMappers` | `AppointmentAccessor` -> `AppointmentAccessorDto` | default (`Target` required mapping) | None |
| `AppointmentAccessorWithNavigationPropertiesToAppointmentAccessorWithNavigationPropertiesDtoMapper` | `AppointmentAccessorWithNavigationProperties` -> `AppointmentAccessorWithNavigationPropertiesDto` | `RequiredMappingStrategy.None` | None |

No `LookupDto<Guid>` mapper is owned by this feature. The AppService's two lookup endpoints reuse the existing `IdentityUser -> LookupDto<Guid>` and `Appointment -> LookupDto<Guid>` mappers from their respective owning features.

## Permissions

Defined in `CaseEvaluationPermissions.cs` (lines 110-116) and registered in `CaseEvaluationPermissionDefinitionProvider.cs` (lines 63-66):

```
CaseEvaluation.AppointmentAccessors          (Default / parent)
CaseEvaluation.AppointmentAccessors.Create
CaseEvaluation.AppointmentAccessors.Edit
CaseEvaluation.AppointmentAccessors.Delete
```

**Enforcement gap (intentional, tracked as Known Gotcha #2):** the AppService class-level decorator is bare `[Authorize]`, and `Create` / `Update` / `Delete` are each re-decorated with bare `[Authorize]`. The feature-specific permissions above are NOT referenced anywhere in the AppService. Any authenticated user can call any endpoint.

A skipped test (`CreateAsync_WhenCallerLacksCreatePermission_ShouldThrow`) encodes the target behaviour and will flip live once the AppService is updated. The skip reason explicitly references this CLAUDE.md.

## Business Rules

- **Class-level + method-level `[Authorize]` (no permission keys).** Non-mutating methods inherit class-level `[Authorize]`; `Create`, `Update`, `Delete` are explicitly re-decorated `[Authorize]` (without permission keys). Authentication is required, but no specific permission is enforced.
- **Empty-`Guid` rejection on Create and Update.** `IdentityUserId == Guid.Empty` and `AppointmentId == Guid.Empty` each throw `UserFriendlyException` with localised messages. `AccessTypeId` is not validated -- any defined enum value is accepted; out-of-range values are not currently rejected.
- **Update is full-field replacement.** `UpdateAsync(id, dto)` overwrites all three mutable properties (`IdentityUserId`, `AppointmentId`, `AccessTypeId`). No fields are frozen; an existing accessor row may be re-targeted to a different user OR a different appointment OR re-typed View<->Edit in a single call.
- **Create defaults `AccessTypeId` to `View`.** `AppointmentAccessorCreateDto.AccessTypeId` initialises to `Enum.GetValues<AccessType>()[0]`, which resolves to `View` (lowest declared value, 23). Callers who omit the field implicitly request View. UpdateDto has no default -- the caller must supply it.
- **`IdentityUser` lookup filters by email substring.** `GetIdentityUserLookupAsync` searches `Email.Contains(filter)` only -- no name fields, no role filtering. The lookup returns ALL identity users (filtered by tenant scope of the calling context), not only attorneys.
- **`Appointment` lookup filters by request-confirmation-number substring.** `GetAppointmentLookupAsync` searches `RequestConfirmationNumber.Contains(filter)` only. Tenant scoping is enforced (test `GetAppointmentLookupAsync_ReturnsAppointmentsInTenant` confirms TenantA caller does not see TenantB appointments).
- **Domain-service `Check.NotNull` on a `Guid` is always true.** `AppointmentAccessorManager.CreateAsync` calls `Check.NotNull(identityUserId, ...)` etc. on `Guid` (a value type) -- the call is harmless but never throws. The real null-equivalent guard is the `Guid.Empty` check in the AppService.
- **MVP intent: no runtime access decisions should depend on this entity.** See "MVP Intent" section above.

## Inbound FKs

No other entity has a foreign key TO `AppointmentAccessor`. (`HasForeignKey(x => x.AppointmentAccessorId)` matches zero callsites in either DbContext.)

The `AppointmentAccessorAppointment` join entity declares an `AppointmentAccessorId` field as part of its composite key, but is never wired into a DbContext, so it imposes no actual FK relationship.

## Angular UI Surface

No Angular UI. There is no `angular/src/app/appointment-accessors/` folder and no proxy at `angular/src/app/proxy/appointment-accessors/`. This is intentional given the MVP intent: no portal screen exists for granting / revoking ad-hoc access.

The 8 HTTP endpoints in `AppointmentAccessorController` are reachable, but there is no UI calling them today. Callers (if any) are server-side only.

## Known Gotchas

1. **`FullAuditedEntity<Guid>`, not `FullAuditedAggregateRoot<Guid>`.** Deviates from the Reference Pattern (Appointments). Likely intentional -- the entity is treated as a relational link row, not a domain-rich aggregate. Do NOT "fix" this without product approval; see `docs/product/appointment-accessors.md`.
2. **Permissions defined but not enforced.** `CaseEvaluationPermissions.AppointmentAccessors.{Create,Edit,Delete}` exist and are registered, but the AppService uses bare `[Authorize]` everywhere. Tracked by the skipped test `CreateAsync_WhenCallerLacksCreatePermission_ShouldThrow`. Acceptable while the entity is dormant at MVP; MUST be fixed before any post-MVP ACL feature wires real grants.
3. **AppService `[RemoteService(IsEnabled = false)]` BUT controller IS registered.** The AppService disables ABP's auto-controller, then `AppointmentAccessorController` re-exposes the same 8 methods under `api/app/appointment-accessors`. The endpoints are reachable; the AppService attribute is misleading.
4. **`AccessType` enum uses non-sequential values (`View=23`, `Edit=24`).** Suggests legacy / external-system origin. New values must be added explicitly with chosen integer codes -- do not assume zero-based ordering.
5. **`AppointmentAccessorAppointment` is dead code.** Composite-key join entity in the Domain folder, never registered in either DbContext, never used by the repository or AppService. Probably an ABP Suite generation artefact. Leave in place (low cost) or remove together with a product-approved cleanup; do not refactor in isolation.
6. **`Check.NotNull` on `Guid` value-type parameters in the Manager is a no-op.** The actual empty-Guid validation lives in the AppService. If you move the Manager out from behind the AppService, re-add real validation.
7. **`EfCoreAppointmentRepository` may still join against this table.** The original purpose was attorney-scoped filtering of the appointment list. Confirm the actual current behaviour before removing or repurposing this entity, even though the product intent is "ignore at MVP".
8. **Update endpoint is unrestricted-rebind.** `UpdateAsync` allows changing the target `IdentityUserId` and `AppointmentId`, not only `AccessTypeId`. If a future ACL feature treats accessor rows as immutable grants, this endpoint must be tightened.

## Test Coverage

`test/HealthcareSupport.CaseEvaluation.Application.Tests/AppointmentAccessors/AppointmentAccessorsAppServiceTests.cs` (merged today via PR #142). Synthetic seed data only -- HIPAA-clean.

| # | Test | What it proves |
|---|---|---|
| 1 | `GetAsync_ReturnsSeededAccessor` | `Get` returns the seeded row inside its owning tenant |
| 2 | `GetListAsync_FromTenantAContext_ReturnsOnlyAccessor1` | Tenant filter excludes rows owned by other tenants |
| 3 | `GetListAsync_FromTenantBContext_ReturnsOnlyAccessor2` | Symmetric tenant isolation in the opposite direction |
| 4 | `GetListAsync_FromHostContextWithFilterDisabled_ReturnsAccessorsFromBothTenants` | Disabling `IDataFilter<IMultiTenant>` reveals all tenants -- confirms the filter is the gating mechanism |
| 5 | `CreateAsync_PersistsNewAccessor_AsHostScoped` | Host-context creates land with `TenantId = null` and persist |
| 6 | `UpdateAsync_ChangesMutableFields` | View -> Edit flip persists; test restores seed value at teardown |
| 7 | `DeleteAsync_RemovesAccessor` | Delete leaves no row findable via `FindAsync` |
| 8 | `GetIdentityUserLookupAsync_FiltersByEmail` | Email-substring filter returns the expected user |
| 9 | `GetAppointmentLookupAsync_ReturnsAppointmentsInTenant` | Lookup respects tenant boundary -- TenantA caller does not see TenantB's appointment |
| 10 | `CreateAsync_WhenCallerLacksCreatePermission_ShouldThrow` | `[Skip=...]` -- encodes target behaviour for the Known Gotcha #2 fix; will flip live once `[Authorize(... .Create)]` is added |

## Links

- Product intent: [docs/product/appointment-accessors.md](/docs/product/appointment-accessors.md)
- Sibling intents: [applicant-attorneys.md](/docs/product/applicant-attorneys.md), [appointment-applicant-attorneys.md](/docs/product/appointment-applicant-attorneys.md)
- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
