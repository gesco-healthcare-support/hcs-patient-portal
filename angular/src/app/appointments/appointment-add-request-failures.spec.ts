import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { config, of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentAddComponent } from './appointment-add.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../proxy/custom-fields-controllers/custom-fields.service';
import { AddressValidationProvider } from '../shared/address/address-validation.provider';

/**
 * The booking form's loaders that used to subscribe with no error branch (#1113).
 *
 * ABP's RestService reports a failed request and then rethrows it, so a subscriber with no error
 * branch sent that copy to RxJS's unhandled-error path. Every request here fails. What is pinned
 * is the behaviour the booker is left with: the loading flag is cleared, nothing already on the
 * form is overwritten, and the global unhandled-error hook never fires. The hook is replaced
 * only inside this block and restored afterwards.
 *
 * <p>The class is an unselectored `@Directive()` base, so it is constructed directly in an
 * injection context and never change-detected. All values are synthetic.</p>
 */
describe('AppointmentAddComponent failed loaders (#1113)', () => {
  interface Probe {
    [key: string]: any;
  }

  let unhandled: jasmine.Spy;
  let previous: typeof config.onUnhandledError;
  let request: jasmine.Spy;

  function create(): Probe {
    request = jasmine.createSpy('request').and.callFake(() => throwError(() => ({ status: 500 })));
    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: { request } },
        { provide: AppointmentService, useValue: {} },
        { provide: AppointmentApprovalService, useValue: { approveAppointment: () => of(null) } },
        { provide: CustomFieldsService, useValue: { getByAppointmentTypeId: () => of([]) } },
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({})) } },
        {
          provide: Router,
          useValue: { navigateByUrl: () => undefined, navigate: () => undefined },
        },
        { provide: ConfigStateService, useValue: { getOne: () => null, getDeep: () => null } },
        {
          provide: ToasterService,
          useValue: { warn: () => undefined, error: () => undefined, success: () => undefined },
        },
        { provide: ConfirmationService, useValue: { warn: () => of('confirm') } },
        {
          provide: AddressValidationProvider,
          useValue: {
            validate: () => of({ status: 'ok', standardized: null, matchesInput: true }),
          },
        },
      ],
    });
    return TestBed.runInInjectionContext(() => new AppointmentAddComponent()) as unknown as Probe;
  }

  beforeEach(() => {
    previous = config.onUnhandledError;
    unhandled = jasmine.createSpy('onUnhandledError');
    config.onUnhandledError = unhandled;
  });

  afterEach(() => {
    config.onUnhandledError = previous;
    TestBed.resetTestingModule();
  });

  it('can see an unhandled error at all (detector self-check)', fakeAsync(() => {
    throwError(() => new Error('probe')).subscribe({ next: () => undefined });
    tick();
    expect(unhandled).toHaveBeenCalled();
  }));

  it('leaves the form unconfigured when the field-config load for a type fails', fakeAsync(() => {
    const c = create();
    c.applyFieldConfigsForAppointmentType('type-1');
    tick();
    expect(request).toHaveBeenCalled();
    expect(unhandled).not.toHaveBeenCalled();
    expect(c.hiddenFieldNames.size).toBe(0);
    expect(c.readOnlyFieldNames.size).toBe(0);
  }));

  it('stops loading when the external-user profile load fails', fakeAsync(() => {
    const c = create();
    c.isProfileLoading = true;
    c.loadExternalUserProfile();
    tick();
    expect(request).toHaveBeenCalled();
    expect(unhandled).not.toHaveBeenCalled();
    expect(c.isProfileLoading).toBeFalse();
  }));

  it('stops loading when the patient profile load fails', fakeAsync(() => {
    const c = create();
    c.isProfileLoading = true;
    c.loadPatientProfile();
    tick();
    expect(request).toHaveBeenCalled();
    expect(unhandled).not.toHaveBeenCalled();
    expect(c.isProfileLoading).toBeFalse();
  }));

  it('leaves the authorized-user options empty when their lookup fails', fakeAsync(() => {
    const c = create();
    c.loadExternalAuthorizedUsers();
    tick();
    expect(request).toHaveBeenCalled();
    expect(unhandled).not.toHaveBeenCalled();
    expect(c.externalAuthorizedUserOptions).toEqual([]);
  }));

  describe('the attorney lookups', () => {
    it('clears the busy flag and fills nothing when the applicant attorney email search fails', fakeAsync(() => {
      const c = create();
      c.applicantAttorneyEmailSearch = 'aa@example.test';
      c.loadApplicantAttorneyByEmail();
      tick();
      expect(request).toHaveBeenCalled();
      expect(unhandled).not.toHaveBeenCalled();
      expect(c.isApplicantAttorneyLoading).toBeFalse();
      expect(c.applicantAttorneyId ?? null).toBeNull();
    }));

    it('clears the busy flag and fills nothing when picking an applicant attorney fails', fakeAsync(() => {
      const c = create();
      c.onApplicantAttorneySelected('user-1');
      tick();
      expect(request).toHaveBeenCalled();
      expect(unhandled).not.toHaveBeenCalled();
      expect(c.isApplicantAttorneyLoading).toBeFalse();
      expect(c.applicantAttorneyId ?? null).toBeNull();
    }));

    it('clears the busy flag and fills nothing when the defense attorney email search fails', fakeAsync(() => {
      const c = create();
      c.defenseAttorneyEmailSearch = 'da@example.test';
      c.loadDefenseAttorneyByEmail();
      tick();
      expect(request).toHaveBeenCalled();
      expect(unhandled).not.toHaveBeenCalled();
      expect(c.isDefenseAttorneyLoading).toBeFalse();
      expect(c.defenseAttorneyId ?? null).toBeNull();
    }));

    it('clears the busy flag and fills nothing when picking a defense attorney fails', fakeAsync(() => {
      const c = create();
      c.onDefenseAttorneySelected('user-2');
      tick();
      expect(request).toHaveBeenCalled();
      expect(unhandled).not.toHaveBeenCalled();
      expect(c.isDefenseAttorneyLoading).toBeFalse();
      expect(c.defenseAttorneyId ?? null).toBeNull();
    }));
  });
});
