[Home](../../INDEX.md) > [Issues](../) > Research > FEAT-03

# FEAT-03: Tenant Dashboard Is a Placeholder -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.ts`
- `angular/src/app/dashboard/tenant-dashboard/tenant-dashboard.component.html`

---

## Current state (verified 2026-04-17)

Component class: `export class TenantDashboardComponent {}` (empty).
Template: `<div class="card-body">Add your Tenant related charts/widgets to this page !</div>` (single placeholder div).

Host dashboard (`host-dashboard.component`) is fully implemented with ABP commercial widgets (error rate, execution duration, edition usage, latest tenants).

This matches [ABP's own commercial startup template](https://github.com/abpio/abp-commercial-docs/blob/dev/en/startup-templates/application/solution-structure.md) -- HostDashboard ships with four widgets; TenantDashboard is deliberately left empty for the developer.

---

## Official documentation

- [ABP Commercial startup-templates solution structure](https://github.com/abpio/abp-commercial-docs/blob/dev/en/startup-templates/application/solution-structure.md) -- confirms Host vs Tenant dashboard split; Host ships with four widgets, Tenant is empty.
- [LeptonX Angular UI](https://abp.io/docs/commercial/8.1/themes/lepton-x/angular) -- theme docs; LeptonX has no opinion on widget content.
- [ABP Angular Config State Service](https://abp.io/docs/latest/framework/ui/angular/config-state-service) -- `ConfigStateService` provides tenant info and `getOne$` for reactive config.
- [ABP Angular Permission Management](https://abp.io/docs/latest/framework/ui/angular/permission-management) -- `PermissionService.getGrantedPolicy$('CaseEvaluation.Appointments.Default')` for conditional widget display.
- [ABP Widgets (MVC-only docs)](https://abp.io/docs/latest/framework/ui/mvc-razor-pages/widgets) -- Angular has no analogous `IWidgetManager`; note the gap.

## Community findings

- [ABP Support #858 -- How to create an Angular dashboard widget](https://abp.io/support/questions/858/How-can-I-create-a-new-angular-dashboard-widget-in-abpio-Documentation-is-only-for-Mvc) -- ABP team's official answer: "There is nothing special in server-side for widgets if you are using Angular UI." Pattern: create a normal API endpoint, build an Angular component, look at `host-dashboard.component` as template. Mentions `chartJsLoaded$` from Chart.js integration.
- [GitHub Discussion #6246 -- Customisability of ABP Commercial Team Edition](https://github.com/abpframework/abp/discussions/6246) -- confirms the four Host dashboard widgets have no source available (packaged), but Tenant is a normal component you own.
- [ASP.NET Zero -- Developing Angular Customizable Dashboard](https://docs.aspnetzero.com/aspnet-core-angular/latest/Developing-Angular-Customizable-Dashboard) -- different product, similar widget-composition pattern; useful design reference.

## Recommended approach

1. Build the Tenant dashboard as a plain Angular standalone component composing feature-aligned widgets:
   - Today's appointments count (KPI)
   - Upcoming availability slots (KPI)
   - Recent status-change list (list)
   - 30-day appointment volume chart (Chart.js)
2. Back each widget with a narrow AppService endpoint (e.g. `IDashboardAppService.GetTodayAppointmentsCountAsync`) rather than overloading `AppointmentsAppService` with dashboard concerns.
3. Use `PermissionService.getGrantedPolicy$` per widget so, e.g., a doctor-scoped user only sees widgets they can query.

## Gotchas / blockers

- No `WidgetManager`/`WidgetConfig` registration API in ABP Angular -- MVC docs do not translate. Plan as ordinary components.
- Commercial Host widgets are closed-source; can't copy their internals as templates -- `host-dashboard.component.ts` is the only visible example.
- Chart.js is the implied default for visualisations in ABP Angular dashboards ([source](https://abp.io/support/questions/858/How-can-I-create-a-new-angular-dashboard-widget-in-abpio-Documentation-is-only-for-Mvc)); adding a second chart lib duplicates bundle size. INFERENCE: verify `npm ls chart.js` in the workspace.

## Open questions

- Does ABP 10.0 still bundle Chart.js via `@volo/abp.ng.account` or similar? INFERENCE: yes; verify before picking a chart lib.
- Should widgets be per-role (Doctor sees doctor-specific, Staff sees staff-specific) or single tenant-wide layout? Design decision.
- What KPIs does Gesco's business team actually want on the tenant dashboard? Not derivable from code.

## Related

- [FEAT-07](FEAT-07.md) -- dashboard endpoints need tests
- [docs/issues/INCOMPLETE-FEATURES.md#feat-03](../INCOMPLETE-FEATURES.md#feat-03-tenant-dashboard-is-a-placeholder)
