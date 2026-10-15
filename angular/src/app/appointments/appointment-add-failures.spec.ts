import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentAddComponent } from './appointment-add.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../proxy/custom-fields-controllers/custom-fields.service';
import { AddressValidationProvider } from '../shared/address/address-validation.provider';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import {
  ADDRESS_CHECK_SKIPPED_MESSAGE,
  NO_SOURCE_FOUND_MESSAGE,
  SOURCE_LOADED_MESSAGE,
} from './shared/booking-failure-message.util';

/**
 * Drives the booking wizard's failure paths end to end (#604).
 *
 * `booking-failure-message.util.spec.ts` pins the wording. What is pinned HERE is the wiring
 * that spec cannot see: that a failed sub-resource GET actually reaches the message, that the
 * booking still completes around it, and that the record is cleared between loads. Before
 * #604 every one of these produced "Prior appointment loaded" over a re-evaluation that was
 * silently missing a section.
 *
 * The class is an unselectored `@Directive()` base, so it is constructed directly in an
 * injection context rather than through `createComponent`. Nothing is change-detected: only
 * the constructor and the methods each test calls run.
 */
describe('AppointmentAddComponent failure reporting (#604)', () => {
  /** A URL fragment that should reject, and the status it rejects with. */
  interface Failing {
    urlPart: string;
    status?: number;
  }

  interface Probe {
    sourceLoadMessage: string;
    patientLoadMessage: string;
    form: { get(path: string): { setValue(v: unknown): void } | null };
    loadSourceForPrefill(conf: string, flow: 'reval' | 'reRequest' | 'reBook'): Promise<void>;
    loadPatientByEmail(): Promise<void>;
    standardizeAddressesBeforeSubmit(): Promise<void>;
  }

  function build(options: {
    failing?: Failing[];
    sourceStatus?: 'throw403' | 'throw404' | 'empty';
    validateThrows?: boolean;
  }) {
    // Mutable so a test can heal the failure and load a second time on the same instance.
    const failing: Failing[] = [...(options.failing ?? [])];
    const warn = jasmine.createSpy('warn');

    const rest = {
      request: (req: { method: string; url: string }): Observable<unknown> => {
        const hit = failing.find((f) => req.url.includes(f.urlPart));
        if (hit) {
          return throwError(() => ({ status: hit.status }));
        }
        if (req.url.includes('appointment-injury-details')) {
          return of([]);
        }
        if (req.url.includes('applicant-attorney') || req.url.includes('defense-attorney')) {
          // Absence is 200 + null on these two, never an error.
          return of(null);
        }
        if (req.url.includes('patients/for-appointment-booking/by-email')) {
          return of(null);
        }
        return of({ items: [], totalCount: 0 });
      },
    };

    const appointmentService = {
      getByConfirmationNumber: (): Observable<unknown> => {
        if (options.sourceStatus === 'throw403') {
          return throwError(() => ({ status: 403 }));
        }
        if (options.sourceStatus === 'throw404') {
          return throwError(() => ({ status: 404 }));
        }
        if (options.sourceStatus === 'empty') {
          // The server answers 200 with no appointment rather than 404 for an unknown
          // number, so this branch is in-band, not an error.
          return of({ appointment: null });
        }
        return of({
          appointment: {
            id: 'a1',
            // Reval's client-side gate requires Approved; anything else short-circuits
            // before the prefill runs and this suite would be testing the gate instead.
            appointmentStatus: AppointmentStatusType.Approved,
            locationId: null,
            appointmentTypeId: null,
          },
          patient: null,
          claimExaminer: null,
          primaryInsurance: null,
        });
      },
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: rest },
        { provide: AppointmentService, useValue: appointmentService },
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
          useValue: { warn, error: () => undefined, success: () => undefined },
        },
        { provide: ConfirmationService, useValue: { warn: () => of('confirm') } },
        {
          provide: AddressValidationProvider,
          useValue: {
            validate: () =>
              options.validateThrows
                ? throwError(() => ({ status: 503 }))
                : of({ status: 'ok', standardized: null, matchesInput: true }),
          },
        },
      ],
    });

    const component = TestBed.runInInjectionContext(
      () => new AppointmentAddComponent(),
    ) as unknown as Probe;
    return { component, warn, failing };
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('re-evaluation prefill', () => {
    it('reports a clean load when every sub-resource came back', async () => {
      const { component } = build({});
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toBe(SOURCE_LOADED_MESSAGE);
    });

    it('names injuries when the booker lacks permission to read them', async () => {
      // The only one of the five behind a named permission. This used to prefill a
      // re-evaluation with zero injuries and still say "Prior appointment loaded".
      const { component } = build({
        failing: [{ urlPart: 'appointment-injury-details', status: 403 }],
      });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('injuries');
      expect(component.sourceLoadMessage).toContain('do not have permission');
    });

    it('names injuries and offers a retry when the server failed', async () => {
      const { component } = build({
        failing: [{ urlPart: 'appointment-injury-details', status: 500 }],
      });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('injuries');
      expect(component.sourceLoadMessage).toContain('retry');
    });

    it('names every section that failed, not just the first', async () => {
      const { component } = build({
        failing: [
          { urlPart: 'appointment-employer-details', status: 500 },
          { urlPart: 'appointment-injury-details', status: 500 },
        ],
      });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('employer details');
      expect(component.sourceLoadMessage).toContain('injuries');
    });

    it('names the attorney section when its GET rejects', async () => {
      const { component } = build({ failing: [{ urlPart: 'applicant-attorney', status: 500 }] });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('applicant attorney');
    });

    it('names authorized users when the accessors page rejects', async () => {
      const { component } = build({ failing: [{ urlPart: 'appointment-accessors', status: 500 }] });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('authorized users');
    });

    it('still completes the prefill rather than abandoning the booking', async () => {
      // The failure is reported, not raised: a booker who can see everything except
      // injuries must still be able to book, entering the injuries by hand.
      const { component } = build({
        failing: [{ urlPart: 'appointment-injury-details', status: 403 }],
      });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).not.toContain('Unable to load');
      expect(component.sourceLoadMessage).toContain('Prior appointment loaded');
    });

    it('clears the record between loads, so a clean retry reports clean', async () => {
      // Without the reset in applySourceToForm the warning would be sticky: every later
      // load on the same instance would keep naming a section that has since loaded fine.
      const { component, failing } = build({
        failing: [{ urlPart: 'appointment-injury-details', status: 500 }],
      });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('injuries');

      failing.length = 0;
      await component.loadSourceForPrefill('C2', 'reval');
      expect(component.sourceLoadMessage).toBe(SOURCE_LOADED_MESSAGE);
    });
  });

  describe('source lookup itself', () => {
    it('sends a refused booker to a supervisor instead of back to the number', async () => {
      const { component } = build({ sourceStatus: 'throw403' });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toContain('do not have permission');
      expect(component.sourceLoadMessage).not.toContain('confirmation number');
    });

    it('reuses the not-found wording for a 404', async () => {
      const { component } = build({ sourceStatus: 'throw404' });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toBe(NO_SOURCE_FOUND_MESSAGE);
    });

    it('says the same thing when the server answers 200 with no appointment', async () => {
      // The in-band branch and the 404 catch must not drift apart: an unknown confirmation
      // number reads identically whichever way the server chose to express it.
      const { component } = build({ sourceStatus: 'empty' });
      await component.loadSourceForPrefill('C1', 'reval');
      expect(component.sourceLoadMessage).toBe(NO_SOURCE_FOUND_MESSAGE);
    });
  });

  describe('patient lookup by email', () => {
    it('points a refused booker at the manual form', async () => {
      const { component } = build({
        failing: [{ urlPart: 'for-appointment-booking/by-email', status: 403 }],
      });
      component.form.get('email')!.setValue('ada@example.test');
      await component.loadPatientByEmail();
      expect(component.patientLoadMessage).toContain('do not have permission');
    });

    it('offers a retry for a transient failure', async () => {
      const { component } = build({
        failing: [{ urlPart: 'for-appointment-booking/by-email', status: 500 }],
      });
      component.form.get('email')!.setValue('ada@example.test');
      await component.loadPatientByEmail();
      expect(component.patientLoadMessage).toContain('try again');
    });
  });

  describe('address standardization', () => {
    it('warns once when the state lookup fails, and never blocks the submit', async () => {
      const { component, warn } = build({ failing: [{ urlPart: 'patients/state-lookup' }] });
      await component.standardizeAddressesBeforeSubmit();
      expect(warn).toHaveBeenCalledWith(ADDRESS_CHECK_SKIPPED_MESSAGE);
    });

    it('warns exactly once when the provider fails for several addresses, not once each', async () => {
      // Two enabled groups both carry a street, so the provider is called twice and both
      // reject. One toast per batch is the point: a booker with five addresses filled in
      // should not have to dismiss five identical warnings.
      const { component, warn } = build({ validateThrows: true });
      component.form.get('street')!.setValue('1 Main St');
      component.form.get('employerStreet')!.setValue('2 Market St');

      await component.standardizeAddressesBeforeSubmit();

      expect(warn).toHaveBeenCalledWith(ADDRESS_CHECK_SKIPPED_MESSAGE);
      expect(warn.calls.count()).toBe(1);
    });
  });
});
