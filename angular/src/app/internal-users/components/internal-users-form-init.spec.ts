import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';

import { InternalUsersFormComponent } from './internal-users-form.component';

/**
 * #628 split this component's `async ngOnInit(): Promise<void>` into a void
 * ngOnInit that discards a private async loadTenants (typescript:S6544).
 * OnInit declares a void return, so the Promise was something Angular silently
 * dropped.
 *
 * The change is structural, but "structural" is exactly the kind of claim that
 * should be executed: the two tenant paths have to behave the same afterwards.
 * Both are pinned here, and neither had any coverage before.
 */
describe('InternalUsersFormComponent initialisation (#628)', () => {
  interface Probe {
    tenants: { (): Array<{ id: string; displayName: string }> };
    tenantLocked: { (): boolean };
    tenantsLoading: { (): boolean };
    form: { getRawValue(): Record<string, unknown> };
    ngOnInit(): void;
  }

  function create(opts: {
    currentTenant?: { id: string; name: string } | null;
    tenantList?: Array<{ id: string; displayName: string }>;
    fail?: boolean;
  }): Probe {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: Router, useValue: { navigate: () => undefined } },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) =>
              k === 'currentTenant' ? (opts.currentTenant ?? { id: null, name: null }) : null,
            getAll: () => ({}),
          },
        },
        {
          provide: RestService,
          useValue: {
            request: () =>
              opts.fail
                ? throwError(() => new Error('tenant lookup down'))
                : of({ items: opts.tenantList ?? [], totalCount: (opts.tenantList ?? []).length }),
          },
        },
      ],
    });
    return TestBed.runInInjectionContext(
      () => new InternalUsersFormComponent(),
    ) as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('locks a tenant admin to their own tenant without a lookup', () => {
    const c = create({ currentTenant: { id: 't-1', name: 'Falkinstein' } });

    c.ngOnInit();

    expect(c.tenantLocked()).toBeTrue();
    expect(c.tenants()).toHaveSize(1);
    expect(c.tenantsLoading()).toBeFalse();
    expect(c.form.getRawValue()['tenantId']).toBe('t-1');
  });

  it('loads the full list for a host admin', async () => {
    const c = create({
      currentTenant: null,
      tenantList: [
        { id: 't-1', displayName: 'Falkinstein' },
        { id: 't-2', displayName: 'Hekmat' },
      ],
    });

    c.ngOnInit();
    await Promise.resolve();
    await Promise.resolve();

    expect(c.tenantLocked()).toBeFalse();
    expect(c.tenants()).toHaveSize(2);
    expect(c.tenantsLoading()).toBeFalse();
  });

  /**
   * The discarded Promise must not be able to surface as an unhandled
   * rejection. A tenant-load failure is non-blocking by design -- the required
   * validator on tenantId stops the submit -- so this asserts the page still
   * settles rather than throwing past ngOnInit.
   */
  it('settles quietly when the tenant lookup fails', async () => {
    const c = create({ currentTenant: null, fail: true });

    expect(() => c.ngOnInit()).not.toThrow();
    await Promise.resolve();
    await Promise.resolve();

    expect(c.tenants()).toHaveSize(0);
    expect(c.tenantsLoading()).toBeFalse();
  });
});
