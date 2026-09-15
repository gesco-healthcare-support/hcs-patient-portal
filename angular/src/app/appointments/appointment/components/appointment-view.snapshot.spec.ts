import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import {
  ConfigStateService,
  EnvironmentService,
  LocalizationService,
  RestService,
} from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentViewComponent } from './appointment-view.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import type { AppointmentWithNavigationPropertiesDto } from '../../../proxy/appointments/models';

/**
 * #629 -- overlayApplicantAttorneySnapshot and overlayDefenseAttorneySnapshot
 * each carried a byte-identical local `set` closure (typescript:S4144), now
 * hoisted to one module-level setIfPresent. Neither method had any coverage,
 * so the rule fix would have gone in unverified.
 *
 * The contract worth pinning is not "it copies fields" but WHICH fields it
 * skips: the guard is `!== null && !== undefined`, so a present-but-falsy
 * value ('' or 0) must still be written. A `if (val)` would look equivalent
 * and would silently drop a cleared attorney field, leaving the previous
 * value on the form.
 */
describe('AppointmentViewComponent attorney snapshot overlay (#629)', () => {
  interface Probe {
    appointment: AppointmentWithNavigationPropertiesDto | null;
    form: { getRawValue(): Record<string, unknown> };
    overlayApplicantAttorneySnapshot(): void;
    overlayDefenseAttorneySnapshot(): void;
  }

  function create(): Probe {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
        { provide: ConfigStateService, useValue: { getOne: () => null, getAll: () => ({}) } },
        { provide: AppointmentService, useValue: {} },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ConfirmationService, useValue: { warn: () => of(null) } },
        { provide: AppointmentChangeRequestService, useValue: {} },
      ],
    });
    return TestBed.runInInjectionContext(() => new AppointmentViewComponent()) as unknown as Probe;
  }

  function withAppointment(c: Probe, fields: Record<string, unknown>): void {
    c.appointment = { appointment: fields } as unknown as AppointmentWithNavigationPropertiesDto;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('copies the present applicant attorney fields onto the form', () => {
    const c = create();
    withAppointment(c, {
      applicantAttorneyFirstName: 'Ada',
      applicantAttorneyLastName: 'Nakamura',
      applicantAttorneyFirmName: 'Nakamura LLP',
      applicantAttorneyCity: 'Fresno',
    });

    c.overlayApplicantAttorneySnapshot();

    const v = c.form.getRawValue();
    expect(v['applicantAttorneyFirstName']).toBe('Ada');
    expect(v['applicantAttorneyLastName']).toBe('Nakamura');
    expect(v['applicantAttorneyFirmName']).toBe('Nakamura LLP');
    expect(v['applicantAttorneyCity']).toBe('Fresno');
  });

  it('leaves a control alone when the snapshot field is null or absent', () => {
    const c = create();
    withAppointment(c, {
      applicantAttorneyFirstName: 'Ada',
      applicantAttorneyLastName: null,
      // applicantAttorneyFirmName omitted entirely
    });

    c.overlayApplicantAttorneySnapshot();

    const v = c.form.getRawValue();
    expect(v['applicantAttorneyFirstName']).toBe('Ada');
    expect(v['applicantAttorneyLastName']).toBeNull();
    expect(v['applicantAttorneyFirmName']).toBeNull();
  });

  /**
   * The one a `if (val)` rewrite would break. An attorney whose fax was
   * cleared upstream must clear on the form too, not silently keep the old
   * value.
   */
  it('writes a present but empty value rather than skipping it', () => {
    const c = create();
    withAppointment(c, { applicantAttorneyFaxNumber: '' });

    c.overlayApplicantAttorneySnapshot();

    expect(c.form.getRawValue()['applicantAttorneyFaxNumber']).toBe('');
  });

  it('overlays the defense fields without touching the applicant ones', () => {
    const c = create();
    withAppointment(c, {
      applicantAttorneyFirstName: 'Ada',
      defenseAttorneyFirstName: 'Boris',
      defenseAttorneyZipCode: '93720',
    });

    c.overlayDefenseAttorneySnapshot();

    const v = c.form.getRawValue();
    expect(v['defenseAttorneyFirstName']).toBe('Boris');
    expect(v['defenseAttorneyZipCode']).toBe('93720');
    expect(v['applicantAttorneyFirstName']).toBeNull();
  });

  it('is a no-op when no appointment is loaded', () => {
    const c = create();
    c.appointment = null;

    expect(() => c.overlayApplicantAttorneySnapshot()).not.toThrow();
    expect(() => c.overlayDefenseAttorneySnapshot()).not.toThrow();
    expect(c.form.getRawValue()['applicantAttorneyFirstName']).toBeNull();
  });
});
