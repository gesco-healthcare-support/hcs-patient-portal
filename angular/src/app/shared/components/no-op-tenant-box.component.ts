import { Component } from '@angular/core';

/**
 * 2026-05-06 -- empty replacement for ABP's TenantBoxComponent on the SPA
 * `/account/*` pages. Mirrors the AuthServer-side fix
 * (commit 59dfbb2 -- "hide LeptonX TenantBox via empty Razor partial").
 *
 * Reason: Phase 1A Falkinstein is single-tenant. The tenant is resolved
 * from the subdomain by DomainTenantResolveContributor (ADR-006); exposing
 * a tenant switcher on the SPA login / register pages would let an
 * unauthenticated visitor target a tenant that doesn't match their
 * subdomain, breaking the affordance contract. OLD has zero tenant UI
 * anywhere (`P:\PatientPortalOld\patientappointment-portal\src\app\` --
 * grep for `tenant` returns nothing); replicating that.
 *
 * Wired in `app.config.ts` via `ReplaceableComponentsService.add({ key:
 * eAccountComponents.TenantBox, component: NoOpTenantBoxComponent })`.
 */
@Component({
  selector: 'app-no-op-tenant-box',
  standalone: true,
  template: '',
})
export class NoOpTenantBoxComponent {}
