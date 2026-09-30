import { TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentAddComponent } from './appointment-add.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../proxy/custom-fields-controllers/custom-fields.service';
import { AddressValidationProvider } from '../shared/address/address-validation.provider';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import { CustomFieldType } from '../proxy/enums/custom-field-type.enum';
import { SECTION_CONTROLS, defaultPrefillSelection } from './shared/prefill-sections';

/**
 * The booking engine's remaining uncovered paths: restoring a prefill section, the patient
 * lookups, stale responses on an appointment-type change, custom-field building and
 * serialising, the source status gate, staged-document uploads, address standardisation, the
 * English interpreter lock, and the patient-by-email load.
 *
 * <p>A sibling of `appointment-add-engine.spec.ts` rather than more of it: that spec is already
 * 2,680 lines. It mirrors that spec's harness -- `runInInjectionContext(() => new
 * AppointmentAddComponent())`, a RestService routed by URL fragment -- and builds its own.</p>
 *
 * <p>Every stub on a path through a next-only subscribe returns success; a failing one there would
 * be an unhandled error, not a test. The one timing-dependent flow, the address dialog, is driven
 * under `fakeAsync` with `flushMicrotasks` only -- no timers.</p>
 *
 * <p>All names, emails, addresses and identifiers below are synthetic.</p>
 */
