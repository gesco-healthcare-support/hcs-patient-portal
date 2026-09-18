import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentAddComponent } from './appointment-add.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../proxy/custom-fields-controllers/custom-fields.service';
import { AddressValidationProvider } from '../shared/address/address-validation.provider';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import { BookingSubmitMode } from '../proxy/enums/booking-submit-mode.enum';
import { CustomFieldType } from '../proxy/enums/custom-field-type.enum';
import { OTHER_DOCUMENT_TYPE_VALUE } from './sections/appointment-add-documents.component';

/**
 * The booking ENGINE -- the class the wizard extends.
 *
 * <p>`appointment-add-failures.spec.ts` drives the #604 failure-reporting paths and
 * nothing else. What it leaves untouched is most of what the engine actually does on a
 * normal booking: resolving the mode from the route, deciding what a booker's role lets
 * them see, the Panel Number state machine, the per-type field-config and custom-field
 * cascades, document staging, the booking-horizon calendar rules, the slot/time wiring,
 * the party lookups, and every gate `onSubmit` applies before it will POST.</p>
 *
 * <p>The class is an unselectored `@Directive()` base, so it is constructed directly in an
 * injection context rather than through `createComponent` -- the arrangement its sibling
 * spec established. Nothing is change-detected: only the constructor and the methods each
 * test calls run.</p>
 *
 * <p>Every patient, attorney, employer and address below is synthetic, and the
 * never-prefill-the-SSN cases use an obvious placeholder token rather than a real-looking
 * number. Dates are computed as offsets from today rather than written as literals, so the
 * booking-horizon tests cannot rot into passing for the wrong reason.</p>
 */
