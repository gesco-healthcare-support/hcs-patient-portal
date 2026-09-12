---
paths:
  - "angular/**/*.ts"
  - "angular/**/*.html"
  - "angular/**/*.scss"
---
# Angular Conventions -- Appointment Portal

- Angular 20 with standalone components (no NgModules)
- Use Angular signals for state management
- Reactive forms preferred over template-driven
- Follow Angular style guide naming: feature.type.ts (e.g., patient-list.component.ts)
- IMPORTANT: Never run `ng serve`, `yarn start`, or `ng build --watch`. Vite duplicates
  the `CORE_OPTIONS` InjectionToken, which breaks ABP DI with a `NullInjectorError`.
  Dev serve: `npx ng build --configuration development` then
  `npx serve -s dist/CaseEvaluation/browser -p 4200`.
- `angular/src/app/proxy/` is auto-generated; regenerate via `abp generate-proxy` after
  any backend DTO or AppService change. Never hand-edit files under `proxy/`.
- Adrian is new to Angular -- explain conventions by comparing to React equivalents
- Always cite Angular official docs when introducing new patterns
