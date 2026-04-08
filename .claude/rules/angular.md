---
paths:
  - "angular/**/*.ts"
  - "angular/**/*.html"
  - "angular/**/*.scss"
---
# Angular Conventions — Patient Portal

- Angular 20 with standalone components (no NgModules)
- Use Angular signals for state management
- Reactive forms preferred over template-driven
- Follow Angular style guide naming: feature.type.ts (e.g., patient-list.component.ts)
- Never use `ng serve` — use the ABP proxy bundler instead (see CLAUDE.md startup sequence)
- Adrian is new to Angular — explain conventions by comparing to React equivalents
- Always cite Angular official docs when introducing new patterns
