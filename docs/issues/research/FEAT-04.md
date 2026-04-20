[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-04

# FEAT-04: AppointmentEmployerDetail and AppointmentAccessor Have No Angular Modules -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- Backend present: `src/.../Domain/AppointmentEmployerDetails/`, `src/.../Domain/AppointmentAccessors/`, AppServices, Controllers, DTOs, permissions
- Generated proxies: `angular/src/app/proxy/` (both entities)
- Missing: any feature module under `angular/src/app/` for `accessor` or `employer`

---

## Current state (verified 2026-04-17)

`ls angular/src/app/ | grep -i "accessor\|employer"` returns empty. Both entities have complete backend (AppService, Controller, permissions) + ABP-generated Angular proxy services. No Angular feature module directory, no standalone list page, no detail modal, no route.

`AppointmentApplicantAttorney` is in a similar state but partially managed inline via `AppointmentController.UpsertApplicantAttorneyForAppointment` accessible from `AppointmentViewComponent`. Accessor/EmployerDetail have no UI entry point at all.

---

## Official documentation

- [ABP Suite overview](https://abp.io/docs/latest/release-info/road-map) -- ABP Suite is the CRUD-page generator; 10.3 planned stable April 2026.
- [ABP Suite entity generation docs (Commercial)](https://abp.io/docs/commercial/latest/abp-suite) -- Suite generates Angular pages when "Create user interface" is ticked during entity definition.
- [ABP Angular Service Proxies](https://abp.io/docs/latest/framework/ui/angular/service-proxies) -- proxies auto-generate; feature components/routes do NOT.

## Community findings

- [ABP Support #10124 -- Unable to create Proxy or Angular components](https://abp.io/support/questions/10124/Unable-to-create-neither-Proxy-or-Angular-components-for-newly-created-entities-from-ABP-Suite) -- Suite can skip Angular UI generation for entities defined inside modules.
- [ABP Support #9365 -- Suite with Modular DDD and Layered Angular](https://abp.io/support/questions/9365/ABP-Suite-with-Modular-DDD-and-Layered-Project-Angular-How-to-Create-UI-in-Main-Project-When-Using-Modules) -- Suite doesn't automatically place UI in the main app when entity lives in a module; manual wiring needed.
- [ABP Support #8074 -- Issue generating CRUD page for Angular UI](https://abp.io/support/questions/8074/Issue-in-generating-CRUD-page-for-Angular-UI-with-Abp-Suite) -- Suite re-generation workflow; Angular UI flag is per-entity.
- [ABP Support #3424 -- Cannot generate angular CRUD pages](https://abp.io/support/questions/3424/Cannot-generate-angular-CRUD-pages-using-abp-suite-and-cli) -- Suite's Angular generation is fragile and silently skips.

## Recommended approach

**Option B (embed inline) is the safer, feature-appropriate choice** for Accessor and EmployerDetail. Both are conceptually per-appointment:
- Accessor grants access to a specific appointment.
- EmployerDetail belongs to a specific appointment.

Nesting them in `AppointmentViewComponent` matches the working `AppointmentApplicantAttorney` precedent and preserves context.

Follow the repo's existing abstract/concrete component pattern (see `src/.../Application/CLAUDE.md`): abstract component holds form + validation + service wiring; concrete component slots into the view tab.

If ABP Suite regeneration is ever on the roadmap, note the caveat: Suite regenerates only if entity is defined via Suite; entities hand-authored (likely the case here) will never get Suite-generated UI.

## Gotchas / blockers

- Inline tabs in `AppointmentViewComponent` inflate file size. Project's enforced ceiling is 250 lines for Angular components (per Adrian's code-standards). Consider child standalone components loaded via route-lazy or `@defer`.
- `AppointmentAccessor` is access-control data; exposing it inline means the form must respect permission checks (`PermissionService.getGrantedPolicy$`) so non-owners don't see grant/revoke controls.
- Regenerating proxies (`abp generate-proxy`) doesn't touch feature modules -- no risk of overwrite, but no scaffolding help either.

## Open questions

- Is there an existing pattern in this repo for permission-scoped inline tabs (does `AppointmentApplicantAttorney` tab hide for certain roles)? Verify in `appointment-view.component.ts`.
- Will Adrian ever run ABP Suite against this repo? If not, the "Suite regenerates" concern is moot.
- Does business want inline (per-appointment) or standalone management for these entities? Option B chosen based on domain fit, but confirm.

## Related

- [FEAT-03](FEAT-03.md) -- dashboard might surface accessor/employer counts
- [BUG-12](BUG-12.md) -- similar "incomplete UI polish"
- [docs/issues/INCOMPLETE-FEATURES.md#feat-04](../INCOMPLETE-FEATURES.md#feat-04-appointmentemployerdetail-and-appointmentaccessor-have-no-angular-modules)
