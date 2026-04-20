[Home](../../INDEX.md) > [Issues](../) > Research > ARC-08

# ARC-08: Missing [RemoteService(IsEnabled = false)] on 3 AppServices -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs` (inherits `Volo.Saas.Host.TenantAppService`)
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` (explicitly flagged in its CLAUDE.md Gotcha #2)
- `src/HealthcareSupport.CaseEvaluation.Application/Users/UserExtendedAppService.cs` (existence needs `ls` verification)

---

## Current state (verified 2026-04-17)

Grep for `RemoteService(IsEnabled` across `Application/`:
- 14 AppServices HAVE the attribute (AppointmentsAppService, DoctorAvailabilitiesAppService, ApplicantAttorneysAppService, AppointmentAccessorsAppService, AppointmentApplicantAttorneysAppService, AppointmentEmployerDetailsAppService, AppointmentTypesAppService, AppointmentStatusesAppService, AppointmentLanguagesAppService, DoctorsAppService, LocationsAppService, PatientsAppService, StatesAppService, WcabOfficesAppService, BookAppService).
- 2 confirmed missing: `DoctorTenantAppService`, `ExternalSignupAppService`.
- `UserExtendedAppService` not appearing in grep -- either missing the attribute OR the file doesn't exist. Verify first.

Per [ADR-002](../decisions/002-manual-controllers-not-auto.md), every AppService in this project should carry `[RemoteService(IsEnabled = false)]` to prevent ABP from auto-generating duplicate routes alongside the manual controllers in `HttpApi/`.

---

## Official documentation

- [ABP Auto API Controllers](https://abp.io/docs/latest/framework/api-development/auto-controllers) -- how discovery works; `ConventionalControllers.Create(assembly)`; `TypePredicate` filter; `[RemoteService(IsEnabled = false)]` opt-out.
- [ABP Auto API Controllers (legacy URL)](https://docs.abp.io/en/abp/latest/API/Auto-API-Controllers)
- [ABP Customizing Modules -- Overriding Services](https://abp.io/docs/3.3/Customizing-Application-Modules-Overriding-Services) -- explicitly: when subclassing and re-registering, add `[RemoteService(IsEnabled = false)]` to avoid duplicate routes.

## Community findings

- [abpframework/abp #4052](https://github.com/abpframework/abp/issues/4052) -- doc-level reminder to disable RemoteService when customising modules.
- [abpframework/abp #5269](https://github.com/abpframework/abp/issues/5269) -- even `[RemoteService(IsEnabled = false, IsMetadataEnabled = false)]` can still trigger `AmbiguousMatchException` depending on auto-API registration order.
- [abpframework/abp #6362](https://github.com/abpframework/abp/issues/6362) -- `IsMetadataEnabled = false` ignored for controllers with API versioning (edge case).
- [abpframework/abp #16832](https://github.com/abpframework/abp/issues/16832) -- clarifying `RemoteServiceAttribute` semantics.
- [ABP support #1076 -- Custom Controller creating 2 end points](https://abp.io/support/questions/1076/Custom-Controller-creating-2-end-points) -- classic duplicate-route symptom.

## Recommended approach

1. First, **verify `UserExtendedAppService` exists** via `ls src/.../Application/Users/`. If it doesn't, strike from the list.
2. Add `[RemoteService(IsEnabled = false)]` on each confirmed service. For `DoctorTenantAppService` inheriting `Volo.Saas.Host.TenantAppService`, the attribute goes on the derived class; `IRemoteService` inheritance is transitive but the attribute is evaluated on the concrete registered class.
3. Establish a convention via a project-level Roslyn analyser OR a simple unit test iterating all loaded AppServices and asserting the attribute is present. Prevents regression.
4. After fix, run `abp generate-proxy` if any client depended on the stale auto-generated endpoint.

## Gotchas / blockers

- `[RemoteService(IsEnabled = false)]` does NOT un-register the service from DI -- only tells auto-controller discovery to skip. Manual controllers still delegate to the AppService via injection.
- If an auto-controller already existed at a different route (e.g. `/api/app/external-signup` auto vs `/api/app/external-signups` manual), existing clients may have bound to the wrong one. Grep Angular proxy for stale endpoint names; regenerate.
- If the project uses `ConventionalControllers.Create(assembly, opts => opts.TypePredicate = t => ...)`, verify the predicate doesn't exclude these three -- if it does, the attribute is redundant but harmless.
- `UserExtendedAppService` may override an ABP Identity module service; in that scenario ABP docs specifically require the attribute to avoid inheriting the module's auto-generated controller.
- Do not confuse `IsMetadataEnabled = false` (hides from Swagger) with `IsEnabled = false` (prevents auto-wiring).

## Open questions

- Does `UserExtendedAppService` actually exist at `src/.../Application/Users/`? Verify first.
- Has the manual controller for each of these services been in production long enough that removing the auto route would break existing clients? Unlikely (Angular client is in-repo and regenerated) but worth confirming.
- Is there value writing the convention-check unit test now (5-min guardrail) vs folding into a larger [ARC-02](ARC-02.md) refactor PR?

## Related

- [ADR-002: Manual controllers](../../decisions/002-manual-controllers-not-auto.md)
- [src/.../Application/CLAUDE.md convention #2](../../../src/HealthcareSupport.CaseEvaluation.Application/CLAUDE.md)
- [docs/issues/ARCHITECTURE.md#arc-08](../ARCHITECTURE.md#arc-08-missing-remoteserviceisenabled--false-on-3-appservices)
