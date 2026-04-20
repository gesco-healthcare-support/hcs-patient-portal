[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-01

# FEAT-01: Appointment Status Workflow Has No Implementation -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` (no transition methods)
- `angular/src/app/appointments/` (no status-change UI)

---

## Current state (verified 2026-04-17)

13 enum values exist:

```
Pending = 1, Approved = 2, Rejected = 3, NoShow = 4,
CancelledNoBill = 5, CancelledLate = 6,
RescheduledNoBill = 7, RescheduledLate = 8,
CheckedIn = 9, CheckedOut = 10, Billed = 11,
RescheduleRequested = 12, CancellationRequested = 13
```

Zero enforcement: no state machine, no transition validation, no role-gated transitions, no `TransitionAsync` on `AppointmentManager`, no dedicated API endpoint, no Angular UI widgets. The documented intended transition diagram in `Domain/Appointments/CLAUDE.md` is documentation only.

---

## Official documentation

- [Stateless -- GitHub](https://github.com/dotnet-state-machine/stateless) -- canonical .NET state-machine library. Targets .NET 8/9/10 + netstandard2.0 + net462. Actively maintained. Supports guards, entry/exit actions, hierarchical states, and external state storage (EF Core remains source of truth; Stateless reads/writes the `AppointmentStatus` column via getter/setter lambdas).
- [Stateless Wiki](https://github.com/dotnet-state-machine/stateless/wiki) -- patterns for guards, OnEntry/OnExit side effects, state-change notifications.
- [Nicholas Blumhardt -- Stateless 3.0 release notes](https://nblumhardt.com/2016/11/stateless-30/) -- author's write-up; "Stateless holds no state, you persist it" design makes EF Core integration trivial.
- [Appccelerate.StateMachine](https://github.com/appccelerate/statemachine) -- alternative; appears to be in maintenance mode (issues 2021-2023 open; limited recent commits). Stateless is safer.
- [Microsoft -- Using Enumeration classes instead of enum types](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/enumeration-classes-over-enum-types) -- pairs well with state machines; enum values can carry metadata.
- [Martin Fowler -- State Machine (DSL catalog)](https://martinfowler.com/dslCatalog/stateMachine.html) -- "enum-only transitions work for linear state, break for branching" -- the IME workflow has branches.
- [ABP Local Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/local) -- `AddLocalEvent` publishes `StatusChanged` consumed by handlers (audit log, notifications) on UoW completion.
- [ABP Distributed Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) -- for cross-service consumers (MRR AI, Case Tracking).
- [ABP Angular Permission Management](https://abp.io/docs/latest/framework/ui/angular/permission-management) -- `PermissionService.getGrantedPolicy` + `*abpPermission` directive for hiding transition buttons.
- [ABP Entities -- AddLocalEvent](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities) -- state changes only via methods that guarantee event publication.
- [ABP Elsa Workflows module](https://abp.io/modules/elsa) -- commercial module wrapping [Elsa Workflows](https://github.com/elsa-workflows/elsa-core). Visual designer + ABP permission integration. Heavyweight; appropriate only if business wants no-code workflow edits.
- [ABP community -- Using Elsa Workflow with the ABP Framework](https://community.abp.io/posts/using-elsa-workflow-with-the-abp-framework-773siqi9) + [HireTechTeam -- Integrating Elsa .NET Workflows with ABP Commercial](https://academy.hiretechteam.com/blog/integratingelsanet/) -- integration walkthroughs.

## California regulatory context

- [8 CCR § 31.3 -- Scheduling Appointment with Panel QME](https://www.law.cornell.edu/regulations/california/8-CCR-31.3) -- 90-day scheduling window, 120-day extended window. Triggers like `ScheduleAppointment`, `RequestReplacement`.
- [8 CCR § 33 -- Unavailability of QME](https://www.law.cornell.edu/regulations/california/8-CCR-33) -- defines "unavailable" and "replacement panel" flows (maps to `CancelledNoBill`/`RescheduleRequested`).
- [8 CCR § 30 -- QME Panel Requests](https://www.law.cornell.edu/regulations/california/8-CCR-30) -- panel-request prerequisites. Audit trails should reference panel numbers.
- [DWC -- FAQs on QMEs for physicians](https://www.dir.ca.gov/dwc/medicalunit/faqphys.html) -- confirms no regulatory schema for a portal's internal states; regulations constrain timings only. Design freedom on state names.

## Community findings

- [Medium -- Implementing State Machines in C# Using Stateless (Ramazan Gunes)](https://gunesramazan.medium.com/implementing-state-machines-in-c-using-stateless-a-step-by-step-guide-641e35133134) -- tutorial: guards, entry actions, external state storage.
- [Medium (vano4ok) -- State modelling in DDD with EF Core](https://medium.com/@vano4ok/state-modelling-in-ddd-with-entity-framework-core-c65cb8ee4a21) -- "state + transition method on aggregate" with EF Core persistence.
- [Alibaba Cloud -- DSLs: Stateless State Machines](https://www.alibabacloud.com/blog/unlocking-the-power-of-dsls-stateless-state-machines_596467) -- larger-scale patterns and monitoring.
- [abpframework/abp #16744 -- State Management API](https://github.com/abpframework/abp/issues/16744) -- confirms ABP has no built-in state-machine primitive on the roadmap.
- [elsa-workflows #5214 -- Integrating Elsa with ABP Commercial](https://github.com/elsa-workflows/elsa-core/discussions/5214) -- active integration thread.
- [abpframework/abp #2737 -- Angular template permission check](https://github.com/abpframework/abp/issues/2737) -- hiding buttons by permission from templates.
- [ABP Community -- App Services vs Domain Services](https://abp.io/community/articles/app-services-vs-domain-services-deep-dive-into-two-core-service-types-in-abp-framework-4dvau41u) -- transition logic (FEAT-01) belongs in a domain service, not the app service.
- [Engin Can Veske -- Using Elsa 3 with the ABP Framework](https://engincanveske.substack.com/p/using-elsa-3-with-the-abp-framework) -- Elsa for ABP 8+.
- [Kamil Grzybek -- Handling concurrency with aggregates and EF Core](https://www.kamilgrzybek.com/blog/posts/handling-concurrency-aggregate-pattern-ef-core) -- `ConcurrencyStamp` validated per transition; prevents races where two users approve + check-in simultaneously.
- [Stateless #248 -- Transitioning to a superstate](https://github.com/dotnet-state-machine/stateless/issues/248) -- relevant if the 13 states group (Cancelled variants as substates of Cancelled).

## Recommended approach (research converges)

**Pattern**: Stateless for transition rules, EF Core for storage, ABP local events for side effects.

1. Use [Stateless](https://github.com/dotnet-state-machine/stateless). Stateless holds no state; it reads the enum via a getter lambda, writes via a setter lambda. `AppointmentManager` owns the `StateMachine` instance per-aggregate.
2. Model the 13 states as `TState`; model triggers (verbs: `Approve`, `CheckIn`, `CheckOut`, `Bill`, `MarkNoShow`, `CancelLate`, `Reschedule`) as `TTrigger`. The documented diagram in `Domain/Appointments/CLAUDE.md` maps directly onto `Configure(state).Permit(trigger, nextState)`.
3. Wrap each transition as a method on the `Appointment` aggregate root: `appointment.CheckIn()`, `appointment.Bill()` -- not `appointment.AppointmentStatus = CheckedIn`. Each method calls `stateMachine.Fire(Trigger.X)`, which enforces legal transitions; then emits a domain event via `AddLocalEvent(new AppointmentStatusChangedEto(...))`.
4. AppService exposes one endpoint per transition (or one generic endpoint accepting a trigger) with its own permission: `Appointments.CheckIn`, `Appointments.Bill`, etc. Angular uses `PermissionService.getGrantedPolicy` + current-status + `GetPermittedTriggers()` from Stateless to render correct action buttons.
5. **Audit trail**: add a thin `AppointmentStatusHistory` entity (Id, AppointmentId, FromStatus, ToStatus, ChangedBy, ChangedAt, Reason) populated by a `LocalEventHandler<AppointmentStatusChangedEto>`. ABP's `FullAuditedAggregateRoot` captures whole-entity modification, not per-transition detail.
6. **Do NOT use Elsa** for this feature unless the business asks for visual workflow editing. Elsa adds Blazor designer + full workflow runtime for what is a 13-state table.

**Product decision required**: the role-to-trigger permission matrix (who can Approve, CheckIn, Bill). Engineering cannot invent this.

## Gotchas / blockers

- Stateless is thread-unsafe per-instance -- construct one state machine per aggregate load (cheap), do not cache.
- EF Core concurrency: if the domain service loads the aggregate and fires a trigger, `SaveChanges` must respect `ConcurrencyStamp` or two clients racing `Approve` lose a transition.
- ABP local events fire on UoW completion (not inline). If a side effect must happen before commit (e.g. reserving a slot), do it inline in the transition method, not in a handler.
- `AddLocalEvent` publishes only on successful persistence; if persistence fails, the event is dropped. Usually desired, but document.
- Angular should NOT hardcode transition rules client-side. Fetch a "allowed triggers for this appointment" list from the server (`GET /api/app/appointments/{id}/allowed-triggers`) and render buttons from that. Stateless exposes `GetPermittedTriggers()`.
- Permissions: endpoint-level covers "can user attempt this trigger at all"; state-gate ("this trigger not valid from current state") is a separate `Fire` throw. Different HTTP responses (401/403 vs 409) for each.
- Mapperly + state machine: keep `AppointmentStatus` a plain enum value on DTOs. State machine is orchestration, not serialisation.
- Regulatory deadlines (90/120 days from 8 CCR § 31.3) are time-based triggers, not user-driven. If auto-transition on timer is needed, use an ABP background worker, not a user endpoint.

## Open questions

- Which role can fire which trigger? (Typical split: front-desk = CheckIn/CheckOut, doctor = complete exam, billing = Bill, admin = override.)
- Are any transitions automatic (time-based)? Auto-move `Pending` -> `Rejected` if not approved within N hours?
- `RescheduleRequested` fireable from both `Pending` and `Approved`, or only `Approved`? Existing diagram incomplete.
- `CancelledLate` vs `CancelledNoBill` -- system-chosen (time-to-appointment) or user-picked? Determines if Cancel is one trigger with a guard or two separate triggers.
- Are transitions ever reversible (undo), or append-only for audit/legal reasons?
- Should `AppointmentStatusHistory` be tenant-scoped? (Yes, probably -- `Appointment` is `IMultiTenant`.)
- Cross-service notifications (MRR AI, Case Tracking) when status changes? Local vs Distributed event bus.

## Related

- [BUG-02](BUG-02.md) -- fix together; BUG-02 is the data-shape half, FEAT-01 is the workflow half
- [DAT-05](DAT-05.md) -- decide enum vs lookup first
- [DAT-03](DAT-03.md) -- some transitions must release slots (Cancelled*, NoShow)
- Q1, Q2 in [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md)
- [docs/issues/INCOMPLETE-FEATURES.md#feat-01](../INCOMPLETE-FEATURES.md#feat-01-appointment-status-workflow-has-no-implementation)