describe('AppointmentAddComponent booking engine', () => {
  let router: { navigateByUrl: jasmine.Spy; navigate: jasmine.Spy };
  let toaster: { warn: jasmine.Spy; error: jasmine.Spy; success: jasmine.Spy };
  let confirmationWarn: jasmine.Spy;
  let confirmationInfo: jasmine.Spy;
  let submit: jasmine.Spy;
  let approveAppointment: jasmine.Spy;
  let getActiveForAppointmentType: jasmine.Spy;
  let restRequest: jasmine.Spy;
  let restResponses: Record<string, Observable<unknown>>;

  /** Mirrors CaseEvaluationSeedIds.AppointmentTypes.PanelQme, as the component does. */
  const PQME_TYPE_ID = 'a0a00002-0000-4000-9000-000000000002';

  /** Obviously-not-real stand-in for a stored SSN; the point is only that it is present. */
  const SSN_PLACEHOLDER = 'SYNTHETIC-PLACEHOLDER';

  interface Probe {
    [key: string]: any;
  }

  function defaultRest(url: string): Observable<unknown> {
    if (url.includes('appointment-type-field-configs')) return of([]);
    if (url.includes('options-by-type')) return of([]);
    if (url.includes('by-appointment')) return of([]);
    if (url.includes('external-users/me')) return of(null);
    if (url.includes('patients/me')) return of(null);
    if (url.includes('external-user-lookup')) return of({ items: [] });
    if (url.includes('state-lookup')) {
      return of({ items: [{ id: 'state-1', displayName: 'California' }] });
    }
    if (url.includes('appointment-language-lookup')) return of({ items: [] });
    return of({ items: [], totalCount: 0 });
  }

  function create(
    options: {
      roles?: string[];
      queryParams?: Record<string, string>;
      confirmStatus?: unknown;
      rest?: Record<string, Observable<unknown>>;
    } = {},
  ): Probe {
    const params = options.queryParams ?? {};
    restResponses = options.rest ?? {};
    router = {
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
      navigate: jasmine.createSpy('navigate'),
    };
    toaster = {
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
      success: jasmine.createSpy('success'),
    };
    confirmationWarn = jasmine
      .createSpy('warn')
      .and.returnValue(of(options.confirmStatus ?? Confirmation.Status.confirm));
    confirmationInfo = jasmine.createSpy('info').and.returnValue(of(Confirmation.Status.confirm));
    submit = jasmine.createSpy('submit').and.returnValue(of({ appointmentId: 'appt-1' }));
    approveAppointment = jasmine.createSpy('approveAppointment').and.returnValue(of(null));
    getActiveForAppointmentType = jasmine
      .createSpy('getActiveForAppointmentType')
      .and.returnValue(of([]));
    restRequest = jasmine.createSpy('request').and.callFake((req: { url: string }) => {
      for (const [fragment, response] of Object.entries(restResponses)) {
        if (req.url.includes(fragment)) return response;
      }
      return defaultRest(req.url);
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: { request: restRequest } },
        {
          provide: AppointmentService,
          useValue: { submit, getByConfirmationNumber: () => of({ appointment: null }) },
        },
        { provide: AppointmentApprovalService, useValue: { approveAppointment } },
        {
          provide: CustomFieldsService,
          useValue: { getActiveForAppointmentType, getList: () => of({ items: [] }) },
        },
        {
          provide: ActivatedRoute,
          useValue: { queryParamMap: of({ get: (key: string) => params[key] ?? null }) },
        },
        { provide: Router, useValue: router },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) => {
              if (key === 'currentUser') {
                return {
                  id: 'user-1',
                  name: 'Ada',
                  surname: 'Lovelace',
                  email: 'ada@example.test',
                  userName: 'ada',
                  roles: options.roles ?? [],
                };
              }
              if (key === 'currentTenant') return { name: 'Falkinstein' };
              return null;
            },
            getDeep: () => null,
          },
        },
        { provide: ToasterService, useValue: toaster },
        {
          provide: ConfirmationService,
          useValue: { warn: confirmationWarn, info: confirmationInfo },
        },
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

  /** A calendar struct N days from today, in local time -- never a hardcoded date. */
  function dateStructIn(days: number): { year: number; month: number; day: number } {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + days);
    return { year: d.getFullYear(), month: d.getMonth() + 1, day: d.getDate() };
  }

  function dateKeyIn(days: number): string {
    const { year, month, day } = dateStructIn(days);
    return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  }

  /** Fill every control carrying Validators.required so the form reaches submit. */
  function fillValidForm(c: Probe): void {
    c.form.patchValue(
      {
        patientId: 'patient-1',
        appointmentTypeId: 'type-1',
        locationId: 'loc-1',
        appointmentDate: dateKeyIn(10),
        appointmentTime: '09:00',
        doctorAvailabilityId: 'avail-1',
        firstName: 'Ada',
        lastName: 'Lovelace',
        dateOfBirth: '1980-01-01',
        employerName: 'Acme Manufacturing',
        employerOccupation: 'Machinist',
        appointmentInsuranceName: 'Statewide Mutual',
        appointmentClaimExaminerName: 'Grace Hopper',
        appointmentClaimExaminerEmail: 'grace@example.test',
        appointmentClaimExaminerPhoneNumber: '5555550100',
        appointmentClaimExaminerStreet: '1 Market Street',
        appointmentClaimExaminerCity: 'Encino',
        appointmentClaimExaminerStateId: 'state-1',
        appointmentClaimExaminerZip: '90001',
      },
      { emitEvent: false },
    );
    c.injuryDrafts = [{ bodyPart: 'Left shoulder' }];
  }

  afterEach(() => TestBed.resetTestingModule());

  // ---------------------------------------------------------------- booking mode

  describe('booking mode', () => {
    it('defaults to a plain new booking', () => {
      const c = create();
      expect(c.bookingMode).toBe('new');
      expect(c.isReevaluation).toBeFalse();
      expect(c.isReRequest).toBeFalse();
      expect(c.isReBook).toBeFalse();
    });

    it('resolves a re-evaluation from the type parameter', () => {
      const c = create({ queryParams: { type: '2' } });
      expect(c.bookingMode).toBe('reval');
      expect(c.isReevaluation).toBeTrue();
    });

    it('resolves a re-book from the type parameter', () => {
      const c = create({ queryParams: { type: '3' } });
      expect(c.bookingMode).toBe('reBook');
      expect(c.isReBook).toBeTrue();
    });

    it('resolves a re-request from the mode parameter', () => {
      const c = create({ queryParams: { mode: 'rerequest' } });
      expect(c.bookingMode).toBe('reRequest');
      expect(c.isReRequest).toBeTrue();
    });

    it('shows the confirmation-number lookup only where the booker types one', () => {
      /**
       * A re-request has a source too, but it rides in the URL and auto-loads, so showing
       * the lookup there would ask for a number the booker never has to know.
       */
      expect(create({ queryParams: { type: '2' } }).showSourceLookup).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ queryParams: { type: '3' } }).showSourceLookup).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ queryParams: { mode: 'rerequest' } }).showSourceLookup).toBeFalse();
      TestBed.resetTestingModule();
      expect(create().showSourceLookup).toBeFalse();
    });

    it('requires a loaded source for every mode but a new booking', () => {
      const c = create({ queryParams: { type: '2' } });
      expect(c.isSourceLoadRequired).toBeTrue();
      c.sourceConfirmationNumber = 'C0001';
      expect(c.isSourceLoadRequired).toBeFalse();
    });

    it('never requires a source for a new booking', () => {
      expect(create().isSourceLoadRequired).toBeFalse();
    });

    it('picks the heading key per mode', () => {
      expect(create({ queryParams: { type: '2' } }).headingKey).toBe('::ReEvaluationAppointment');
      TestBed.resetTestingModule();
      expect(create({ queryParams: { mode: 'rerequest' } }).headingKey).toBe(
        '::ReRequestAppointment',
      );
      TestBed.resetTestingModule();
      expect(create().headingKey).toBe('::NewAppointment');
    });

    it('filters the type dropdown to normal evaluations for a new booking', () => {
      expect(create().appointmentTypeEvaluationContext).toBe(0);
    });

    it('filters the type dropdown to re-evaluations for a reval', () => {
      expect(create({ queryParams: { type: '2' } }).appointmentTypeEvaluationContext).toBe(1);
    });

    it('applies no type filter where the source type must stay selectable', () => {
      /**
       * A re-request resubmits the SAME appointment and a re-book can chain from a
       * re-evaluation, so either prefilled type could be classified Re. Filtering to
       * Normal would leave the prefilled value unselectable in its own dropdown.
       */
      expect(
        create({ queryParams: { mode: 'rerequest' } }).appointmentTypeEvaluationContext,
      ).toBeUndefined();
      TestBed.resetTestingModule();
      expect(
        create({ queryParams: { type: '3' } }).appointmentTypeEvaluationContext,
      ).toBeUndefined();
    });

    it('auto-loads a re-request source from the URL', () => {
      const c = create({ queryParams: { mode: 'rerequest', source: 'C0001' } });
      expect(c.isProfileLoading).toBeFalse();
      expect(c.isAutoLoadingSource).toBeTrue();
    });

    it('auto-loads a re-book source when one arrives from Book again', () => {
      const c = create({ queryParams: { type: '3', source: 'C0001' } });
      expect(c.isAutoLoadingSource).toBeTrue();
    });

    it('loads the booker profile for a re-book typed in by hand', () => {
      /**
       * Item 4: at a bare `?type=3` there is no source, so the profile load that a
       * deep-linked re-book must skip is exactly the one this path needs. The condition
       * is "a source load is in flight", not "the mode is re-book".
       */
      const c = create({ queryParams: { type: '3' } });
      expect(c.isAutoLoadingSource).toBeFalse();
      expect(restRequest.calls.allArgs().some(([req]) => req.url.includes('/me'))).toBeTrue();
    });

    it('routes the submit call by mode once a source is loaded', () => {
      const cases: Array<[Record<string, string>, unknown]> = [
        [{ type: '2' }, BookingSubmitMode.Reval],
        [{ mode: 'rerequest' }, BookingSubmitMode.ReSubmit],
        [{ type: '3' }, BookingSubmitMode.ReBook],
        [{}, BookingSubmitMode.Create],
      ];
      for (const [queryParams, expected] of cases) {
        TestBed.resetTestingModule();
        const c = create({ queryParams });
        c.sourceConfirmationNumber = 'C0001';
        expect(c.resolveSubmitMode()).withContext(JSON.stringify(queryParams)).toBe(expected);
      }
    });

    it('falls back to a plain create when no source was loaded', () => {
      // Deliberately kept from the old routing: a broken navigation state degrades to a
      // create rather than a hard refusal, and the server refuses a modeless source
      // independently.
      const c = create({ queryParams: { type: '2' } });
      c.sourceConfirmationNumber = null;
      expect(c.resolveSubmitMode()).toBe(BookingSubmitMode.Create);
    });

    it('asks for a confirmation number before looking one up', () => {
      const c = create({ queryParams: { type: '2' } });
      c.loadRevalSource('   ');
      expect(c.sourceLoadMessage).toBe('Enter the prior appointment confirmation number.');
    });

    it('sends the reval flow from the shared lookup control', () => {
      const c = create({ queryParams: { type: '2' } });
      const load = spyOn<any>(c, 'loadSourceForPrefill');
      c.loadRevalSource('C0001');
      expect(load).toHaveBeenCalledWith('C0001', 'reval');
    });

    it('sends the re-book flow from the same control', () => {
      // One lookup box serves both modes; only the flow it requests differs.
      const c = create({ queryParams: { type: '3' } });
      const load = spyOn<any>(c, 'loadSourceForPrefill');
      c.loadRevalSource(' C0002 ');
      expect(load).toHaveBeenCalledWith('C0002', 'reBook');
    });
  });

  describe('source eligibility gate', () => {
    it('accepts only an approved appointment for a re-evaluation', () => {
      const c = create();
      expect(c.checkSourceStatusForFlow(AppointmentStatusType.Approved, 'reval')).toBeNull();
      expect(c.checkSourceStatusForFlow(AppointmentStatusType.Pending, 'reval')).toContain(
        'only an approved appointment',
      );
    });

    it('accepts only a rejected appointment for a re-request', () => {
      const c = create();
      expect(c.checkSourceStatusForFlow(AppointmentStatusType.Rejected, 'reRequest')).toBeNull();
      expect(c.checkSourceStatusForFlow(AppointmentStatusType.Approved, 'reRequest')).toContain(
        'only a rejected appointment',
      );
    });

    it('refuses a re-book of an appointment that did take place', () => {
      const c = create();
      const refusal = c.checkSourceStatusForFlow(AppointmentStatusType.Approved, 'reBook');
      expect(refusal).toContain('cannot be booked again');
    });

    it('explains the re-book rule in the server wording', () => {
      // The refusal has to read the same whether it came from here or from the POST.
      const c = create();
      const refusal = c.checkSourceStatusForFlow(AppointmentStatusType.Pending, 'reBook');
      expect(refusal).toContain('cancelled');
      expect(refusal).toContain('did not attend');
    });
  });

  // ---------------------------------------------------------------- roles

  describe('role gates', () => {
    it('treats an unresolved role as a non-patient booker', () => {
      /**
       * Safer this way round: calling /patients/me for a user whose roles have not
       * loaded is a guaranteed 404 that breaks the form globally, whereas the
       * external-user profile call simply returns nothing for a patient.
       */
      const c = create({ roles: [] });
      expect(c.isExternalUserNonPatient).toBeTrue();
    });

    it('recognises a patient booker', () => {
      const c = create({ roles: ['Patient'] });
      expect(c.isExternalUserNonPatient).toBeFalse();
    });

    it('matches the role name case-insensitively', () => {
      const c = create({ roles: ['PATIENT'] });
      expect(c.isExternalUserNonPatient).toBeFalse();
    });

    it('recognises each external party role', () => {
      const cases: Array<[string, string]> = [
        ['Applicant Attorney', 'isApplicantAttorney'],
        ['Defense Attorney', 'isDefenseAttorney'],
        ['Claim Examiner', 'isClaimExaminerRole'],
        ['IT Admin', 'isItAdmin'],
      ];
      for (const [role, getter] of cases) {
        TestBed.resetTestingModule();
        const c = create({ roles: [role] });
        expect(c[getter]).withContext(role).toBeTrue();
      }
    });

    it('treats staff as an internal booker', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.isInternalBooker).toBeTrue();
    });

    it('treats every external party role as not internal', () => {
      for (const role of ['Patient', 'Applicant Attorney', 'Defense Attorney', 'Claim Examiner']) {
        TestBed.resetTestingModule();
        expect(create({ roles: [role] }).isInternalBooker)
          .withContext(role)
          .toBeFalse();
      }
    });

    it('treats an unresolved role as external, not internal', () => {
      // The opposite default to isExternalUserNonPatient, and deliberately so: guessing
      // "internal" would hand an unidentified session the 90-day horizon and auto-approval.
      expect(create({ roles: [] }).isInternalBooker).toBeFalse();
    });

    it('shows both attorney sections to everyone', () => {
      const c = create({ roles: ['Claim Examiner'] });
      expect(c.shouldShowApplicantAttorneySection()).toBeTrue();
      expect(c.shouldShowDefenseAttorneySection()).toBeTrue();
    });

    it('offers the authorized-user section only to those who may manage accessors', () => {
      for (const role of ['Staff Supervisor', 'Applicant Attorney', 'Defense Attorney']) {
        TestBed.resetTestingModule();
        expect(create({ roles: [role] }).shouldShowAuthorizedUserSection())
          .withContext(role)
          .toBeTrue();
      }
      TestBed.resetTestingModule();
      expect(create({ roles: ['Patient'] }).shouldShowAuthorizedUserSection()).toBeFalse();
    });

    it('disables the insurance fieldset for a claim examiner', () => {
      const c = create({ roles: ['Claim Examiner'] });
      expect(c.isInsuranceFieldsetDisabled).toBeTrue();
    });

    it('lets an IT admin edit insurance while booking on behalf', () => {
      const c = create({ roles: ['Claim Examiner', 'IT Admin'] });
      expect(c.isInsuranceFieldsetDisabled).toBeFalse();
    });

    it('prefills the claim examiner section for a claim-examiner booker', () => {
      const c = create({ roles: ['Claim Examiner'] });
      expect(c.form.get('appointmentClaimExaminerName')?.value).toBe('Ada Lovelace');
      expect(c.form.get('appointmentClaimExaminerEmail')?.value).toBe('ada@example.test');
    });

    it('does not prefill the claim examiner for an IT admin booking on behalf', () => {
      // The IT admin is not the examiner; seeding their identity would misattribute the claim.
      const c = create({ roles: ['Claim Examiner', 'IT Admin'] });
      expect(c.form.get('appointmentClaimExaminerName')?.value).toBeNull();
    });

    it('does not prefill the claim examiner for any other role', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.form.get('appointmentClaimExaminerName')?.value).toBeNull();
    });

    it('gives internal staff the longer booking horizon', () => {
      expect(create({ roles: ['Staff Supervisor'] }).maxBookingDays).toBe(90);
      TestBed.resetTestingModule();
      expect(create({ roles: ['Patient'] }).maxBookingDays).toBe(60);
    });
  });

  describe('display helpers', () => {
    it('shows the booker full name', () => {
      expect(create().displayUserName).toBe('Ada Lovelace');
    });

    it('shows the tenant name', () => {
      expect(create().displayTenantName).toBe('Falkinstein');
    });

    it('shows the first role, defaulting to Patient', () => {
      expect(create({ roles: ['Staff Supervisor'] }).displayRoleName).toBe('Staff Supervisor');
      TestBed.resetTestingModule();
      expect(create({ roles: [] }).displayRoleName).toBe('Patient');
    });

    it('reports a field invalid only once the booker has touched it', () => {
      // Reddening a required field the booker has not reached yet makes a blank form
      // look broken on arrival.
      const c = create();
      expect(c.isFieldInvalid('firstName')).toBeFalse();
      c.form.get('firstName')?.markAsTouched();
      expect(c.isFieldInvalid('firstName')).toBeTrue();
    });

    it('reports an unknown control as valid rather than throwing', () => {
      expect(create().isFieldInvalid('noSuchControl')).toBeFalse();
    });

    it('exposes a stable bound reference for the section inputs', () => {
      // A fresh arrow per change-detection pass would force the OnPush sections to
      // re-evaluate on every cycle.
      const c = create();
      expect(c.isFieldInvalidBound).toBe(c.isFieldInvalidBound);
      expect(c.isFieldInvalidBound('firstName')).toBeFalse();
    });

    it('derives the type-and-location gate from the form rather than storing it', () => {
      /**
       * Phase 4a: this used to be a stored flag set inside a fetch that was later
       * removed, so it stopped being set and the date UI never unhid -- a regression 452
       * green specs missed. Deriving it removes the possibility.
       */
      const c = create();
      expect(c.checkForAppointmentTypeSelected).toBeFalse();
      c.form.patchValue({ locationId: 'loc-1' }, { emitEvent: false });
      expect(c.checkForAppointmentTypeSelected).toBeFalse();
      c.form.patchValue({ appointmentTypeId: 'type-1' }, { emitEvent: false });
      expect(c.checkForAppointmentTypeSelected).toBeTrue();
    });
  });

  // ---------------------------------------------------------------- panel number

  describe('the Panel Number state machine', () => {
    it('starts cleared and disabled with no type chosen', () => {
      const c = create();
      expect(c.isPqmeType).toBeFalse();
      expect(c.form.get('panelNumber')?.disabled).toBeTrue();
    });

    it('enables and requires the field for a PQME', () => {
      const c = create();
      c.form.get('appointmentTypeId')?.setValue(PQME_TYPE_ID);
      expect(c.isPqmeType).toBeTrue();
      expect(c.form.get('panelNumber')?.enabled).toBeTrue();
      expect(c.form.get('panelNumber')?.hasError('required')).toBeTrue();
    });

    it('clears and disables the field when leaving PQME', () => {
      /**
       * A REMOVAL, so the fixture enters PQME and types a number first. Against a blank
       * form the clear is invisible -- and the bug it prevents is a legitimate AME
       * submission carrying a stale panel number.
       */
      const c = create();
      c.form.get('appointmentTypeId')?.setValue(PQME_TYPE_ID);
      c.form.get('panelNumber')?.setValue('PN-0001');
      expect(c.form.get('panelNumber')?.value).toBe('PN-0001');

      c.form.get('appointmentTypeId')?.setValue('type-ame');

      expect(c.isPqmeType).toBeFalse();
      expect(c.form.get('panelNumber')?.value).toBeNull();
      expect(c.form.get('panelNumber')?.disabled).toBeTrue();
    });

    it('drops the strike-list opt-in when leaving PQME', () => {
      const c = create();
      c.form.get('appointmentTypeId')?.setValue(PQME_TYPE_ID);
      c.hasPanelStrikeList = true;
      c.panelStrikeListMissing = true;

      c.form.get('appointmentTypeId')?.setValue('type-ame');

      expect(c.hasPanelStrikeList).toBeFalse();
      expect(c.panelStrikeListMissing).toBeFalse();
    });
  });

  // ---------------------------------------------------------------- field configs

  describe('per-type field configuration', () => {
    it('hides and disables a field the configuration marks hidden', () => {
      const c = create({
        rest: {
          'appointment-type-field-configs': of([
            {
              id: 'f1',
              appointmentTypeId: 'type-1',
              fieldName: 'refferedBy',
              hidden: true,
              readOnly: false,
            },
          ]),
        },
      });

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.isFieldHidden('refferedBy')).toBeTrue();
      expect(c.form.get('refferedBy')?.disabled).toBeTrue();
    });

    it('marks a read-only field without hiding it', () => {
      const c = create({
        rest: {
          'appointment-type-field-configs': of([
            {
              id: 'f1',
              appointmentTypeId: 'type-1',
              fieldName: 'employerName',
              hidden: false,
              readOnly: true,
            },
          ]),
        },
      });

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.isFieldReadOnly('employerName')).toBeTrue();
      expect(c.isFieldHidden('employerName')).toBeFalse();
    });

    it('applies a configured default value', () => {
      const c = create({
        rest: {
          'appointment-type-field-configs': of([
            {
              id: 'f1',
              appointmentTypeId: 'type-1',
              fieldName: 'refferedBy',
              hidden: false,
              readOnly: false,
              defaultValue: 'Intake desk',
            },
          ]),
        },
      });

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.form.get('refferedBy')?.value).toBe('Intake desk');
    });

    it('ignores an empty default value', () => {
      const c = create({
        rest: {
          'appointment-type-field-configs': of([
            {
              id: 'f1',
              appointmentTypeId: 'type-1',
              fieldName: 'refferedBy',
              hidden: false,
              readOnly: false,
              defaultValue: '',
            },
          ]),
        },
      });

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.form.get('refferedBy')?.value).toBeNull();
    });

    it('re-enables a previously hidden field when the type changes', () => {
      /**
       * A REMOVAL that needs the earlier state seeded: without the reset, switching from
       * a type that hides Referred By to one that does not would leave the control
       * disabled -- and a disabled control skips validators AND submits nothing.
       */
      const c = create();
      restResponses['appointment-type-field-configs'] = of([
        {
          id: 'f1',
          appointmentTypeId: 'type-1',
          fieldName: 'refferedBy',
          hidden: true,
          readOnly: false,
        },
      ] as never);
      c.form.get('appointmentTypeId')?.setValue('type-1');
      expect(c.form.get('refferedBy')?.disabled).toBeTrue();

      restResponses['appointment-type-field-configs'] = of([] as never);
      c.form.get('appointmentTypeId')?.setValue('type-2');

      expect(c.isFieldHidden('refferedBy')).toBeFalse();
      expect(c.form.get('refferedBy')?.enabled).toBeTrue();
    });

    // A slot's doctorAvailabilityId is scoped to the type + location it was fetched under, so a
    // slot picked before the booker changes either is stale and must not survive into the payload.
    function pickSlotUnderType1Loc1(c: Probe): void {
      c.form.get('appointmentTypeId')?.setValue('type-1');
      c.form.get('locationId')?.setValue('loc-1');
      c.form.patchValue(
        {
          appointmentDate: dateKeyIn(10),
          appointmentTime: '09:00',
          doctorAvailabilityId: 'avail-1',
        },
        { emitEvent: false },
      );
    }

    it('clears the picked time and slot id when the appointment type changes', () => {
      const c = create();
      pickSlotUnderType1Loc1(c);
      expect(c.form.get('doctorAvailabilityId')?.value).toBe('avail-1');

      c.form.get('appointmentTypeId')?.setValue('type-2');

      expect(c.form.get('appointmentTime')?.value).toBeNull();
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
    });

    it('clears the picked time and slot id when the location changes', () => {
      const c = create();
      pickSlotUnderType1Loc1(c);
      expect(c.form.get('doctorAvailabilityId')?.value).toBe('avail-1');

      c.form.get('locationId')?.setValue('loc-2');

      expect(c.form.get('appointmentTime')?.value).toBeNull();
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
    });

    it('leaves a picked slot untouched when an unrelated field changes', () => {
      const c = create();
      pickSlotUnderType1Loc1(c);

      c.form.get('firstName')?.setValue('Grace');

      expect(c.form.get('doctorAvailabilityId')?.value).toBe('avail-1');
      expect(c.form.get('appointmentTime')?.value).toBe('09:00');
    });

    it('does not fetch a configuration for a cleared type', () => {
      const c = create();
      restRequest.calls.reset();
      c.form.get('appointmentTypeId')?.setValue(null);
      expect(
        restRequest.calls
          .allArgs()
          .some(([req]) => req.url.includes('appointment-type-field-configs')),
      ).toBeFalse();
    });
  });

  describe('per-type custom fields', () => {
    it('builds one control group per active field, in display order', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([
          {
            id: 'cf2',
            fieldLabel: 'Second',
            displayOrder: 2,
            fieldType: CustomFieldType.Alphanumeric,
          },
          {
            id: 'cf1',
            fieldLabel: 'First',
            displayOrder: 1,
            fieldType: CustomFieldType.Alphanumeric,
          },
        ]),
      );

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.customFieldsArray.length).toBe(2);
      expect(c.customFieldsArray.at(0).get('fieldLabel')?.value).toBe('First');
    });

    it('drops an inactive field', () => {
      /**
       * A REMOVAL, so the fixture includes an inactive row. With only active rows the
       * filter is a no-op and this passes with it deleted -- and a retired custom field
       * would reappear on the booking form.
       */
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([
          {
            id: 'cf1',
            fieldLabel: 'Live',
            isActive: true,
            fieldType: CustomFieldType.Alphanumeric,
          },
          {
            id: 'cf2',
            fieldLabel: 'Retired',
            isActive: false,
            fieldType: CustomFieldType.Alphanumeric,
          },
        ]),
      );

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.customFieldsArray.length).toBe(1);
      expect(c.customFieldsArray.at(0).get('fieldLabel')?.value).toBe('Live');
    });

    it('requires a mandatory field', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([
          {
            id: 'cf1',
            fieldLabel: 'Claim number',
            isMandatory: true,
            fieldType: CustomFieldType.Alphanumeric,
          },
        ]),
      );

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.customFieldsArray.at(0).get('customFieldValue')?.hasError('required')).toBeTrue();
    });

    it('rejects a non-numeric answer on a numeric field', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([{ id: 'cf1', fieldLabel: 'Weeks', fieldType: CustomFieldType.Numeric }]),
      );
      c.form.get('appointmentTypeId')?.setValue('type-1');
      const control = c.customFieldsArray.at(0).get('customFieldValue');

      control?.setValue('twelve');
      expect(control?.valid).toBeFalse();
      control?.setValue('-12.5');
      expect(control?.valid).toBeTrue();
    });

    it('clears the array when the type is cleared', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([{ id: 'cf1', fieldLabel: 'Claim number', fieldType: CustomFieldType.Alphanumeric }]),
      );
      c.form.get('appointmentTypeId')?.setValue('type-1');
      expect(c.customFieldsArray.length).toBe(1);

      c.form.get('appointmentTypeId')?.setValue(null);

      expect(c.customFieldsArray.length).toBe(0);
    });

    it('clears the array when the fetch fails', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(throwError(() => ({ status: 500 })));
      c.form.get('appointmentTypeId')?.setValue('type-1');
      expect(c.customFieldsArray.length).toBe(0);
    });

    it('serialises answers into the submit payload shape', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([{ id: 'cf1', fieldLabel: 'Claim number', fieldType: CustomFieldType.Alphanumeric }]),
      );
      c.form.get('appointmentTypeId')?.setValue('type-1');
      c.customFieldsArray.at(0).get('customFieldValue')?.setValue('  WC-00042  ');

      expect(c.serializeCustomFieldValues()).toEqual([{ customFieldId: 'cf1', value: 'WC-00042' }]);
    });

    it('drops an unanswered field rather than sending a blank', () => {
      const c = create();
      getActiveForAppointmentType.and.returnValue(
        of([{ id: 'cf1', fieldLabel: 'Claim number', fieldType: CustomFieldType.Alphanumeric }]),
      );
      c.form.get('appointmentTypeId')?.setValue('type-1');
      c.customFieldsArray.at(0).get('customFieldValue')?.setValue('   ');

      expect(c.serializeCustomFieldValues()).toEqual([]);
    });

    it('serialises a multi-option tickbox as a comma-joined list', () => {
      const c = create();
      expect(
        c.serializeOneCustomFieldValue({
          fieldType: CustomFieldType.Tickbox,
          customFieldValue: ['Back', '', 'Neck'],
        }),
      ).toBe('Back,Neck');
    });

    it('serialises a single tickbox as true or false', () => {
      const c = create();
      expect(
        c.serializeOneCustomFieldValue({
          fieldType: CustomFieldType.Tickbox,
          customFieldValue: true,
        }),
      ).toBe('true');
      expect(
        c.serializeOneCustomFieldValue({
          fieldType: CustomFieldType.Tickbox,
          customFieldValue: false,
        }),
      ).toBe('false');
    });

    it('treats an unanswered field as absent rather than as the string null', () => {
      const c = create();
      expect(c.serializeOneCustomFieldValue({ customFieldValue: null })).toBeNull();
      expect(c.serializeOneCustomFieldValue({ customFieldValue: undefined })).toBeNull();
    });
  });

  // ---------------------------------------------------------------- documents

  describe('document staging', () => {
    function file(name: string, type = 'application/pdf', size = 1024): File {
      const blob = new Blob([new Uint8Array(size)], { type });
      return new File([blob], name, { type });
    }

    it('stages an acceptable file', () => {
      const c = create();
      c.onDocumentsSelected([file('records.pdf')]);
      expect(c.stagedDocuments.length).toBe(1);
      expect(c.stagedDocuments[0].status).toBe('staged');
    });

    it('rejects an unacceptable file and says which one', () => {
      // The toast names the file, because a booker dropping six attachments needs to
      // know which of them was refused.
      const c = create();
      c.onDocumentsSelected([file('script.exe', 'application/x-msdownload')]);
      expect(c.stagedDocuments.length).toBe(0);
      expect(toaster.error).toHaveBeenCalled();
      expect(toaster.error.calls.mostRecent().args[0]).toContain('script.exe');
    });

    it('stages the acceptable files from a mixed selection', () => {
      const c = create();
      c.onDocumentsSelected([file('records.pdf'), file('script.exe', 'application/x-msdownload')]);
      expect(c.stagedDocuments.map((d: any) => d.file.name)).toEqual(['records.pdf']);
    });

    it('records the strike-list opt-in', () => {
      const c = create();
      c.panelStrikeListMissing = true;
      c.onHasPanelStrikeListChange(true);
      expect(c.hasPanelStrikeList).toBeTrue();
      expect(c.panelStrikeListMissing).toBeFalse();
    });

    it('marks the document labelled Panel Strike List as the strike list', () => {
      const c = create();
      c.panelStrikeListTypeId = 'doc-strike';
      c.stagedDocuments = [
        { file: file('strike.pdf'), status: 'staged', isStrikeList: false, documentTypeId: null },
      ];

      c.onDocumentTypeChange({ index: 0, typeId: 'doc-strike' });

      expect(c.stagedDocuments[0].isStrikeList).toBeTrue();
      expect(c.hasPanelStrikeList).toBeTrue();
    });

    it('does not mark another category as the strike list', () => {
      const c = create();
      c.panelStrikeListTypeId = 'doc-strike';
      c.stagedDocuments = [
        { file: file('records.pdf'), status: 'staged', isStrikeList: false, documentTypeId: null },
      ];

      c.onDocumentTypeChange({ index: 0, typeId: 'doc-records' });

      expect(c.stagedDocuments[0].isStrikeList).toBeFalse();
    });

    it('clears the type id when the booker chooses Other', () => {
      /**
       * The two are mutually exclusive on the backend. A REMOVAL, so the fixture gives
       * the document a category and a strike-list flag first -- switching to a free-text
       * label must drop both, since a custom label is never the recognised strike list.
       */
      const c = create();
      c.stagedDocuments = [
        {
          file: file('other.pdf'),
          status: 'staged',
          isStrikeList: true,
          documentTypeId: 'doc-strike',
        },
      ];

      c.onDocumentTypeChange({ index: 0, typeId: OTHER_DOCUMENT_TYPE_VALUE });

      expect(c.stagedDocuments[0].isOtherType).toBeTrue();
      expect(c.stagedDocuments[0].documentTypeId).toBeNull();
      expect(c.stagedDocuments[0].isStrikeList).toBeFalse();
    });

    it('clears a stale free-text label when a real category is chosen', () => {
      const c = create();
      c.stagedDocuments = [
        {
          file: file('other.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
          isOtherType: true,
          otherDocumentTypeName: 'Something else',
        },
      ];

      c.onDocumentTypeChange({ index: 0, typeId: 'doc-records' });

      expect(c.stagedDocuments[0].isOtherType).toBeFalse();
      expect(c.stagedDocuments[0].otherDocumentTypeName).toBeNull();
    });

    it('ignores a type change for a row that is gone', () => {
      const c = create();
      c.stagedDocuments = [];
      expect(() => c.onDocumentTypeChange({ index: 3, typeId: 'doc-records' })).not.toThrow();
    });

    it('records the free-text label and clears its error once filled', () => {
      const c = create();
      c.stagedDocuments = [
        { file: file('other.pdf'), status: 'staged', isStrikeList: false, documentTypeId: null },
      ];
      c.otherLabelMissing = true;

      c.onOtherDocumentTypeNameChange({ index: 0, value: 'Surveillance report' });

      expect(c.stagedDocuments[0].otherDocumentTypeName).toBe('Surveillance report');
      expect(c.otherLabelMissing).toBeFalse();
    });

    it('keeps the error flag while the label is only whitespace', () => {
      const c = create();
      c.stagedDocuments = [
        { file: file('other.pdf'), status: 'staged', isStrikeList: false, documentTypeId: null },
      ];
      c.otherLabelMissing = true;

      c.onOtherDocumentTypeNameChange({ index: 0, value: '   ' });

      expect(c.otherLabelMissing).toBeTrue();
    });

    it('removes a staged document', () => {
      const c = create();
      c.stagedDocuments = [
        { file: file('a.pdf'), status: 'staged', isStrikeList: false, documentTypeId: null },
      ];
      c.removeStagedDocument(0);
      expect(c.stagedDocuments.length).toBe(0);
    });

    it('refuses to remove a file that is uploading or already uploaded', () => {
      // Yanking a file mid-upload leaves the request in flight against a row that no
      // longer exists; removing an uploaded one loses the only reference to it.
      const c = create();
      c.stagedDocuments = [
        { file: file('a.pdf'), status: 'uploading', isStrikeList: false, documentTypeId: null },
        { file: file('b.pdf'), status: 'uploaded', isStrikeList: false, documentTypeId: null },
      ];

      c.removeStagedDocument(0);
      c.removeStagedDocument(1);

      expect(c.stagedDocuments.length).toBe(2);
    });

    it('ignores a remove for a row that is gone', () => {
      const c = create();
      expect(() => c.removeStagedDocument(9)).not.toThrow();
    });

    it('loads the category list for the chosen type and caches the strike-list id', () => {
      const c = create({
        rest: {
          'options-by-type': of([
            { id: 'doc-1', displayName: 'Medical records' },
            { id: 'doc-strike', displayName: 'Panel Strike List' },
          ]),
        },
      });

      c.form.get('appointmentTypeId')?.setValue('type-1');

      expect(c.documentTypeOptions.length).toBe(2);
      expect(c.panelStrikeListTypeId).toBe('doc-strike');
    });

    it('matches the strike-list label regardless of case or padding', () => {
      const c = create({
        rest: {
          'options-by-type': of([{ id: 'doc-strike', displayName: '  panel strike LIST ' }]),
        },
      });
      c.form.get('appointmentTypeId')?.setValue('type-1');
      expect(c.panelStrikeListTypeId).toBe('doc-strike');
    });

    it('leaves the picker empty when the category fetch fails', () => {
      // Best effort: a category outage must not block a booking.
      const c = create({ rest: { 'options-by-type': throwError(() => ({ status: 500 })) } });
      c.form.get('appointmentTypeId')?.setValue('type-1');
      expect(c.documentTypeOptions).toEqual([]);
      expect(c.panelStrikeListTypeId).toBeNull();
    });

    it('clears staged labels when the appointment type changes', () => {
      /**
       * A REMOVAL needing the labels seeded: categories are scoped to the appointment
       * type, so a label carried across types would post a category that does not belong
       * to the new one.
       */
      const c = create();
      c.stagedDocuments = [
        {
          file: file('a.pdf'),
          status: 'staged',
          isStrikeList: true,
          documentTypeId: 'doc-strike',
          isOtherType: true,
          otherDocumentTypeName: 'Custom',
        },
      ];

      c.form.get('appointmentTypeId')?.setValue('type-2');

      expect(c.stagedDocuments[0].documentTypeId).toBeNull();
      expect(c.stagedDocuments[0].isStrikeList).toBeFalse();
      expect(c.stagedDocuments[0].isOtherType).toBeFalse();
      expect(c.stagedDocuments[0].otherDocumentTypeName).toBeNull();
    });
  });

  // ---------------------------------------------------------------- calendar

  describe('the booking calendar', () => {
    it('disables nothing until a type and location are chosen', () => {
      // Before that the availability set is meaningless, and greying the whole calendar
      // would read as "no appointments ever".
      const c = create();
      expect(c.markAppointmentDateDisabled(dateStructIn(10))).toBeFalse();
    });

    it('disables a date inside the lead time', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      expect(c.markAppointmentDateDisabled(dateStructIn(1))).toBeTrue();
    });

    it('disables every date beyond the absolute ceiling, whatever the role', () => {
      /**
       * 90 days is the state ceiling, so it binds internal staff too -- the 60-day
       * external horizon is handled separately, by an interception on selection.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      expect(c.markAppointmentDateDisabled(dateStructIn(120))).toBeTrue();
    });

    it('disables every date when no availability has loaded', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      expect(c.markAppointmentDateDisabled(dateStructIn(10))).toBeTrue();
    });

    it('enables a date the availability lookup offered', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      c.availableDateKeys.add(dateKeyIn(10));
      expect(c.markAppointmentDateDisabled(dateStructIn(10))).toBeFalse();
    });

    it('disables a date the lookup did not offer, even inside the window', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      c.availableDateKeys.add(dateKeyIn(10));
      expect(c.markAppointmentDateDisabled(dateStructIn(11))).toBeTrue();
    });

    it('reports which dates are available', () => {
      const c = create();
      c.availableDateKeys.add(dateKeyIn(10));
      expect(c.isAvailableAppointmentDate(dateStructIn(10))).toBeTrue();
      expect(c.isAvailableAppointmentDate(dateStructIn(11))).toBeFalse();
    });

    it('warns when the chosen date sits inside the lead time', () => {
      const c = create();
      c.form.patchValue({ appointmentDate: dateKeyIn(1) }, { emitEvent: false });
      expect(c.showMinimumBookingRuleWarning).toBeTrue();
    });

    it('does not warn for a date beyond the lead time', () => {
      const c = create();
      c.form.patchValue({ appointmentDate: dateKeyIn(10) }, { emitEvent: false });
      expect(c.showMinimumBookingRuleWarning).toBeFalse();
    });

    it('does not warn while availability is still loading, or with no date', () => {
      const c = create();
      expect(c.showMinimumBookingRuleWarning).toBeFalse();
      c.form.patchValue({ appointmentDate: dateKeyIn(1) }, { emitEvent: false });
      c.isAvailableDatesLoading = true;
      expect(c.showMinimumBookingRuleWarning).toBeFalse();
    });

    it('explains an all-disabled calendar once the lookup has resolved empty', () => {
      /**
       * Without this the booker sees a silently all-grey calendar and no reason for it.
       * The message names the lead time, which is the usual cause.
       */
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      expect(c.noBookableDatesMessage).toContain('No appointment dates are available');
      expect(c.noBookableDatesMessage).toContain('3 days');
    });

    it('says nothing before a type and location are chosen', () => {
      expect(create().noBookableDatesMessage).toBe('');
    });

    it('says nothing while the lookup is in flight', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      c.isAvailableDatesLoading = true;
      expect(c.noBookableDatesMessage).toBe('');
    });

    it('says nothing once dates are available', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', appointmentTypeId: 'type-1' }, { emitEvent: false });
      c.availableDateKeys.add(dateKeyIn(10));
      expect(c.noBookableDatesMessage).toBe('');
    });

    it('intercepts a date past the external horizon and clears it', () => {
      /**
       * External users can SEE 60-90 day slots -- the picker only caps the shared 90-day
       * ceiling -- but cannot book them online. The notice is the friendly guard; the
       * server BookingPolicyValidator stays authoritative.
       */
      const c = create({ roles: ['Patient'] });
      c.form.get('appointmentDate')?.setValue(dateKeyIn(75));

      expect(confirmationInfo).toHaveBeenCalled();
      expect(c.form.get('appointmentDate')?.value).toBeNull();
    });

    it('lets internal staff book the same date', () => {
      // The interception is role-aware. A hardcoded 60 here would have told internal
      // bookers the wrong limit, which is the 2026-08-14 fix.
      const c = create({ roles: ['Staff Supervisor'] });
      c.form.get('appointmentDate')?.setValue(dateKeyIn(75));

      expect(confirmationInfo).not.toHaveBeenCalled();
      expect(c.form.get('appointmentDate')?.value).toBe(dateKeyIn(75));
    });

    it('interpolates the caller own horizon into the notice', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.showContactStaffForFurtherBooking();
      const options = confirmationInfo.calls.mostRecent().args[2];
      expect(options.messageLocalizationParams).toEqual(['90']);
    });

    it('ignores a cleared date', () => {
      const c = create();
      c.form.get('appointmentDate')?.setValue(null);
      expect(confirmationInfo).not.toHaveBeenCalled();
    });

    it('clears the date and its slot together', () => {
      const c = create();
      c.form.patchValue(
        {
          appointmentDate: dateKeyIn(10),
          appointmentTime: '09:00',
          doctorAvailabilityId: 'avail-1',
        },
        { emitEvent: false },
      );
      c.appointmentTimeOptions = [
        { value: '09:00', label: '09:00 AM', doctorAvailabilityId: 'avail-1' },
      ];

      c.clearAppointmentDate();

      expect(c.form.get('appointmentDate')?.value).toBeNull();
      expect(c.form.get('appointmentTime')?.value).toBeNull();
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
      expect(c.appointmentTimeOptions).toEqual([]);
    });

    it('clears the due date on its own', () => {
      const c = create();
      c.form.get('dueDate')?.setValue('2026-12-01');
      c.clearDueDate();
      expect(c.form.get('dueDate')?.value).toBeNull();
    });
  });

  describe('date key parsing', () => {
    it('zero-pads a key', () => {
      expect(create().toDateKey(2026, 3, 7)).toBe('2026-03-07');
    });

    it('takes the date half of an ISO timestamp', () => {
      const c = create();
      expect(c.toDateKeyFromApi('2026-03-07T09:30:00')).toBe('2026-03-07');
      expect(c.toDateKeyFromApi('2026-03-07')).toBe('2026-03-07');
    });

    it('rejects an empty or truncated API value', () => {
      const c = create();
      expect(c.toDateKeyFromApi(null)).toBeNull();
      expect(c.toDateKeyFromApi('')).toBeNull();
      expect(c.toDateKeyFromApi('2026-03')).toBeNull();
    });

    it('takes a control value that is already a key', () => {
      expect(create().toDateKeyFromControl('2026-03-07T00:00:00')).toBe('2026-03-07');
    });

    it('parses a control value in another format', () => {
      expect(create().toDateKeyFromControl('March 7, 2026')).toBe('2026-03-07');
    });

    it('rejects an unparseable control value rather than producing a wrong date', () => {
      const c = create();
      expect(c.toDateKeyFromControl('not a date')).toBeNull();
      expect(c.toDateKeyFromControl(null)).toBeNull();
    });
  });

  describe('time slots', () => {
    it('labels morning and afternoon times in twelve-hour form', () => {
      const c = create();
      expect(c.toTimeLabel('09:30')).toBe('09:30 AM');
      expect(c.toTimeLabel('13:05')).toBe('01:05 PM');
    });

    it('labels both noon and midnight as twelve', () => {
      // `hour % 12 || 12` -- without the fallback these read "00:00".
      const c = create();
      expect(c.toTimeLabel('00:15')).toBe('12:15 AM');
      expect(c.toTimeLabel('12:00')).toBe('12:00 PM');
    });

    it('survives a malformed time rather than printing NaN', () => {
      expect(create().toTimeLabel('oops')).toBe('12:00 AM');
    });

    it('offers the slots for a date in time order', () => {
      const c = create();
      c.availableSlotsByDate.set('2026-03-07', [
        { time: '13:00', doctorAvailabilityId: 'a2' },
        { time: '09:00', doctorAvailabilityId: 'a1' },
      ]);

      c.populateTimeSlotsForDate('2026-03-07');

      expect(c.appointmentTimeOptions.map((o: any) => o.value)).toEqual(['09:00', '13:00']);
    });

    it('drops a selected time the new date does not offer', () => {
      /**
       * A REMOVAL needing the stale selection seeded. Left behind, the booking would
       * carry a doctorAvailabilityId from a different day.
       */
      const c = create();
      c.availableSlotsByDate.set('2026-03-07', [{ time: '09:00', doctorAvailabilityId: 'a1' }]);
      c.form.patchValue(
        { appointmentTime: '15:00', doctorAvailabilityId: 'a9' },
        { emitEvent: false },
      );

      c.populateTimeSlotsForDate('2026-03-07');

      expect(c.form.get('appointmentTime')?.value).toBeNull();
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
    });

    it('keeps a selected time the new date still offers', () => {
      const c = create();
      c.availableSlotsByDate.set('2026-03-07', [{ time: '09:00', doctorAvailabilityId: 'a1' }]);
      c.form.patchValue({ appointmentTime: '09:00' }, { emitEvent: false });

      c.populateTimeSlotsForDate('2026-03-07');

      expect(c.form.get('appointmentTime')?.value).toBe('09:00');
      expect(c.form.get('doctorAvailabilityId')?.value).toBe('a1');
    });

    it('resolves the availability id when a time is picked', () => {
      const c = create();
      c.appointmentTimeOptions = [
        { value: '09:00', label: '09:00 AM', doctorAvailabilityId: 'a1' },
      ];
      c.form.get('appointmentTime')?.setValue('09:00');
      expect(c.form.get('doctorAvailabilityId')?.value).toBe('a1');
    });

    it('clears the availability id when the time is cleared', () => {
      const c = create();
      c.form.patchValue({ doctorAvailabilityId: 'a1' }, { emitEvent: false });
      c.form.get('appointmentTime')?.setValue(null);
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
    });

    it('clears the availability id for a time with no matching slot', () => {
      const c = create();
      c.appointmentTimeOptions = [];
      c.form.get('appointmentTime')?.setValue('09:00');
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
    });

    it('combines the date and time into the submitted timestamp', () => {
      const c = create();
      expect(c.combineAppointmentDateAndTime('2026-03-07', '09:30')).toBe('2026-03-07T09:30');
    });

    it('defaults to midnight when no time was picked', () => {
      expect(create().combineAppointmentDateAndTime('2026-03-07', null)).toBe(
        '2026-03-07T00:00:00',
      );
    });

    it('sends nothing when there is no date', () => {
      expect(create().combineAppointmentDateAndTime(null, '09:30')).toBeUndefined();
    });
  });

  describe('location selection', () => {
    it('requires a date once a location is chosen', () => {
      const c = create();
      c.onLocationSelected('loc-1');
      expect(c.isLocationSelected).toBeTrue();
      expect(c.form.get('appointmentDate')?.hasError('required')).toBeTrue();
    });

    it('clears the whole slot selection when the location is cleared', () => {
      /**
       * A REMOVAL needing a complete selection seeded first: a date and slot belonging to
       * the previous location must not survive, because availability is per location.
       */
      const c = create();
      c.form.patchValue({ locationId: 'loc-1' }, { emitEvent: false });
      c.form.patchValue(
        { appointmentDate: dateKeyIn(10), appointmentTime: '09:00', doctorAvailabilityId: 'a1' },
        { emitEvent: false },
      );
      c.appointmentTimeOptions = [
        { value: '09:00', label: '09:00 AM', doctorAvailabilityId: 'a1' },
      ];

      c.form.get('locationId')?.setValue(null);

      expect(c.isLocationSelected).toBeFalse();
      expect(c.form.get('appointmentDate')?.value).toBeNull();
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
      expect(c.appointmentTimeOptions).toEqual([]);
    });

    it('drops the date requirement when no location is chosen', () => {
      const c = create();
      c.onLocationSelected('loc-1');
      c.onLocationSelected('');
      expect(c.form.get('appointmentDate')?.hasError('required')).toBeFalse();
    });
  });

  // ---------------------------------------------------------------- patient

  describe('patient selection', () => {
    it('loads the chosen patient into the form', () => {
      const c = create({
        rest: {
          'for-appointment-booking/': of({
            patient: {
              id: 'patient-1',
              firstName: 'Ada',
              lastName: 'Lovelace',
              email: 'ada.patient@example.test',
              city: 'Encino',
            },
          }),
        },
      });

      c.onPatientSelected('patient-1');

      expect(c.form.get('patientId')?.value).toBe('patient-1');
      expect(c.patientLabel).toBe('Ada Lovelace');
      expect(c.form.get('city')?.value).toBe('Encino');
    });

    it('never prefills the social security number', () => {
      /**
       * Design B: the SSN is write-only on this form. A REMOVAL that needs the value
       * PRESENT in the response, or the assertion would hold with the override deleted.
       * The placeholder is deliberately non-numeric -- nothing here should ever resemble
       * a real identifier.
       */
      const c = create({
        rest: {
          'for-appointment-booking/': of({
            patient: {
              id: 'patient-1',
              firstName: 'Ada',
              socialSecurityNumber: SSN_PLACEHOLDER,
            },
          }),
        },
      });

      c.onPatientSelected('patient-1');

      expect(c.form.get('socialSecurityNumber')?.value).toBeNull();
    });

    it('clears the patient section when the selection is removed', () => {
      const c = create();
      c.form.patchValue({ patientId: 'patient-1', firstName: 'Ada' }, { emitEvent: false });
      c.patientLabel = 'Ada Lovelace';

      c.onPatientSelected(null);

      expect(c.form.get('patientId')?.value).toBeNull();
      expect(c.form.get('firstName')?.value).toBeNull();
      expect(c.patientLabel).toBe('');
    });

    it('does nothing for a patient booking for themselves', () => {
      // A patient's own record is loaded from /patients/me; a picker selection here
      // would let them book against somebody else's row.
      const c = create({ roles: ['Patient'] });
      c.onPatientSelected('patient-9');
      expect(c.form.get('patientId')?.value).toBeNull();
    });

    it('treats an edited email as deselecting the loaded patient', () => {
      /**
       * Otherwise the booking would carry the SELECTED patient's id with a different
       * email typed over it -- silently booking for the wrong person.
       */
      const c = create();
      c.currentPatientProfile = { patient: { id: 'patient-1', email: 'ada@example.test' } };
      c.form.patchValue(
        { patientId: 'patient-1', email: 'someone.else@example.test' },
        { emitEvent: false },
      );

      c.onPatientEmailInputChanged();

      expect(c.form.get('patientId')?.value).toBeNull();
    });

    it('keeps the selection when the email is unchanged apart from case or padding', () => {
      const c = create();
      c.currentPatientProfile = { patient: { id: 'patient-1', email: 'Ada@Example.test' } };
      c.form.patchValue(
        { patientId: 'patient-1', email: '  ada@example.test ' },
        { emitEvent: false },
      );

      c.onPatientEmailInputChanged();

      expect(c.form.get('patientId')?.value).toBe('patient-1');
    });

    it('does nothing when no patient is loaded', () => {
      const c = create();
      expect(() => c.onPatientEmailInputChanged()).not.toThrow();
    });

    it('treats the registration gender sentinel as not provided', () => {
      /**
       * A registered-but-never-booked patient carries Gender.Unspecified (0), which is a
       * sentinel rather than an answer. Pre-selecting it would fabricate a value the
       * patient never gave.
       */
      const c = create();
      expect(c.normalizePatientGender(0)).toBeNull();
      expect(c.normalizePatientGender(null)).toBeNull();
      expect(c.normalizePatientGender(undefined)).toBeNull();
      expect(c.normalizePatientGender(2)).toBe(2);
    });
  });

  describe('profile loading', () => {
    it('blanks the patient section for an external non-patient booker', () => {
      /**
       * The booker is not the patient, so their own identity must not seed the patient
       * fields -- only the identity user id is kept, to own the appointment.
       */
      const c = create({
        rest: {
          'external-users/me': of({
            identityUserId: 'identity-1',
            firstName: 'Grace',
            lastName: 'Hopper',
            email: 'grace@example.test',
          }),
        },
        roles: ['Applicant Attorney'],
      });

      expect(c.patientLabel).toBe('Grace Hopper');
      expect(c.form.get('identityUserId')?.value).toBe('identity-1');
      expect(c.form.get('firstName')?.value).toBeNull();
      expect(c.form.get('email')?.value).toBeNull();
      expect(c.isProfileLoading).toBeFalse();
    });

    it('never seeds the attorney sections from the booker own identity', () => {
      /**
       * Firm model D7/C4: a firm or paralegal account books on behalf of a DISTINCT
       * attorney, and the details endpoint returns the firm's own registration email --
       * exactly the identity that must stay out of the on-behalf section.
       */
      const c = create({
        rest: {
          'external-users/me': of({
            identityUserId: 'identity-1',
            firstName: 'Grace',
            lastName: 'Hopper',
            email: 'grace@example.test',
          }),
        },
        roles: ['Applicant Attorney'],
      });

      expect(c.form.get('applicantAttorneyEmail')?.value).toBeNull();
      expect(c.form.get('applicantAttorneyFirstName')?.value).toBeNull();
      expect(c.applicantAttorneyId).toBeNull();
    });

    it('ignores an external profile with no identity', () => {
      const c = create({ rest: { 'external-users/me': of({}) }, roles: ['Applicant Attorney'] });
      expect(c.patientLabel).toBe('');
      expect(c.isProfileLoading).toBeFalse();
    });

    it('loads a patient booker own record', () => {
      const c = create({
        roles: ['Patient'],
        rest: {
          'patients/me': of({
            patient: { id: 'patient-1', firstName: 'Ada', lastName: 'Lovelace', city: 'Encino' },
          }),
        },
      });

      expect(c.form.get('patientId')?.value).toBe('patient-1');
      expect(c.patientLabel).toBe('Ada Lovelace');
      expect(c.form.get('city')?.value).toBe('Encino');
    });

    it('never prefills a patient booker social security number', () => {
      const c = create({
        roles: ['Patient'],
        rest: {
          'patients/me': of({
            patient: {
              id: 'patient-1',
              firstName: 'Ada',
              socialSecurityNumber: SSN_PLACEHOLDER,
            },
          }),
        },
      });
      expect(c.form.get('socialSecurityNumber')?.value).toBeNull();
    });

    it('does not load a profile while a source prefill is in flight', () => {
      // The profile load would race the prefill and null out the patient and employer
      // controls the source had just populated.
      const c = create({ queryParams: { mode: 'rerequest', source: 'C0001' } });
      expect(restRequest.calls.allArgs().some(([req]) => req.url.includes('/me'))).toBeFalse();
    });
  });

  // ---------------------------------------------------------------- interpreter

  describe('the English interpreter default', () => {
    function withEnglish(): Probe {
      return create({
        rest: {
          'appointment-language-lookup': of({
            items: [{ id: 'lang-en', displayName: 'English' }],
          }),
        },
      });
    }

    it('defaults interpreter to No when the language is English', () => {
      const c = withEnglish();
      c.form.get('appointmentLanguageId')?.setValue('lang-en');
      expect(c.form.get('needsInterpreter')?.value).toBeFalse();
    });

    it('keeps the control enabled so ASL can still be requested', () => {
      // I7 changed this from a lock to a default: an English speaker may still need an
      // interpreter, and disabling the radio made that impossible to express.
      const c = withEnglish();
      c.form.get('appointmentLanguageId')?.setValue('lang-en');
      expect(c.form.get('needsInterpreter')?.enabled).toBeTrue();
    });

    it('clears a vendor name left over from another language', () => {
      /**
       * A REMOVAL needing the vendor seeded: the @if hides the input, so without the
       * clear a stale vendor would ride along on submit invisibly.
       */
      const c = withEnglish();
      c.form.patchValue({ interpreterVendorName: 'Acme Interpreting' }, { emitEvent: false });

      c.form.get('appointmentLanguageId')?.setValue('lang-en');

      expect(c.form.get('interpreterVendorName')?.value).toBeNull();
    });

    it('leaves a non-English language alone', () => {
      const c = withEnglish();
      c.form.patchValue({ needsInterpreter: true }, { emitEvent: false });
      c.form.get('appointmentLanguageId')?.setValue('lang-es');
      expect(c.form.get('needsInterpreter')?.value).toBeTrue();
    });

    it('does not touch the control before the lookup resolves', () => {
      const c = create({ rest: { 'appointment-language-lookup': of({ items: [] }) } });
      c.form.patchValue({ needsInterpreter: true }, { emitEvent: false });
      c.form.get('appointmentLanguageId')?.setValue('lang-en');
      expect(c.form.get('needsInterpreter')?.value).toBeTrue();
    });

    it('allows a retry when the language lookup fails', () => {
      // The flag is released so the next language change re-attempts; a stuck flag would
      // disable the behaviour for the rest of the session.
      const c = create({
        rest: { 'appointment-language-lookup': throwError(() => ({ status: 500 })) },
      });
      expect(c.englishLanguageLookupComplete).toBeFalse();
    });
  });

  // ---------------------------------------------------------------- attorneys

  describe('attorney toggles', () => {
    it('leaves the section unrequired while the question is unanswered', () => {
      /**
       * Item 5: null is a THIRD state, and it is falsy. A bare `!enabled` popped the
       * self-represented confirmation at a booker over a form they had not touched.
       */
      const c = create();
      c.form.get('applicantAttorneyEnabled')?.setValue(null);
      expect(confirmationWarn).not.toHaveBeenCalled();
      expect(c.form.get('applicantAttorneyEmail')?.hasError('required')).toBeFalse();
    });

    it('requires the applicant attorney email once the section is enabled', () => {
      const c = create();
      c.form.get('applicantAttorneyEnabled')?.setValue(true);
      expect(c.form.get('applicantAttorneyEmail')?.hasError('required')).toBeTrue();
    });

    it('asks for confirmation when the applicant section is switched off', () => {
      const c = create();
      c.form.get('applicantAttorneyEnabled')?.setValue(false);
      expect(confirmationWarn).toHaveBeenCalled();
    });

    it('clears the applicant section when self-representation is confirmed', () => {
      /**
       * A REMOVAL: the email is seeded so the clear has something to remove. Without it
       * a stale address would ride along on a later submit -- the @if hides the input but
       * the control keeps its value.
       */
      const c = create({ confirmStatus: Confirmation.Status.confirm });
      c.form.get('applicantAttorneyEnabled')?.setValue(true);
      c.form.get('applicantAttorneyEmail')?.setValue('grace@example.test');

      c.form.get('applicantAttorneyEnabled')?.setValue(false);

      expect(c.form.get('applicantAttorneyEmail')?.value).toBeNull();
      expect(c.form.get('applicantAttorneyEmail')?.hasError('required')).toBeFalse();
    });

    it('reverts the applicant toggle when the booker says no', () => {
      const c = create({ confirmStatus: Confirmation.Status.reject });
      c.form.get('applicantAttorneyEnabled')?.setValue(true);

      c.form.get('applicantAttorneyEnabled')?.setValue(false);

      expect(c.form.get('applicantAttorneyEnabled')?.value).toBeTrue();
    });

    it('keeps the defense section when the booker confirms one is assigned', () => {
      /**
       * The defense modal has INVERTED polarity -- it asks "is a defense attorney
       * assigned?", so Yes means keep. Sharing the applicant's polarity here would drop
       * the section for exactly the bookers who have one.
       */
      const c = create({ confirmStatus: Confirmation.Status.confirm });
      c.form.get('defenseAttorneyEnabled')?.setValue(true);

      c.form.get('defenseAttorneyEnabled')?.setValue(false);

      expect(c.form.get('defenseAttorneyEnabled')?.value).toBeTrue();
    });

    it('clears the defense section when none is assigned', () => {
      const c = create({ confirmStatus: Confirmation.Status.reject });
      c.form.get('defenseAttorneyEnabled')?.setValue(true);
      c.form.get('defenseAttorneyEmail')?.setValue('alan@example.test');

      c.form.get('defenseAttorneyEnabled')?.setValue(false);

      expect(c.form.get('defenseAttorneyEmail')?.value).toBeNull();
    });

    it('clears the claim examiner email when that section is switched off', () => {
      const c = create();
      c.form.get('claimExaminerEnabled')?.setValue(true);
      c.form.get('claimExaminerEmail')?.setValue('grace@example.test');

      c.form.get('claimExaminerEnabled')?.setValue(false);

      expect(c.form.get('claimExaminerEmail')?.value).toBeNull();
    });
  });

  describe('attorney lookups', () => {
    const attorneyRow = {
      applicantAttorneyId: 'aa-1',
      defenseAttorneyId: 'da-1',
      identityUserId: 'identity-9',
      firstName: 'Grace',
      lastName: 'Hopper',
      email: 'grace@example.test',
      firmName: 'Hopper & Co',
      city: 'Encino',
      concurrencyStamp: 'stamp-1',
    };

    it('records the search term', () => {
      const c = create();
      c.onApplicantAttorneyEmailSearch({ target: { value: '  grace@example.test ' } } as any);
      expect(c.applicantAttorneyEmailSearch).toBe('grace@example.test');
    });

    it('does not search on an empty term', () => {
      const c = create();
      c.applicantAttorneyEmailSearch = '   ';
      restRequest.calls.reset();
      c.loadApplicantAttorneyByEmail();
      expect(restRequest).not.toHaveBeenCalled();
      expect(c.isApplicantAttorneyLoading).toBeFalse();
    });

    it('fills the applicant section from an email search', () => {
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(attorneyRow) } });
      c.applicantAttorneyEmailSearch = 'grace@example.test';

      c.loadApplicantAttorneyByEmail();

      expect(c.form.get('applicantAttorneyFirstName')?.value).toBe('Grace');
      expect(c.form.get('applicantAttorneyFirmName')?.value).toBe('Hopper & Co');
      expect(c.isApplicantAttorneyLoading).toBeFalse();
    });

    it('keeps the master id and stamp from a deliberate pick', () => {
      /**
       * Prefill deliberately does NOT carry these -- an id sends the server down its
       * id-present branch, which blanks fields left empty on the SHARED master record.
       * A booker who explicitly picked an existing attorney does expect that update,
       * which is why these two paths differ.
       */
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(attorneyRow) } });
      c.applicantAttorneyEmailSearch = 'grace@example.test';

      c.loadApplicantAttorneyByEmail();

      expect(c.applicantAttorneyId).toBe('aa-1');
      expect(c.applicantAttorneyConcurrencyStamp).toBe('stamp-1');
    });

    it('leaves the section alone when the search finds nobody', () => {
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(null) } });
      c.applicantAttorneyEmailSearch = 'nobody@example.test';

      c.loadApplicantAttorneyByEmail();

      expect(c.form.get('applicantAttorneyFirstName')?.value).toBeNull();
      expect(c.isApplicantAttorneyLoading).toBeFalse();
    });

    it('fills the applicant section from a picked identity', () => {
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(attorneyRow) } });
      c.onApplicantAttorneySelected('identity-9');
      expect(c.form.get('applicantAttorneyEmail')?.value).toBe('grace@example.test');
    });

    it('clears the applicant section when the pick is removed', () => {
      const c = create();
      c.applicantAttorneyId = 'aa-1';
      c.applicantAttorneyConcurrencyStamp = 'stamp-1';
      c.form.patchValue({ applicantAttorneyFirstName: 'Grace' }, { emitEvent: false });

      c.onApplicantAttorneySelected(null);

      expect(c.form.get('applicantAttorneyFirstName')?.value).toBeNull();
      expect(c.applicantAttorneyId).toBeNull();
      expect(c.applicantAttorneyConcurrencyStamp).toBeNull();
    });

    it('mirrors all of that for the defense section', () => {
      const c = create({ rest: { 'defense-attorney-details-for-booking': of(attorneyRow) } });
      c.onDefenseAttorneyEmailSearch({ target: { value: 'alan@example.test' } } as any);
      expect(c.defenseAttorneyEmailSearch).toBe('alan@example.test');

      c.loadDefenseAttorneyByEmail();
      expect(c.form.get('defenseAttorneyFirstName')?.value).toBe('Grace');
      expect(c.defenseAttorneyId).toBe('da-1');

      c.onDefenseAttorneySelected(null);
      expect(c.form.get('defenseAttorneyFirstName')?.value).toBeNull();
      expect(c.defenseAttorneyId).toBeNull();
    });

    it('does not search the defense endpoint on an empty term', () => {
      const c = create();
      c.defenseAttorneyEmailSearch = '';
      restRequest.calls.reset();
      c.loadDefenseAttorneyByEmail();
      expect(restRequest).not.toHaveBeenCalled();
    });

    it('fills the defense section from a picked identity', () => {
      const c = create({ rest: { 'defense-attorney-details-for-booking': of(attorneyRow) } });
      c.onDefenseAttorneySelected('identity-9');
      expect(c.form.get('defenseAttorneyEmail')?.value).toBe('grace@example.test');
    });
  });

  describe('authorized users', () => {
    it('splits the lookup into per-role option lists', () => {
      const c = create({
        rest: {
          'external-user-lookup': of({
            items: [
              { identityUserId: 'u1', userRole: 'Applicant Attorney' },
              { identityUserId: 'u2', userRole: 'Defense Attorney' },
              { identityUserId: 'u3', userRole: 'Patient' },
            ],
          }),
        },
      });

      expect(c.externalAuthorizedUserOptions.length).toBe(3);
      expect(c.applicantAttorneyOptions.map((o: any) => o.identityUserId)).toEqual(['u1']);
      expect(c.defenseAttorneyOptions.map((o: any) => o.identityUserId)).toEqual(['u2']);
    });

    it('backfills a role that a prefill could not resolve', () => {
      /**
       * A re-request auto-prefill can build the accessor drafts BEFORE this lookup
       * resolves, leaving userRole blank. The fixture seeds exactly that blank, because
       * with a populated role the backfill is a no-op.
       */
      const c = create();
      c.appointmentAuthorizedUsers = [{ identityUserId: 'u1', userRole: '' }];
      c.externalAuthorizedUserOptions = [{ identityUserId: 'u1', userRole: 'Applicant Attorney' }];

      c.backfillAuthorizedUserRoles();

      expect(c.appointmentAuthorizedUsers[0].userRole).toBe('Applicant Attorney');
    });

    it('keeps a role already resolved', () => {
      const c = create();
      c.appointmentAuthorizedUsers = [{ identityUserId: 'u1', userRole: 'Defense Attorney' }];
      c.externalAuthorizedUserOptions = [{ identityUserId: 'u1', userRole: '' }];

      c.backfillAuthorizedUserRoles();

      expect(c.appointmentAuthorizedUsers[0].userRole).toBe('Defense Attorney');
    });

    it('does nothing when either list is empty', () => {
      const c = create();
      c.appointmentAuthorizedUsers = [];
      c.externalAuthorizedUserOptions = [{ identityUserId: 'u1', userRole: 'x' }];
      expect(() => c.backfillAuthorizedUserRoles()).not.toThrow();
    });
  });

  // ---------------------------------------------------------------- prefill picker

  describe('the prefill picker', () => {
    it('is used by the two modes that start from an aged source', () => {
      /**
       * A re-request corrects a rejected request that is usually hours old with the
       * problem fields already flagged, so the question there is noise in front of a task
       * the booker already understands.
       */
      expect(create({ queryParams: { type: '2' } }).usesPrefillPicker).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ queryParams: { type: '3' } }).usesPrefillPicker).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ queryParams: { mode: 'rerequest' } }).usesPrefillPicker).toBeFalse();
      TestBed.resetTestingModule();
      expect(create().usesPrefillPicker).toBeFalse();
    });

    it('opens once a source is loaded and the question is unasked', () => {
      const c = create({ queryParams: { type: '2' } });
      c.sourceConfirmationNumber = 'C0001';
      c.openPrefillPickerIfDue();
      expect(c.prefillPickerVisible).toBeTrue();
    });

    it('does not re-ask once the booker has answered', () => {
      const c = create({ queryParams: { type: '2' } });
      c.sourceConfirmationNumber = 'C0001';
      c.prefillSelection = {};
      c.openPrefillPickerIfDue();
      expect(c.prefillPickerVisible).toBeFalse();
    });

    it('does not open before a source is loaded', () => {
      const c = create({ queryParams: { type: '2' } });
      c.openPrefillPickerIfDue();
      expect(c.prefillPickerVisible).toBeFalse();
    });

    it('reopens on demand even after an answer', () => {
      // Someone reaching the Defense step and not recognising the name should not have
      // to restart the booking.
      const c = create({ queryParams: { type: '2' } });
      c.sourceConfirmationNumber = 'C0001';
      c.prefillSelection = {};
      c.reopenPrefillPicker();
      expect(c.prefillPickerVisible).toBeTrue();
    });

    it('does not reopen for a booking that never uses the picker', () => {
      const c = create();
      c.sourceConfirmationNumber = 'C0001';
      c.reopenPrefillPicker();
      expect(c.prefillPickerVisible).toBeFalse();
    });

    it('reports a section dirty once one of its controls is edited', () => {
      const c = create();
      c.form.get('applicantAttorneyFirstName')?.markAsDirty();
      expect(c.prefillSectionIsDirty('applicantAttorney')).toBeTrue();
    });

    it('reports an untouched section clean', () => {
      expect(create().prefillSectionIsDirty('applicantAttorney')).toBeFalse();
    });
  });

  // ---------------------------------------------------------------- submit gates

  describe('submit gates', () => {
    it('refuses a second submit while one is in flight', () => {
      /**
       * The guard is set synchronously before the first await, because the disabled
       * button alone was insufficient -- an impatient double-click used to run the whole
       * submit twice, creating a duplicate patient and a 500 on the second POST.
       */
      const c = create();
      c.isSaving = true;
      c.onSubmit();
      expect(submit).not.toHaveBeenCalled();
    });

    it('blocks a re-evaluation with no source loaded', () => {
      const c = create({ queryParams: { type: '2' } });
      c.onSubmit();
      expect(c.sourceLoadMessage).toContain('prior approved appointment');
      expect(submit).not.toHaveBeenCalled();
    });

    it('blocks a re-book with its own wording', () => {
      const c = create({ queryParams: { type: '3' } });
      c.onSubmit();
      expect(c.sourceLoadMessage).toContain('book again');
    });

    it('blocks a re-request with its own wording', () => {
      const c = create({ queryParams: { mode: 'rerequest' } });
      c.onSubmit();
      expect(c.sourceLoadMessage).toContain('could not be loaded');
    });

    it('names the fields needed to create a new patient', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      c.onSubmit();
      expect(c.patientLoadMessage).toContain('First Name, Last Name');
      expect(c.form.get('firstName')?.touched).toBeTrue();
    });

    it('does not demand a patient email when an applicant attorney is present', () => {
      /**
       * task_d5407b22: injured workers often have no email, and patient mail falls back
       * to the applicant attorney server-side. The two messages differ precisely here.
       */
      const c = create({ roles: ['Applicant Attorney'] });
      c.form.patchValue({ applicantAttorneyEnabled: true }, { emitEvent: false });

      c.onSubmit();

      expect(c.patientLoadMessage).not.toContain('Email');
    });

    it('demands a patient email for a self-represented applicant', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      c.form.patchValue({ applicantAttorneyEnabled: false }, { emitEvent: false });

      c.onSubmit();

      expect(c.patientLoadMessage).toContain('Email');
    });

    it('flags a missing patient id for a patient booker', () => {
      const c = create({ roles: ['Patient'] });
      c.onSubmit();
      expect(c.form.get('patientId')?.hasError('required')).toBeTrue();
      expect(submit).not.toHaveBeenCalled();
    });

    it('refuses an incomplete form and marks every control touched', () => {
      const c = create();
      c.form.patchValue({ patientId: 'patient-1' }, { emitEvent: false });

      c.onSubmit();

      expect(c.patientLoadMessage).toContain('complete all required fields');
      expect(c.form.get('employerName')?.touched).toBeTrue();
      expect(submit).not.toHaveBeenCalled();
    });

    it('requires at least one claim information entry', () => {
      // OLD parity: injuries gate approval server-side, so a booking with none cannot
      // proceed past Pending anyway.
      const c = create();
      fillValidForm(c);
      c.injuryDrafts = [];

      c.onSubmit();

      expect(c.claimInformationMissing).toBeTrue();
      expect(c.patientLoadMessage).toContain('Claim Information');
      expect(submit).not.toHaveBeenCalled();
    });

    it('requires a marked strike list once a PQME booker opts in', () => {
      const c = create();
      fillValidForm(c);
      c.isPqmeType = true;
      c.hasPanelStrikeList = true;
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];

      c.onSubmit();

      expect(c.panelStrikeListMissing).toBeTrue();
      expect(submit).not.toHaveBeenCalled();
    });

    it('requires a name for every document labelled Other', () => {
      const c = create();
      fillValidForm(c);
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
          isOtherType: true,
          otherDocumentTypeName: '   ',
        },
      ];

      c.onSubmit();

      expect(c.otherLabelMissing).toBeTrue();
      expect(submit).not.toHaveBeenCalled();
    });
  });

  describe('a successful submit', () => {
    it('posts one payload carrying the appointment and its child groups', async () => {
      /**
       * This replaced an appointment POST followed by seven child POSTs, each committing
       * on its own. A failure part-way left a half-booking -- production appointments
       * with full attorney columns and zero join rows. One call is the fix.
       */
      const c = create();
      fillValidForm(c);

      await c.onSubmit();

      expect(submit).toHaveBeenCalledTimes(1);
      const payload = submit.calls.mostRecent().args[0];
      expect(payload.mode).toBe(BookingSubmitMode.Create);
      expect(payload.appointmentStatus).toBe(AppointmentStatusType.Pending);
      expect(payload.locationId).toBe('loc-1');
      expect(payload.injuryDetails).toBeTruthy();
    });

    it('combines the chosen date and slot into one timestamp', async () => {
      const c = create();
      fillValidForm(c);

      await c.onSubmit();

      expect(submit.calls.mostRecent().args[0].appointmentDate).toBe(`${dateKeyIn(10)}T09:00`);
    });

    it('lets the server allocate the confirmation number', async () => {
      /**
       * A REMOVAL: the form HAS a requestConfirmationNumber control, defaulted to 'A',
       * and it must not be sent. The server allocates the number and derives the
       * returning-patient flag from what deduplication actually decided; a client value
       * could disagree with what happened.
       */
      const c = create();
      fillValidForm(c);
      expect(c.form.get('requestConfirmationNumber')?.value).toBe('A');

      await c.onSubmit();

      const payload = submit.calls.mostRecent().args[0];
      expect(payload.requestConfirmationNumber).toBeUndefined();
      expect(payload.isPatientAlreadyExist).toBeUndefined();
    });

    it('sends an attorney email only when that section is enabled', async () => {
      const c = create();
      fillValidForm(c);
      c.form.patchValue(
        {
          applicantAttorneyEnabled: false,
          applicantAttorneyEmail: 'grace@example.test',
          defenseAttorneyEnabled: true,
          defenseAttorneyEmail: 'alan@example.test',
        },
        { emitEvent: false },
      );

      await c.onSubmit();

      const payload = submit.calls.mostRecent().args[0];
      expect(payload.applicantAttorneyEmail).toBeUndefined();
      expect(payload.defenseAttorneyEmail).toBe('alan@example.test');
    });

    it('sends the appointment-level claim examiner email for the fan-out', async () => {
      const c = create();
      fillValidForm(c);

      await c.onSubmit();

      expect(submit.calls.mostRecent().args[0].claimExaminerEmail).toBe('grace@example.test');
    });

    it('navigates once the booking is complete', async () => {
      const c = create();
      fillValidForm(c);

      await c.onSubmit();

      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
      expect(c.isSaving).toBeFalse();
    });

    it('releases the in-flight guard even when the POST fails', async () => {
      const c = create();
      submit.and.returnValue(
        throwError(() => ({ error: { error: { message: 'Booking failed.' } } })),
      );
      fillValidForm(c);

      await c.onSubmit();

      expect(c.isSaving).toBeFalse();
      expect(toaster.error).toHaveBeenCalledWith('Booking failed.');
    });

    it('clears the slot and warns when the slot was taken', async () => {
      /**
       * ABP screens only 401/403/404/500, so a 400 reaches here. Clearing the slot is
       * what makes the next attempt pick from current server state rather than retrying
       * a slot that is gone.
       */
      const c = create();
      submit.and.returnValue(
        throwError(() => ({
          error: {
            error: {
              code: 'CaseEvaluation:Appointment.BookingSlotFull',
              message: 'That slot has just filled.',
            },
          },
        })),
      );
      fillValidForm(c);

      await c.onSubmit();

      expect(toaster.warn).toHaveBeenCalledWith('That slot has just filled.');
      expect(c.form.get('appointmentTime')?.value).toBeNull();
      expect(c.form.get('doctorAvailabilityId')?.value).toBeNull();
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('routes save through the same submit', () => {
      const c = create();
      const onSubmit = spyOn(c as any, 'onSubmit');
      c.save();
      expect(onSubmit).toHaveBeenCalled();
    });
  });

  describe('auto-approval for internal bookers', () => {
    it('does not approve an external booking', async () => {
      const c = create({ roles: ['Applicant Attorney'] });
      await c.autoApproveIfInternalBooker('appt-1');
      expect(approveAppointment).not.toHaveBeenCalled();
    });

    it('does not approve without an appointment id', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      await c.autoApproveIfInternalBooker(undefined);
      expect(approveAppointment).not.toHaveBeenCalled();
    });

    it('approves an internal booking', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      await c.autoApproveIfInternalBooker('appt-1');
      expect(approveAppointment).toHaveBeenCalledWith('appt-1', {
        primaryResponsibleUserId: 'user-1',
      });
    });

    it('retries once for an unclassified failure', async () => {
      // At most one retry, and only here: a refused gate cannot succeed on a second
      // identical attempt, so retrying it would just delay the message the booker needs.
      const c = create({ roles: ['Staff Supervisor'] });
      approveAppointment.and.returnValue(throwError(() => ({ status: 500 })));

      await c.autoApproveIfInternalBooker('appt-1');

      expect(approveAppointment).toHaveBeenCalledTimes(2);
      expect(toaster.warn).toHaveBeenCalledTimes(1);
    });

    it('treats an already-approved appointment as success', async () => {
      /**
       * Idempotent: a double submit, or a previous attempt that reached Approved, is not
       * a failure and must not tell the booker to go approve it by hand.
       *
       * The code is `NotPendingForApproval` -- read from auto-approve-outcome.ts rather
       * than guessed. The first version of this test invented `AlreadyApproved`, which
       * classified as `retryable`, so the call was retried and the test failed on the
       * count. An invented error code is indistinguishable from an unhandled one.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      approveAppointment.and.returnValue(
        throwError(() => ({
          error: { error: { code: 'CaseEvaluation:Appointment.NotPendingForApproval' } },
        })),
      );

      await c.autoApproveIfInternalBooker('appt-1');

      expect(approveAppointment).toHaveBeenCalledTimes(1);
      expect(toaster.warn).not.toHaveBeenCalled();
    });

    it('shows the server reason for a refused gate and does not retry', async () => {
      /**
       * A gate refusal names something the booker can act on, and retrying without acting
       * is guaranteed to fail again -- so the retry must NOT happen, and the server's own
       * message is more useful than the generic fallback.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      approveAppointment.and.returnValue(
        throwError(() => ({
          error: {
            error: {
              code: 'CaseEvaluation:Appointment.ApprovalRequiresPanelStrikeList',
              message: 'A panel strike list is required before approval.',
            },
          },
        })),
      );

      await c.autoApproveIfInternalBooker('appt-1');

      expect(approveAppointment).toHaveBeenCalledTimes(1);
      expect(toaster.warn).toHaveBeenCalledWith('A panel strike list is required before approval.');
    });

    it('warns when the booker has no resolvable responsible user', async () => {
      // Was a silent return. An internal booking that can never be auto-approved must not
      // be left Pending with no explanation.
      const c = create({ roles: ['Staff Supervisor'] });
      // Shadow the prototype getter on the instance. `spyOnProperty` cannot reach a
      // private accessor declared on the class prototype.
      Object.defineProperty(c, 'currentUser', {
        get: () => ({ roles: ['Staff Supervisor'] }),
        configurable: true,
      });

      await c.autoApproveIfInternalBooker('appt-1');

      expect(approveAppointment).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalled();
    });
  });

  describe('staged document upload', () => {
    it('reports success when there is nothing to upload', async () => {
      const c = create();
      await expectAsync(c.uploadStagedDocuments('appt-1')).toBeResolvedTo(true);
    });

    it('reports success when there is no appointment to upload to', async () => {
      const c = create();
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];
      await expectAsync(c.uploadStagedDocuments(undefined)).toBeResolvedTo(true);
    });

    it('uploads each staged file and marks it done', async () => {
      const c = create();
      c.stagedDocuments = [
        {
          file: new File([''], 'records.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: 'doc-1',
        },
      ];

      const ok = await c.uploadStagedDocuments('appt-1');

      expect(ok).toBeTrue();
      expect(c.stagedDocuments[0].status).toBe('uploaded');
    });

    it('skips a file that already uploaded, so a retry cannot duplicate it', async () => {
      const c = create();
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'uploaded',
          isStrikeList: false,
          documentTypeId: null,
        },
        {
          file: new File([''], 'b.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];
      restRequest.calls.reset();

      await c.uploadStagedDocuments('appt-1');

      const posts = restRequest.calls.allArgs().filter(([req]) => req.method === 'POST');
      expect(posts.length).toBe(1);
    });

    it('keeps a failed file for retry and reports the failure', async () => {
      /**
       * No rollback, deliberately -- matching the existing non-atomic child behaviour.
       * The file stays staged so the Documents section can re-POST it to the SAME
       * appointment rather than creating a duplicate booking.
       */
      const c = create();
      restResponses['documents'] = throwError(() => ({
        error: { error: { message: 'File too large.' } },
      }));
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];

      const ok = await c.uploadStagedDocuments('appt-1');

      expect(ok).toBeFalse();
      expect(c.stagedDocuments[0].status).toBe('failed');
      expect(c.stagedDocuments[0].error).toBe('File too large.');
    });

    it('falls back to a generic message when the server gave none', async () => {
      const c = create();
      restResponses['documents'] = throwError(() => ({}));
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'staged',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];

      await c.uploadStagedDocuments('appt-1');

      expect(c.stagedDocuments[0].error).toBe('Upload failed.');
    });

    it('re-posts to the same appointment on retry', async () => {
      const c = create();
      c.createdAppointmentIdForRetry = 'appt-1';
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'failed',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];

      await c.retryStagedUploads();

      expect(c.stagedDocuments[0].status).toBe('uploaded');
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
      expect(c.isSaving).toBeFalse();
    });

    it('does not retry while a submit is in flight', async () => {
      const c = create();
      c.isSaving = true;
      await c.retryStagedUploads();
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('stays put when the retry also fails', async () => {
      const c = create();
      restResponses['documents'] = throwError(() => ({}));
      c.createdAppointmentIdForRetry = 'appt-1';
      c.stagedDocuments = [
        {
          file: new File([''], 'a.pdf'),
          status: 'failed',
          isStrikeList: false,
          documentTypeId: null,
        },
      ];

      await c.retryStagedUploads();

      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });
  });

  // ---------------------------------------------------------------- misc

  describe('address dialog formatting', () => {
    it('builds a street line and a city line', () => {
      const c = create();
      expect(
        c.formatAddressLines('1 Market Street', 'Suite 4', 'Encino', 'California', '90001'),
      ).toEqual(['1 Market Street Suite 4', 'Encino, California 90001']);
    });

    it('omits the parts that are missing', () => {
      const c = create();
      expect(c.formatAddressLines('1 Market Street', null, 'Encino', null, null)).toEqual([
        '1 Market Street',
        'Encino',
      ]);
    });

    it('produces no lines for an empty address', () => {
      expect(create().formatAddressLines(null, null, null, null, null)).toEqual([]);
    });

    it('hands the booker choice back to the waiting submit', () => {
      const c = create();
      const resolve = jasmine.createSpy('resolve');
      c.addressDialogResolve = resolve;

      c.onAddressDialogResolved({ patient: 'suggested' });

      expect(resolve).toHaveBeenCalledWith({ patient: 'suggested' });
    });

    it('does nothing when no dialog is waiting', () => {
      const c = create();
      expect(() => c.onAddressDialogResolved({})).not.toThrow();
    });
  });

  describe('attorney question copy', () => {
    /**
     * CORRECTED AFTER READING attorney-question.util.ts. The first version asserted that
     * this returns '' where the viewer IS that attorney type. It does not, and the
     * distinction matters: `attorneyQuestionText` ALWAYS returns wording, and whether the
     * question is asked at all is a separate decision in `shouldAskAttorneyQuestion`,
     * which this component does not call. So this method resolves COPY, not visibility.
     */
    it('uses claim language for a booker who is not the patient', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.attorneyQuestionFor('applicant')).toBe('Is the applicant represented?');
    });

    it('uses jargon-free wording for a patient booker', () => {
      // Patients are the one audience who would not necessarily know that "the applicant"
      // means them.
      const c = create({ roles: ['Patient'] });
      expect(c.attorneyQuestionFor('applicant')).toBe('Do you have an attorney representing you?');
    });

    it('asks the defense question the same way for everyone', () => {
      const staff = create({ roles: ['Staff Supervisor'] });
      expect(staff.attorneyQuestionFor('defense')).toBe('Is there a defense attorney?');
      TestBed.resetTestingModule();
      const patient = create({ roles: ['Patient'] });
      expect(patient.attorneyQuestionFor('defense')).toBe('Is there a defense attorney?');
    });

    it('still returns wording when the viewer is that attorney type', () => {
      // Visibility is decided elsewhere; returning '' here would be a different design.
      const c = create({ roles: ['Applicant Attorney'] });
      expect(c.attorneyQuestionFor('applicant')).toBe('Is the applicant represented?');
    });
  });

  describe('reset and navigation', () => {
    it('clears the form and its slot state', () => {
      const c = create();
      c.form.patchValue({ locationId: 'loc-1', firstName: 'Ada' }, { emitEvent: false });
      c.appointmentTimeOptions = [
        { value: '09:00', label: '09:00 AM', doctorAvailabilityId: 'a1' },
      ];

      c.reset();

      expect(c.form.get('firstName')?.value).toBeNull();
      expect(c.isLocationSelected).toBeFalse();
      expect(c.appointmentTimeOptions).toEqual([]);
    });

    it('leaves the attorney questions unanswered after a reset', () => {
      /**
       * BUG-044 used to restore these to true. Item 5 removed the default, so null is now
       * the CORRECT post-reset state -- re-asserting true would put back exactly the
       * silent default the change removed: a fresh form claiming both attorneys exist.
       */
      const c = create();
      c.form.patchValue(
        { applicantAttorneyEnabled: true, defenseAttorneyEnabled: true },
        { emitEvent: false },
      );

      c.reset();

      expect(c.form.get('applicantAttorneyEnabled')?.value).toBeNull();
      expect(c.form.get('defenseAttorneyEnabled')?.value).toBeNull();
    });

    it('sends the booker home from Back and Cancel', () => {
      const c = create();
      c.goBack();
      c.cancel();
      expect(router.navigateByUrl.calls.allArgs()).toEqual([['/'], ['/']]);
    });

    it('opens the patient profile page', () => {
      const c = create();
      c.openMyProfile();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/user-management/patients/my-profile');
    });
  });
});
