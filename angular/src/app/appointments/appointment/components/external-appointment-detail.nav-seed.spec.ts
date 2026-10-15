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
  RestService,
} from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { ExternalAppointmentDetailComponent } from './external-appointment-detail.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';

/**
 * #629 covers two edits in this file that had no coverage at all.
 *
 * openProfileNav routes each external role to its own self-edit profile. It was
 * a nested ternary (typescript:S3358), now an if/else chain -- a rewrite that
 * silently reordered would send an attorney to the patient profile, so the
 * three branches are pinned rather than eyeballed.
 *
 * seedEdits carried `String(appt?.['applicantAttorneyEmail'] ?? '')` against an
 * appointment cast to Record<string, unknown> (typescript:S6551). Both fields
 * are declared on AppointmentDto, so the cast threw away a real type; these
 * specs prove the emails actually reach the edit model now.
 */
describe('ExternalAppointmentDetailComponent nav and seed (#629)', () => {
  interface Probe {
    appointment: unknown;
    infoRequest: { flaggedFields: Array<{ key: string }> } | null;
    edits: Record<string, string>;
    openProfileNav(): void;
    seedEdits(): void;
  }

  let navigated: string[];

  function create(roles: string[]): Probe {
    navigated = [];
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        Injector,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        {
          provide: Router,
          useValue: {
            navigate: () => undefined,
            navigateByUrl: (u: string) => {
              navigated.push(u);
              return Promise.resolve(true);
            },
          },
        },
        { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) => (k === 'currentUser' ? { roles } : null),
            getAll: () => ({}),
          },
        },
        { provide: AppointmentService, useValue: {} },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ConfirmationService, useValue: { warn: () => of(null) } },
        { provide: AppointmentChangeRequestService, useValue: {} },
        { provide: AppointmentInfoRequestService, useValue: {} },
      ],
    });
    return TestBed.runInInjectionContext(
      () => new ExternalAppointmentDetailComponent(),
    ) as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('routes an applicant attorney to the attorney profile', () => {
    create(['Applicant Attorney']).openProfileNav();
    expect(navigated).toEqual(['/user-management/attorneys/my-profile']);
  });

  it('routes a defense attorney to the attorney profile', () => {
    create(['Defense Attorney']).openProfileNav();
    expect(navigated).toEqual(['/user-management/attorneys/my-profile']);
  });

  it('routes a claim examiner to the claim-examiner profile', () => {
    create(['Claim Examiner']).openProfileNav();
    expect(navigated).toEqual(['/user-management/claim-examiners/my-profile']);
  });

  it('falls back to the patient profile for any other role', () => {
    create(['Patient']).openProfileNav();
    expect(navigated).toEqual(['/user-management/patients/my-profile']);
  });

  /** Attorney wins when a user somehow holds both roles -- the order in the
   *  if/else chain is the behaviour, so it is asserted rather than assumed. */
  it('prefers the attorney profile when a user holds both roles', () => {
    create(['Claim Examiner', 'Defense Attorney']).openProfileNav();
    expect(navigated).toEqual(['/user-management/attorneys/my-profile']);
  });

  it('seeds the attorney and claim-examiner emails from the appointment', () => {
    const c = create(['Patient']);
    c.appointment = {
      patient: { street: '1 Oak St', city: 'Fresno' },
      appointment: {
        applicantAttorneyEmail: 'aa@example.test',
        claimExaminerEmail: 'ce@example.test',
      },
    };
    c.infoRequest = {
      flaggedFields: [
        { key: 'applicantAttorneyEmail' },
        { key: 'appointmentClaimExaminerEmail' },
        { key: 'street' },
      ],
    };

    c.seedEdits();

    expect(c.edits['applicantAttorneyEmail']).toBe('aa@example.test');
    expect(c.edits['appointmentClaimExaminerEmail']).toBe('ce@example.test');
    expect(c.edits['street']).toBe('1 Oak St');
  });

  it('seeds an empty string when the appointment has no emails', () => {
    const c = create(['Patient']);
    c.appointment = { patient: {}, appointment: {} };
    c.infoRequest = {
      flaggedFields: [{ key: 'applicantAttorneyEmail' }, { key: 'appointmentClaimExaminerEmail' }],
    };

    c.seedEdits();

    expect(c.edits['applicantAttorneyEmail']).toBe('');
    expect(c.edits['appointmentClaimExaminerEmail']).toBe('');
  });
});