describe('AppointmentAddComponent remaining paths', () => {
  let toaster: { warn: jasmine.Spy; error: jasmine.Spy; success: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy; navigate: jasmine.Spy };
  let restRequest: jasmine.Spy;
  let getActiveForAppointmentType: jasmine.Spy;
  let validate: jasmine.Spy;

  interface Probe {
    [key: string]: any;
  }

  type Responder = Observable<unknown> | (() => Observable<unknown>);

  function defaultRest(url: string): Observable<unknown> {
    if (url.includes('appointment-type-field-configs')) return of([]);
    if (url.includes('options-by-type')) return of([]);
    if (url.includes('by-appointment')) return of([]);
    if (url.includes('applicant-attorney') || url.includes('defense-attorney')) return of(null);
    if (url.includes('external-users/me')) return of(null);
    if (url.includes('patients/me')) return of(null);
    if (url.includes('state-lookup')) {
      return of({ items: [{ id: 'state-1', displayName: 'California' }] });
    }
    return of({ items: [], totalCount: 0 });
  }

  function create(
    options: { roles?: string[]; rest?: Record<string, Responder>; source?: unknown } = {},
  ): Probe {
    const responders = options.rest ?? {};
    toaster = {
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
      success: jasmine.createSpy('success'),
    };
    router = {
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
      navigate: jasmine.createSpy('navigate'),
    };
    restRequest = jasmine.createSpy('request').and.callFake((req: { url: string }) => {
      for (const [fragment, responder] of Object.entries(responders)) {
        if (req.url.includes(fragment)) {
          return typeof responder === 'function' ? responder() : responder;
        }
      }
      return defaultRest(req.url);
    });
    getActiveForAppointmentType = jasmine
      .createSpy('getActiveForAppointmentType')
      .and.returnValue(of([]));
    validate = jasmine
      .createSpy('validate')
      .and.returnValue(of({ status: 'ok', standardized: null, matchesInput: true }));

    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: { request: restRequest } },
        {
          provide: AppointmentService,
          useValue: {
            submit: () => of({ appointmentId: 'appt-1' }),
            getByConfirmationNumber: () => of(options.source ?? { appointment: null }),
          },
        },
        { provide: AppointmentApprovalService, useValue: { approveAppointment: () => of(null) } },
        {
          provide: CustomFieldsService,
          useValue: { getActiveForAppointmentType, getList: () => of({ items: [] }) },
        },
        { provide: ActivatedRoute, useValue: { queryParamMap: of({ get: () => null }) } },
        { provide: Router, useValue: router },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) =>
              key === 'currentUser'
                ? { id: 'user-1', email: 'booker@example.test', roles: options.roles ?? [] }
                : null,
            getDeep: () => null,
          },
        },
        { provide: ToasterService, useValue: toaster },
        {
          provide: ConfirmationService,
          useValue: {
            warn: () => of(Confirmation.Status.confirm),
            info: () => of(Confirmation.Status.confirm),
          },
        },
        { provide: AddressValidationProvider, useValue: { validate } },
      ],
    });

    return TestBed.runInInjectionContext(() => new AppointmentAddComponent()) as unknown as Probe;
  }

  /** The requests whose URL contains the fragment, in order. */
  function requestsTo(fragment: string): { url: string; params?: any; body?: any }[] {
    return restRequest.calls
      .allArgs()
      .map((args) => args[0])
      .filter((req: { url: string }) => req.url.includes(fragment));
  }

  afterEach(() => TestBed.resetTestingModule());

  // ------------------------------------------------------------------ prefill restore

  describe('flipping a prefill section back to unchanged', () => {
    it("restores that section's source values, and only that section's", () => {
      expect(SECTION_CONTROLS.patient).toContain('firstName');
      const c = create();
      c.retainedPrefillPatch = { firstName: 'Ada', lastName: 'Example', employerName: 'Kept Co' };
      c.prefillSelection = { ...defaultPrefillSelection(), patient: true };
      c.form.patchValue({ firstName: null, lastName: 'Edited', employerName: 'Typed Co' });

      c.applyPrefillSelection(defaultPrefillSelection());

      expect(c.form.get('firstName').value).toBe('Ada');
      expect(c.form.get('lastName').value).toBe('Example');
      expect(c.form.get('employerName').value).withContext('another section').toBe('Typed Co');
      expect(c.prefillPickerVisible).toBeFalse();
    });

    it('leaves the form as it is when no source values were retained', () => {
      const c = create();
      c.prefillSelection = { ...defaultPrefillSelection(), patient: true };
      c.form.patchValue({ firstName: 'Typed' });

      c.applyPrefillSelection(defaultPrefillSelection());

      expect(c.form.get('firstName').value).toBe('Typed');
      expect(c.prefillSelection).toEqual(defaultPrefillSelection());
    });
  });

  // ------------------------------------------------------------------ lookups

  describe('lookups', () => {
    it('reads appointment statuses from the status lookup', () => {
      const c = create();
      c.getAppointmentStatusLookup({ filter: 'pend', skipCount: 10, maxResultCount: 5 });

      const req = requestsTo('appointment-status-lookup')[0];
      expect(req.url).toBe('/api/app/appointments/appointment-status-lookup');
      expect(req.params).toEqual({ filter: 'pend', skipCount: 10, maxResultCount: 5 });
    });

    function search(c: Probe, term: string): unknown[] | undefined {
      let result: unknown[] | undefined;
      // A completing source flushes debounceTime's pending value at once, so this needs no timer.
      c.searchPatientByEmail(of(term)).subscribe((rows: unknown[]) => (result = rows));
      return result;
    }

    it('does not search the patient list for fewer than two characters', () => {
      const c = create();
      expect(search(c, ' a ')).toEqual([]);
      expect(requestsTo('patient-lookup')).toEqual([]);
    });

    it('searches by the trimmed term and returns the matching rows', () => {
      const rows = [{ id: 'p-1', displayName: 'ada@example.test' }];
      const c = create({ rest: { 'patient-lookup': of({ items: rows }) } });

      expect(search(c, '  ada@ex  ')).toEqual(rows);
      expect(requestsTo('patient-lookup')[0].params).toEqual({
        filter: 'ada@ex',
        skipCount: 0,
        maxResultCount: 20,
      });
    });

    it('returns no rows rather than breaking the search box when the lookup fails', () => {
      const c = create({ rest: { 'patient-lookup': throwError(() => ({ status: 500 })) } });
      expect(search(c, 'ada@ex')).toEqual([]);
    });
  });

  // ------------------------------------------------------------------ appointment-type change

  describe('changing the appointment type', () => {
    it('ignores field configs that arrive after the type changed again', () => {
      const first = new Subject<unknown[]>();
      const second = new Subject<unknown[]>();
      const queue = [first, second];
      const c = create({ rest: { 'appointment-type-field-configs': () => queue.shift()! } });

      c.applyFieldConfigsForAppointmentType('type-1');
      c.applyFieldConfigsForAppointmentType('type-2');
      first.next([{ fieldName: 'firstName', defaultValue: 'Stale' }]);
      expect(c.form.get('firstName').value).not.toBe('Stale');

      second.next([{ fieldName: 'firstName', defaultValue: 'Fresh' }]);
      expect(c.form.get('firstName').value).toBe('Fresh');
    });

    it('re-enables a read-only field once the type is cleared', () => {
      const c = create({
        rest: { 'appointment-type-field-configs': of([{ fieldName: 'lastName', readOnly: true }]) },
      });

      c.applyFieldConfigsForAppointmentType('type-1');
      expect(c.form.get('lastName').disabled).toBeTrue();

      c.applyFieldConfigsForAppointmentType(null);
      expect(c.form.get('lastName').enabled).toBeTrue();
    });

    it('ignores custom fields that arrive after the type changed again', () => {
      const first = new Subject<unknown[]>();
      const second = new Subject<unknown[]>();
      const c = create();
      getActiveForAppointmentType.and.returnValues(first, second);

      c.loadCustomFieldsForAppointmentType('type-1');
      c.loadCustomFieldsForAppointmentType('type-2');
      first.next([{ id: 'cf-stale', fieldType: CustomFieldType.Alphanumeric }]);
      expect(c.customFieldsArray.length).toBe(0);

      second.next([{ id: 'cf-fresh', fieldType: CustomFieldType.Alphanumeric }]);
      expect(c.customFieldsArray.length).toBe(1);
      expect(c.customFieldsArray.at(0).value.customFieldId).toBe('cf-fresh');
    });
  });

  // ------------------------------------------------------------------ custom fields

  describe('custom fields', () => {
    it('caps an alphanumeric field at its configured length', () => {
      const c = create();
      const group = c.buildCustomFieldGroup({
        id: 'cf-1',
        fieldType: CustomFieldType.Alphanumeric,
        fieldLength: 5,
      });

      group.get('customFieldValue').setValue('abcdef');
      expect(group.get('customFieldValue').hasError('maxlength')).toBeTrue();

      group.get('customFieldValue').setValue('abcde');
      expect(group.get('customFieldValue').valid).toBeTrue();
    });

    it('writes any other tickbox value as text', () => {
      const c = create();
      expect(
        c.serializeOneCustomFieldValue({ fieldType: CustomFieldType.Tickbox, customFieldValue: 1 }),
      ).toBe('1');
    });

    it('writes a non-text value of any other type as text', () => {
      const c = create();
      expect(
        c.serializeOneCustomFieldValue({
          fieldType: CustomFieldType.Numeric,
          customFieldValue: 42,
        }),
      ).toBe('42');
    });
  });

  // ------------------------------------------------------------------ source prefill

  describe('prefilling from a source appointment', () => {
    it('refuses a re-evaluation of an appointment that is not approved', async () => {
      const c = create({
        source: { appointment: { id: 'a1', appointmentStatus: AppointmentStatusType.Pending } },
      });

      await c.loadSourceForPrefill('A00001', 'reval');

      expect(c.sourceLoadMessage).toBe('You can re-evaluate only an approved appointment.');
      expect(c.sourceConfirmationNumber).toBeFalsy();
    });

    it("reuses the source appointment's patient", async () => {
      const c = create({
        source: {
          appointment: { id: 'a1', appointmentStatus: AppointmentStatusType.Approved },
          patient: { id: 'p-1', firstName: 'Ada', lastName: 'Example', identityUserId: 'u-9' },
        },
      });

      await c.loadSourceForPrefill('A00001', 'reval');

      expect(c.currentPatientProfile.patient.id).toBe('p-1');
      expect(c.currentPatientProfile.isExisting).toBeTrue();
      expect(c.patientLabel).toBe('Ada Example');
      expect(c.form.get('patientId').value).toBe('p-1');
      expect(c.form.get('identityUserId').value).toBe('u-9');
    });
  });

  // ------------------------------------------------------------------ staged documents

  describe('staged documents', () => {
    const file = () => new File(['x'], 'scan.pdf', { type: 'application/pdf' });

    function staged(over: Record<string, unknown> = {}) {
      return { file: file(), status: 'staged', isStrikeList: false, ...over };
    }

    it('ignores an "Other" label edit for a row that is not there', () => {
      const c = create();
      c.stagedDocuments = [];
      c.onOtherDocumentTypeNameChange({ index: 3, value: 'Imaging CD' });
      expect(c.stagedDocuments).toEqual([]);
      expect(c.otherLabelMissing).toBeFalse();
    });

    it('uploads an "Other" document with its trimmed free-text label', async () => {
      const c = create();
      c.stagedDocuments = [staged({ isOtherType: true, otherDocumentTypeName: '  Imaging CD  ' })];

      expect(await c.uploadStagedDocuments('appt-1')).toBeTrue();

      const body = requestsTo('appt-1/documents')[0].body as FormData;
      expect(body.get('otherDocumentTypeName')).toBe('Imaging CD');
      expect(body.get('appointmentDocumentTypeId')).toBeNull();
    });

    it('sends no label for an "Other" document whose label is blank', async () => {
      const c = create();
      c.stagedDocuments = [staged({ isOtherType: true, otherDocumentTypeName: '   ' })];

      await c.uploadStagedDocuments('appt-1');

      const body = requestsTo('appt-1/documents')[0].body as FormData;
      expect(body.has('otherDocumentTypeName')).toBeFalse();
    });

    it('keeps the booker on the form when a document fails after the booking is created', async () => {
      const c = create({
        rest: {
          'appt-1/documents': throwError(() => ({ error: { error: { message: 'Too large.' } } })),
        },
      });
      fillValidForm(c);
      c.stagedDocuments = [staged()];

      await c.onSubmit();

      expect(toaster.warn).toHaveBeenCalledWith(
        'Appointment created, but some documents failed to upload. Retry from the Documents section.',
      );
      expect(router.navigateByUrl).not.toHaveBeenCalled();
      expect(c.stagedDocuments[0].status).toBe('failed');
      expect(c.stagedDocuments[0].error).toBe('Too large.');
    });
  });

  // ------------------------------------------------------------------ address standardisation

  describe('address standardisation before submit', () => {
    function withPatientAddress(c: Probe): void {
      c.form.patchValue(
        {
          street: '1 Example Way',
          address: null,
          city: 'encino',
          stateId: 'state-1',
          zipCode: '00000',
        },
        { emitEvent: false },
      );
    }

    const suggestion = {
      status: 'ok',
      matchesInput: false,
      standardized: {
        street: '1 Example Way',
        suite: 'Apt 2',
        city: 'Encino',
        state: 'CA',
        zip: '00000-0001',
      },
    };

    it('offers a differing suggestion and applies it when the booker takes it', fakeAsync(() => {
      const c = create();
      withPatientAddress(c);
      validate.and.returnValue(of(suggestion));

      c.standardizeAddressesBeforeSubmit();
      flushMicrotasks();

      expect(c.addressDialogItems).toEqual([
        {
          key: 'patient',
          label: 'Patient address',
          enteredLines: ['1 Example Way', 'encino, California 00000'],
          suggestedLines: ['1 Example Way Apt 2', 'Encino, California 00000-0001'],
        },
      ]);

      c.onAddressDialogResolved({ patient: 'suggested' });
      flushMicrotasks();

      expect(c.addressDialogItems).toBeNull();
      expect(c.form.get('city').value).toBe('Encino');
      expect(c.form.get('zipCode').value).toBe('00000-0001');
      expect(c.form.get('address').value).toBe('Apt 2');
      expect(c.form.get('stateId').value).toBe('state-1');
    }));

    it("keeps the booker's address when they choose their own", fakeAsync(() => {
      const c = create();
      withPatientAddress(c);
      validate.and.returnValue(of(suggestion));

      c.standardizeAddressesBeforeSubmit();
      flushMicrotasks();
      c.onAddressDialogResolved({ patient: 'mine' });
      flushMicrotasks();

      expect(c.form.get('city').value).toBe('encino');
      expect(c.form.get('zipCode').value).toBe('00000');
    }));
  });

  // ------------------------------------------------------------------ English interpreter lock

  describe('the English interpreter lock', () => {
    it('looks the English language up only once', () => {
      const c = create();
      const englishLookups = () =>
        requestsTo('language-lookup').filter((r) => r.params?.filter === 'English').length;
      expect(englishLookups()).withContext('on construction').toBe(1);

      c.loadEnglishLanguageId();

      expect(englishLookups()).toBe(1);
    });

    it('leaves the form alone when it has no interpreter question', () => {
      const c = create();
      c.englishLanguageId = 'lang-en';
      c.form.get('interpreterVendorName').setValue('Example Interpreting');
      c.form.removeControl('needsInterpreter');

      c.applyEnglishInterpreterLock('lang-en');

      expect(c.form.get('interpreterVendorName').value).toBe('Example Interpreting');
    });
  });

  // ------------------------------------------------------------------ patient by email

  describe('loading a patient by email', () => {
    it('does nothing without an email', async () => {
      const c = create();
      c.form.get('email').setValue('   ');

      await c.loadPatientByEmail();

      expect(requestsTo('by-email')).toEqual([]);
      expect(c.patientLoadMessage).toBe('');
    });

    it('fills the form from the patient it finds, never the SSN', async () => {
      const c = create({
        rest: {
          'by-email': of({
            patient: {
              id: 'p-1',
              firstName: 'Ada',
              lastName: 'Example',
              email: 'ada@example.test',
              interpreterVendorName: 'Example Interpreting',
            },
          }),
        },
      });
      c.form.get('email').setValue('ada@example.test');
      c.form.get('socialSecurityNumber').setValue('SYNTHETIC-PLACEHOLDER');

      await c.loadPatientByEmail();

      expect(c.form.get('patientId').value).toBe('p-1');
      expect(c.form.get('firstName').value).toBe('Ada');
      expect(c.form.get('needsInterpreter').value).toBeTrue();
      expect(c.form.get('socialSecurityNumber').value).toBeNull();
      expect(c.patientLabel).toBe('Ada Example');
      expect(c.patientLoadMessage).toBe('Patient loaded. You can edit details below if needed.');
    });

    it('clears any chosen patient and invites a new one when none is found', async () => {
      const c = create({ rest: { 'by-email': of(null) } });
      c.form.get('email').setValue('nobody@example.test');
      c.form.get('patientId').setValue('p-old');
      c.patientLabel = 'Old Patient';

      await c.loadPatientByEmail();

      expect(c.form.get('patientId').value).toBeNull();
      expect(c.currentPatientProfile).toBeUndefined();
      expect(c.patientLabel).toBe('');
      expect(c.patientLoadMessage).toBe(
        'No patient found with this email. Fill in the form below to create a new patient.',
      );
    });
  });

  // ------------------------------------------------------------------ patient selection

  describe('choosing a patient', () => {
    it('ignores a chosen patient whose record comes back empty', () => {
      const c = create({
        roles: ['Applicant Attorney'],
        rest: { 'for-appointment-booking/p-1': of({ patient: null }) },
      });
      c.form.get('firstName').setValue('Typed');

      c.onPatientSelected('p-1');

      expect(requestsTo('for-appointment-booking/p-1')).toHaveSize(1);
      expect(c.form.get('firstName').value).toBe('Typed');
      expect(c.currentPatientProfile).toBeUndefined();
    });

    it('keeps the chosen patient when a patient booker edits the email', () => {
      // Only a booker acting for someone else picks a patient, so only they lose the pick when
      // the email stops matching. A patient booking for themselves keeps their own record.
      const c = create({ roles: ['Patient'] });
      const profile = { patient: { id: 'p-1', email: 'ada@example.test' } };
      c.currentPatientProfile = profile;
      c.form.get('email').setValue('other@example.test');

      c.onPatientEmailInputChanged();

      expect(c.currentPatientProfile).toBe(profile);
    });
  });

  // ------------------------------------------------------------------ helpers

  /** A calendar key N days from today, in local time -- never a hardcoded date. */
  function dateKeyIn(days: number): string {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + days);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  /** Fill every control carrying Validators.required, as the engine spec does. */
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
        lastName: 'Example',
        dateOfBirth: '1980-01-01',
        employerName: 'Example Manufacturing',
        employerOccupation: 'Machinist',
        appointmentInsuranceName: 'Example Mutual',
        appointmentClaimExaminerName: 'Grace Example',
        appointmentClaimExaminerEmail: 'grace@example.test',
        appointmentClaimExaminerPhoneNumber: '5555550100',
        appointmentClaimExaminerStreet: '1 Example Way',
        appointmentClaimExaminerCity: 'Encino',
        appointmentClaimExaminerStateId: 'state-1',
        appointmentClaimExaminerZip: '00000',
      },
      { emitEvent: false },
    );
    c.injuryDrafts = [{ bodyPart: 'Left shoulder' }];
  }
});
