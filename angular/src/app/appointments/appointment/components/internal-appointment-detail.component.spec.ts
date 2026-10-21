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
function configure(
  rest: { request: (...args: unknown[]) => unknown } = { request: () => of(null) },
) {
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
      { provide: RestService, useValue: rest },
      { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
    ],
  });
  return TestBed.createComponent(InternalAppointmentDetailComponent);
}

describe('InternalAppointmentDetailComponent Escape handling (#622)', () => {
  interface Probe {
    isAuthorizedUserModalOpen: boolean;
    onEscapeKey(): void;
  }

  function create() {
    const fixture = configure();
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

/**
 * Staff "Edit details" party changes (2026-10-21). The save must go through the staff parties
 * endpoint (which writes the access-key columns), keep the appointment's new concurrency stamp,
 * and never write anything when an attorney is missing its email.
 */
describe('InternalAppointmentDetailComponent staff party changes', () => {
  interface Probe {
    form: {
      getRawValue(): Record<string, unknown>;
      patchValue(v: Record<string, unknown>): void;
      get(n: string): { value: unknown } | null;
    };
    appointment: any;
    errorMessage: string;
    applicantAttorneyId: string | null;
    defenseAttorneyId: string | null;
    parties: {
      begin(f: Record<string, unknown>, ce: unknown): void;
      remove(p: string): void;
      removed: Record<string, boolean>;
      claimExaminerForm: { getRawValue(): any; patchValue(v: any): void };
    };
    enterEdit(): void;
    cancelEdit(): void;
    saveEdit(): Promise<void>;
    save(): Promise<void>;
    saveRelatedDetails(id?: string): Promise<void>;
    upsertEmployerDetails(id?: string): Promise<void>;
    loadApplicantAttorneyDetails(id?: string): void;
    bindDefenseAttorneyForAppointment(id?: string): void;
  }

  function create(rest = jasmine.createSpyObj('RestService', ['request'])) {
    rest.request.and.returnValue(of({ id: 'appt-1', concurrencyStamp: 'stamp-2' }));
    const fixture = configure(rest);
    const probe = fixture.componentInstance as unknown as Probe;
    probe.appointment = {
      appointment: { id: 'appt-1', concurrencyStamp: 'stamp-1' },
      claimExaminer: { name: 'TEST-Cleo', email: 'ce@example.test' },
    };
    probe.form.patchValue({
      defenseAttorneyEmail: 'da@example.test',
      defenseAttorneyFirstName: 'TEST-Dana',
      defenseAttorneyFirmName: 'TEST-Firm',
    });
    spyOn(probe, 'upsertEmployerDetails').and.resolveTo();
    spyOn(probe, 'loadApplicantAttorneyDetails');
    spyOn(probe, 'bindDefenseAttorneyForAppointment');
    return { probe, rest };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('starts the claim examiner editor from the appointment, and cancel restores it', () => {
    const { probe } = create();

    probe.enterEdit();
    expect(probe.parties.claimExaminerForm.getRawValue().name).toBe('TEST-Cleo');

    probe.parties.claimExaminerForm.patchValue({ name: 'TEST-Changed' });
    probe.cancelEdit();
    expect(probe.parties.claimExaminerForm.getRawValue().name).toBe('TEST-Cleo');
  });

  it('refuses to save an attorney without an email, before anything is written', async () => {
    const { probe } = create();
    probe.enterEdit();
    const save = spyOn(probe, 'save').and.resolveTo();
    probe.form.patchValue({ defenseAttorneyEmail: '', defenseAttorneyFirstName: 'TEST-New' });

    await probe.saveEdit();

    expect(save).not.toHaveBeenCalled();
    expect(probe.errorMessage).toContain('email address');
  });

  it('saves the parties through the staff endpoint and keeps the new concurrency stamp', async () => {
    const { probe, rest } = create();
    probe.enterEdit();
    probe.parties.remove('defenseAttorney');
    probe.defenseAttorneyId = 'master-1';

    await probe.saveRelatedDetails('appt-1');

    const [request] = rest.request.calls.mostRecent().args as unknown as [
      { url: string; body: any },
    ];
    expect(request.url).toBe('/api/app/appointments/appt-1/parties');
    expect(request.body.defenseAttorney).toEqual({ remove: true });
    expect(probe.appointment.appointment.concurrencyStamp).toBe('stamp-2');
    expect(probe.form.get('defenseAttorneyEmail')?.value).toBeNull();
    expect(probe.defenseAttorneyId).toBeNull();
    expect(probe.bindDefenseAttorneyForAppointment).toHaveBeenCalledWith('appt-1');
    expect(probe.upsertEmployerDetails).toHaveBeenCalledWith('appt-1');
  });

  it('makes no parties call when no party changed', async () => {
    const { probe, rest } = create();
    probe.enterEdit();

    await probe.saveRelatedDetails('appt-1');

    expect(rest.request).not.toHaveBeenCalled();
    expect(probe.appointment.appointment.concurrencyStamp).toBe('stamp-1');
    expect(probe.upsertEmployerDetails).toHaveBeenCalledWith('appt-1');
  });
});
