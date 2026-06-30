import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { InternalShellLayoutComponent } from './internal-shell-layout.component';
import { InternalNavBadgeService } from '../../services/internal-nav-badge.service';
import { BrandingService } from '../../branding/branding.service';
import { InternalUsersService } from '../../../proxy/internal-users/internal-users.service';
import { IntakeAssignmentsService } from '../../../proxy/host-operators/intake-assignments.service';
import {
  clearPendingOfficeSwitch,
  readPendingOfficeSwitch,
  storePendingOfficeSwitch,
} from './pending-office-switch';

/**
 * F Half 2 office -> office two-hop orchestration. We exercise switchInto() and
 * maybeResumePendingSwitch() directly (cast past `protected`) without calling
 * detectChanges(), so ngOnInit's config/router/badge subscriptions never run --
 * keeping the test focused on the hop logic with stubbed services.
 */
describe('InternalShellLayoutComponent office-to-office switch', () => {
  let impersonation: {
    impersonateTenant: jasmine.Spy;
    impersonate: jasmine.Spy;
    isImpersonatorVisible: jasmine.Spy;
  };

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function createComponent(): any {
    impersonation = {
      impersonateTenant: jasmine.createSpy('impersonateTenant').and.returnValue(of({})),
      impersonate: jasmine.createSpy('impersonate').and.returnValue(of({})),
      isImpersonatorVisible: jasmine.createSpy('isImpersonatorVisible').and.returnValue(false),
    };

    TestBed.configureTestingModule({
      imports: [InternalShellLayoutComponent],
      providers: [
        {
          provide: ConfigStateService,
          useValue: { createOnUpdateStream: () => new Subject(), getOne: () => null },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false } },
        { provide: ToasterService, useValue: { info: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { url: '/', events: new Subject() } },
        { provide: Title, useValue: { setTitle: () => undefined } },
        { provide: ImpersonationService, useValue: impersonation },
        {
          provide: InternalNavBadgeService,
          useValue: {
            start: () => undefined,
            pendingAppointments: () => 0,
            pendingChangeRequests: () => 0,
          },
        },
        { provide: BrandingService, useValue: { displayName: () => null } },
        { provide: InternalUsersService, useValue: { getTenantOptions: () => of({ items: [] }) } },
        {
          provide: IntakeAssignmentsService,
          useValue: { getSwitchableOffices: () => of({ items: [] }) },
        },
      ],
    });

    // No detectChanges() -> ngOnInit is not invoked.
    return TestBed.createComponent(InternalShellLayoutComponent).componentInstance;
  }

  afterEach(() => {
    clearPendingOfficeSwitch();
    TestBed.resetTestingModule();
  });

  it('host-scope switchInto does a direct impersonateTenant and stores nothing', () => {
    const c = createComponent();
    c.hostScope.set(true);
    c.impersonating.set(false);
    c.user.set({ roles: ['Staff Supervisor'] });

    c.switchInto('office-b');

    expect(impersonation.impersonateTenant).toHaveBeenCalledWith('office-b', 'admin');
    expect(impersonation.impersonate).not.toHaveBeenCalled();
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('in-office switchInto stashes the target and de-impersonates to host', () => {
    const c = createComponent();
    c.hostScope.set(false);
    c.impersonating.set(true);
    c.user.set({ roles: ['admin'] });
    c.tenant.set({ id: 'office-a' });

    c.switchInto('office-b');

    expect(impersonation.impersonate).toHaveBeenCalledWith({});
    expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: 'admin' });
  });

  it('in-office intake switchInto stashes an empty username (shadow-user target)', () => {
    const c = createComponent();
    c.hostScope.set(false);
    c.impersonating.set(true);
    c.user.set({ roles: ['Intake Staff'] });

    c.switchInto('office-b');

    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: '' });
  });

  it('resumes a pending switch once back at host scope, clearing the record first', () => {
    const c = createComponent();
    storePendingOfficeSwitch({ officeId: 'office-b', userName: 'admin' });
    c.hostScope.set(true);
    c.impersonating.set(false);

    c.maybeResumePendingSwitch();

    expect(impersonation.impersonateTenant).toHaveBeenCalledWith('office-b', 'admin');
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('does not resume while still impersonating (in office A)', () => {
    const c = createComponent();
    storePendingOfficeSwitch({ officeId: 'office-b', userName: 'admin' });
    c.hostScope.set(false);
    c.impersonating.set(true);

    c.maybeResumePendingSwitch();

    expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: 'admin' });
  });

  it('does nothing at host scope when no switch is pending', () => {
    const c = createComponent();
    c.hostScope.set(true);
    c.impersonating.set(false);

    c.maybeResumePendingSwitch();

    expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
  });
});
