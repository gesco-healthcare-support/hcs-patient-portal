import { ABP } from '@abp/ng.core';

// IP3 (2026-06-05): the Doctor entity is kept DORMANT (the office IS the doctor; nothing
// operational reads a Doctor row). The Doctors nav item is hidden by registering no menu
// route. The lazy route in app.routes.ts is retained so the dormant feature still compiles;
// revisit if a multi-doctor-per-tenant model returns to scope (see ADR-004).
export const DOCTOR_BASE_ROUTES: ABP.Route[] = [];
