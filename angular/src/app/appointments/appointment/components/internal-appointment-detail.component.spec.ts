import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import {
  ConfigStateService,
  EnvironmentService,
  LocalizationService,
  PermissionService,
  RestService,
} from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { InternalAppointmentDetailComponent } from './internal-appointment-detail.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';

/**
 * Covers the Escape-to-close handler added in #622. The authorized-user modal
 * could previously be dismissed only with the mouse.
 *
 * The component is created but never change-detected, so `ngOnInit` -- which
 * loads the appointment over HTTP -- does not run. Most collaborators are empty
 * stubs for that reason; only the ones touched during construction need bodies.
 */
describe('InternalAppointmentDetailComponent Escape handling (#622)', () => {
  interface Probe {
    isAuthorizedUserModalOpen: boolean;
    onEscapeKey(): void;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
        { provide: AppointmentService, useValue: {} },
        { provide: AppointmentChangeRequestService, useValue: {} },
        { provide: AppointmentInfoRequestService, useValue: {} },
        { provide: ConfigStateService, useValue: { getOne: () => null, getAll: () => ({}) } },
        { provide: ConfirmationService, useValue: { warn: () => of(null) } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(InternalAppointmentDetailComponent);
    return { fixture, probe: fixture.componentInstance as unknown as Probe };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('closes the authorized-user modal on Escape', () => {
    const c = create();
    c.probe.isAuthorizedUserModalOpen = true;
    c.probe.onEscapeKey();
    expect(c.probe.isAuthorizedUserModalOpen).toBeFalse();
  });

  it('is inert when the modal is closed', () => {
    const c = create();
    c.probe.isAuthorizedUserModalOpen = false;
    expect(() => c.probe.onEscapeKey()).not.toThrow();
    expect(c.probe.isAuthorizedUserModalOpen).toBeFalse();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    // Proves the @HostListener binding, which a direct method call cannot.
    const c = create();
    c.probe.isAuthorizedUserModalOpen = true;
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.probe.isAuthorizedUserModalOpen).toBeFalse();
  });
});
