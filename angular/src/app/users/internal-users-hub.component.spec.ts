import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalUsersHubComponent } from './internal-users-hub.component';
import { UsersSectionGateway } from './users-section.gateway';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';

/**
 * Covers the Escape-to-close handler added in sweep #648. Both modals could
 * previously be dismissed only with the mouse.
 *
 * The component is created but never change-detected, so the route
 * subscription's `load()` is the only work that runs.
 */
describe('InternalUsersHubComponent Escape handling (sweep #648)', () => {
  interface Probe {
    createForm: { set(value: unknown): void };
    tenantForm: { set(value: unknown): void };
    isBusy: { set(value: boolean): void };
    onEscapeKey(): void;
  }

  const page = () => of({ items: [], totalCount: 0 });

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { data: of({ section: 'invite' }) } },
        {
          provide: UsersSectionGateway,
          useValue: {
            getInviteTenantOptions: page,
            internalUsersPage: page,
            invitesPage: page,
            officesPage: page,
            uploadOfficeLogo: page,
          },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: ImpersonationService, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(InternalUsersHubComponent);
    const inst = fixture.componentInstance as unknown as Probe & {
      createForm(): unknown;
      tenantForm(): unknown;
    };
    return {
      fixture,
      probe: inst as Probe,
      readCreate: () => inst.createForm(),
      readTenant: () => inst.tenantForm(),
    };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('closes the create-user modal', () => {
    const c = create();
    c.probe.createForm.set({ firstName: 'Ada' });
    c.probe.onEscapeKey();
    expect(c.readCreate()).toBeNull();
  });

  it('closes the tenant modal', () => {
    const c = create();
    c.probe.tenantForm.set({ name: 'Falkinstein' });
    c.probe.onEscapeKey();
    expect(c.readTenant()).toBeNull();
  });

  it('closes only the tenant modal when both are open, because it opens over the list', () => {
    const c = create();
    c.probe.createForm.set({ firstName: 'Ada' });
    c.probe.tenantForm.set({ name: 'Falkinstein' });
    c.probe.onEscapeKey();
    expect(c.readTenant()).toBeNull();
    expect(c.readCreate()).not.toBeNull();
  });

  it('does not discard a save in flight', () => {
    // The guard is inherited from the close methods rather than reimplemented.
    const c = create();
    c.probe.createForm.set({ firstName: 'Ada' });
    c.probe.tenantForm.set({ name: 'Falkinstein' });
    c.probe.isBusy.set(true);
    c.probe.onEscapeKey();
    expect(c.readTenant()).not.toBeNull();
    c.probe.tenantForm.set(null);
    c.probe.onEscapeKey();
    expect(c.readCreate()).not.toBeNull();
  });

  it('is inert when nothing is open', () => {
    const c = create();
    expect(() => c.probe.onEscapeKey()).not.toThrow();
    expect(c.readCreate()).toBeNull();
    expect(c.readTenant()).toBeNull();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    // Proves the @HostListener binding, which a direct method call cannot.
    const c = create();
    c.probe.createForm.set({ firstName: 'Ada' });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readCreate()).toBeNull();
  });
});
