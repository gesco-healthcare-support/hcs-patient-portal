import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { Injector } from '@angular/core';
import { of } from 'rxjs';
import {
  ConfigStateService,
  EnvironmentService,
  LocalizationService,
  PermissionService,
  RestService,
} from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { ExternalAppointmentDetailComponent } from './external-appointment-detail.component';
import { InternalAppointmentsComponent } from './internal-appointments.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';

/**
 * #629 -- Sonar's MouseEventWithoutKeyboardEquivalentCheck flagged two overlays
 * that genuinely could only be dismissed with a mouse: the resubmit scrim on
 * the external detail page, and the row-actions click-away on the internal
 * list. Both now close on Escape.
 *
 * The guard is the part worth pinning, not the close. A handler bound to
 * `document` fires on every Escape anywhere in the app, so it must be inert
 * when its overlay is shut -- otherwise Escape in an unrelated dialog reaches
 * in and mutates this component's state.
 */
const CORE_STUBS = [
  FormBuilder,
  Injector,
  { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
  { provide: Router, useValue: { navigate: () => undefined, navigateByUrl: () => undefined } },
  { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
  { provide: ConfigStateService, useValue: { getOne: () => null, getAll: () => ({}) } },
  { provide: AppointmentService, useValue: {} },
  { provide: RestService, useValue: { request: () => of(null) } },
  { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
  { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
  { provide: LocalizationService, useValue: { instant: (k: string) => k } },
  { provide: ConfirmationService, useValue: { warn: () => of(null) } },
  { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
  { provide: AppointmentChangeRequestService, useValue: {} },
  { provide: AppointmentInfoRequestService, useValue: {} },
];

describe('Escape closes the mouse-only overlays (#629)', () => {
  afterEach(() => TestBed.resetTestingModule());

  describe('external detail resubmit modal', () => {
    interface Probe {
      resubmitConfirmVisible: boolean;
      onResubmitEscapeKey(): void;
    }

    function create(): Probe {
      TestBed.configureTestingModule({ providers: CORE_STUBS });
      return TestBed.runInInjectionContext(
        () => new ExternalAppointmentDetailComponent(),
      ) as unknown as Probe;
    }

    it('closes the resubmit confirmation when it is open', () => {
      const c = create();
      c.resubmitConfirmVisible = true;

      c.onResubmitEscapeKey();

      expect(c.resubmitConfirmVisible).toBeFalse();
    });

    it('is inert when the confirmation is already closed', () => {
      const c = create();
      c.resubmitConfirmVisible = false;

      expect(() => c.onResubmitEscapeKey()).not.toThrow();
      expect(c.resubmitConfirmVisible).toBeFalse();
    });
  });

  describe('internal list row-actions menu', () => {
    interface Probe {
      menuId: { set(v: string | null): void; (): string | null };
      onEscapeKey(): void;
    }

    function create(): Probe {
      TestBed.configureTestingModule({ providers: CORE_STUBS });
      return TestBed.runInInjectionContext(
        () => new InternalAppointmentsComponent(),
      ) as unknown as Probe;
    }

    it('closes an open row-actions menu', () => {
      const c = create();
      c.menuId.set('appt-1');

      c.onEscapeKey();

      expect(c.menuId()).toBeNull();
    });

    it('is inert when no menu is open', () => {
      const c = create();
      c.menuId.set(null);

      expect(() => c.onEscapeKey()).not.toThrow();
      expect(c.menuId()).toBeNull();
    });
  });
});
