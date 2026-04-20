[Home](../../INDEX.md) > [Issues](../) > Research > BUG-02

# BUG-02: Appointment Status Changes Are Never Persisted -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentUpdateDto.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` lines 39-59
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` line 312
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts`

---

## Current state (verified 2026-04-17)

Verified the DTO has no `AppointmentStatus` property:

```csharp
public class AppointmentUpdateDto : IHasConcurrencyStamp
{
    public string? PanelNumber { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string RequestConfirmationNumber { get; set; } = null!;
    public DateTime? DueDate { get; set; }
    public Guid PatientId { get; set; }
    public Guid IdentityUserId { get; set; }
    public Guid AppointmentTypeId { get; set; }
    public Guid LocationId { get; set; }
    public Guid DoctorAvailabilityId { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
}
```

`AppointmentManager.UpdateAsync` has no `appointmentStatus` parameter. Angular sends it anyway and it is silently dropped by the deserialiser. E2E test B11.1.1 confirmed.

This is a data-shape gap, but the right fix overlaps with [FEAT-01](FEAT-01.md) -- status changes should not be free-text updates, they should be state-machine transitions.

---

## Official documentation

- [ABP Data Transfer Objects](https://docs.abp.io/en/abp/latest/Data-Transfer-Objects) -- DTO minimalism. Update DTOs should contain exactly what the use case mutates; omission = "not updatable via this use case." Omission is correct IF the intent is documented, which it is not here.
- [ABP Application Services best practices](https://docs.abp.io/en/abp/latest/Best-Practices/Application-Services) -- recommends separate use-case methods (`ChangeStatusAsync`) rather than fat update DTOs.
- [ABP Entities (always-valid invariants)](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities) -- aggregates must be mutated via methods that enforce invariants, not property setters in the app service.
- [Riok.Mapperly -- RequiredMappingStrategy.Target](https://mapperly.riok.app/docs/configuration/required-mapping/) -- the existing mapper uses `RequiredMappingStrategy.Target`, which does NOT fail the build when a source property is missing. Why the missing field silently compiled.

## Community findings

- [ABP Community -- App Services vs Domain Services Deep Dive](https://abp.io/community/articles/app-services-vs-domain-services-deep-dive-into-two-core-service-types-in-abp-framework-4dvau41u) -- status/lifecycle changes belong in the domain service, not a generic `UpdateAsync`.
- [CodeOpinion -- Aggregate (Root) Design: Behavior & Data](https://codeopinion.com/aggregate-root-design-behavior-data/) -- CRUD-shaped update method is the wrong place for a lifecycle event.
- [Kamil Grzybek -- Handling concurrency with aggregates and EF Core](https://www.kamilgrzybek.com/blog/posts/handling-concurrency-aggregate-pattern-ef-core) -- `ConcurrencyStamp` must be validated on every transition to prevent two users approving + checking-in simultaneously.
- [Mapperly discussion #162 -- Map property using specific method](https://github.com/riok/mapperly/discussions/162) -- if you insist on keeping status on the update DTO, use custom mapping methods; but this signals bad design.

## Recommended approach

**Do NOT add `AppointmentStatus` to `AppointmentUpdateDto`.** Research converges on: status transitions are use cases, not CRUD. Fix BUG-02 by building FEAT-01 simultaneously:

1. Add a dedicated `ChangeStatusAsync(Guid id, AppointmentStatusType next, string concurrencyStamp)` method on `AppointmentsAppService` backed by a domain-service method on `AppointmentManager` that enforces transitions (see [FEAT-01](FEAT-01.md) for state-machine design).
2. Angular's `appointment-view.component` should stop sending status in the PUT body and call the new endpoint instead. Remove `appointmentStatus` from generated proxy DTO after regeneration.
3. Wire a new permission per transition (e.g. `Appointments.Approve`, `Appointments.CheckIn`, `Appointments.Bill`) -- see FEAT-01.
4. If a short-term "just make the field work" patch is needed while FEAT-01 is being designed, add `AppointmentStatus` to `AppointmentUpdateDto` + `AppointmentManager.UpdateAsync` with NO transition validation. Document this as TEMP in the commit message. This pattern will bite again for other lifecycle fields.

## Gotchas / blockers

- The `ConcurrencyStamp` must propagate to the new endpoint or optimistic concurrency is lost.
- Angular proxy regeneration will overwrite any hand-edit to the DTO -- the C# DTO is the source of truth.
- ABP's `[RemoteService(IsEnabled = false)]` pattern (project convention per root CLAUDE.md) requires a matching manual controller method for the new endpoint.
- Permission gating: the existing `Appointments.Edit` permission covers all fields. A status change deserves its own permission (see FEAT-01).
- Behaviour-change test coverage: no existing test covers status changes specifically; add tests as part of the fix.

## Open questions

- Should any user with `Appointments.Edit` be able to change status, or only specific roles per transition?
- Is status change an event worth audit-logging separately from the entity's `ModificationTime`? (Yes -- see FEAT-01's `AppointmentStatusHistory` proposal.)
- Should existing hardcoded status at creation (`AppointmentCreateDto.AppointmentStatus`) be locked to `Pending` to force all subsequent transitions through the new endpoint?

## Related

- [FEAT-01](FEAT-01.md) -- status workflow; fix them together
- [DAT-05](DAT-05.md) -- decide enum vs lookup before persisting transitions
- Q1 in [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md#q1-what-is-the-intended-appointment-status-workflow)
- [docs/issues/BUGS.md#bug-02](../BUGS.md#bug-02-appointment-status-changes-are-never-persisted)
