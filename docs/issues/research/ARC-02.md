[Home](../../INDEX.md) > [Issues](../) > Research > ARC-02

# ARC-02: Business Logic in Application Service Layer -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs`

---

## Current state (verified 2026-04-17)

`AppointmentsAppService.CreateAsync` performs 7 distinct validation/coordination checks plus confirmation-number generation and slot-state mutation:

- FK existence (lines 189-217)
- Slot availability check (line 219)
- Location match (224)
- Type match (229)
- Date alignment (234)
- Time-within-slot (240)
- Confirmation number generation (254-282)
- Slot state mutation (248-249)

None of this is in `AppointmentManager`. Textbook anemic-domain pattern -- conflicts with ABP's Manager convention.

---

## Official documentation

- [ABP Application Services](https://abp.io/docs/en/abp/latest/Application-Services) -- thin services, orchestrate only, return DTOs, delegate to Managers.
- [ABP Domain Services](https://abp.io/docs/en/abp/latest/Domain-Services) -- core business logic, manager suffix, mutate state, throw `BusinessException`.
- [ABP Domain Services best practices](https://abp.io/docs/latest/framework/architecture/best-practices/domain-services) -- entities should not have public setters for invariant-protected fields; Manager enforces cross-entity rules.
- [MS DDD microservice domain model](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model) -- behaviours on aggregate root; services only for cross-aggregate behaviour.

## Community findings

- [ABP Community -- App Services vs Domain Services Deep Dive](https://abp.io/community/articles/app-services-vs-domain-services-deep-dive-into-two-core-service-types-in-abp-framework-4dvau41u) -- AppService = use-case orchestrator; DomainService = business logic.
- [ABP Community -- What is That Domain Service in DDD](https://abp.io/community/articles/what-is-that-domain-service-in-ddd-for-.net-developers-uqnpwjja)
- [Volosoft LinkedIn -- Domain/App layers in ABP](https://www.linkedin.com/pulse/understanding-domain-application-layers-abp-framework-volosoft-iawhf)
- [Martin Fowler -- Anemic Domain Model](https://martinfowler.com/bliki/AnemicDomainModel.html) -- logic-in-service-layer smell.
- [Vernon -- Effective Aggregate Design Part II](https://www.dddcommunity.org/wp-content/uploads/files/pdf_articles/Vernon_2011_2.pdf) -- invariants inside the consistency boundary.

## Recommended approach

Incremental 4-phase migration, low-risk:

1. **Phase 1**: add invariant guards to entity constructor (`fromTime < toTime`, required FKs, confirmation-number pattern). No behaviour change -- only surfaces already-failed states earlier.
2. **Phase 2**: create Manager methods accepting domain objects, call from AppService, keep AppService guards as belt-and-braces. Land tests on Manager.
3. **Phase 3**: remove AppService guards once Manager coverage is green. AppService becomes thin orchestrator (~15 lines).
4. **Phase 4**: promote setter visibility to `internal`, switch Mapperly to factory/constructor mapping.

Target shape for `AppointmentsAppService.CreateAsync`: map DTO -> entity via Mapperly, authorise, call `_appointmentManager.CreateAsync(entity)`, insert via repository, return DTO.

## Gotchas / blockers

- `CurrentTenant`, `CurrentUser`, and `Clock` are base properties on `ApplicationService` but NOT on `DomainService`. Pass required values into Manager methods as parameters -- ABP best-practices page explicitly calls this out.
- Manager tests can use `AbpEntityFrameworkCoreSqliteModule` without HTTP pipeline but still need DI container; use existing `CaseEvaluationDomainTestBase`.
- Swapping to internal setters may break existing Mapperly mappers that depend on public setters; Mapperly supports ctor-based mapping but each mapper partial class needs updating.
- Confirmation number collisions: hoisting into Manager is fine but uniqueness check must be in Manager (talks to repo) -- otherwise race conditions. Use unique index + retry loop (see [DAT-02](DAT-02.md)).

## Open questions

- Is there a session/tenant context requirement inside the Manager (e.g. data filter disable)? If so, pass result of `CurrentTenant.Id` into Manager, not `using CurrentTenant.Change` inside it.
- Are Mapperly mappers configured with constructor mapping today, or default property assignment? Affects refactor scope.
- Existing 9 injected repositories in `AppointmentsAppService` suggest it has become a dumping ground; after moving logic to Manager, which repositories are still needed?

## Related

- [BUG-09](BUG-09.md), [BUG-10](BUG-10.md) -- invariants that belong in Manager
- [DAT-01](DAT-01.md), [DAT-02](DAT-02.md), [DAT-03](DAT-03.md) -- all point to missing Manager-level coordination
- [FEAT-01](FEAT-01.md) -- state machine also lives in Manager
- [docs/issues/ARCHITECTURE.md#arc-02](../ARCHITECTURE.md#arc-02-business-logic-in-the-application-service-layer-not-domain)
