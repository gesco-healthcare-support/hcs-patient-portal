import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
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
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';

/**
 * The EXTERNAL appointment detail -- what a patient or attorney sees.
 *
 * <p>It extends `AppointmentViewComponent`, so the inherited engine is covered by
 * `appointment-view-engine.spec.ts`; this file covers only what the subclass adds: the status
 * banner and its callout copy, the read-only ledger accessors, and the Send Back "fix it" flow
 * where the clinic flags fields and the requester corrects them in place.</p>
 *
 * <p>The fix-it flow is the substance. Staff flag a set of fields; the requester edits each one
 * inline, acknowledges documents and claim information separately, and can only resubmit once
 * EVERY flagged item is addressed. Getting the progress gate wrong either strands a requester
 * on a form they cannot submit, or lets a request go back to the clinic still incomplete.</p>
 *
 * <p>NOT TESTED, deliberately: the success path of `confirmResubmit`, which ends in
 * `window.location.reload()`. `window.location` is [Unforgeable] -- it cannot be spied, and
 * attempting it lets the real call run, which in karma reloads the test page and kills the
 * whole suite. That exact mistake aborted a run at 549 of 682 tests earlier in this epic. Every
 * test below that touches `confirmResubmit` drives it down a path that returns BEFORE the
 * reload, and says which. Logged to docs/backlog.md alongside the same problem in
 * `openRescheduleSource`.</p>
 *
 * <p>All patients, attorneys and firms below are synthetic.</p>
 */
describe('ExternalAppointmentDetailComponent', () => {
  let restRequest: jasmine.Spy;
  let restResponses: Record<string, Observable<unknown>>;
  let getWithNav: jasmine.Spy;
  let getHistory: jasmine.Spy;
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; error: jasmine.Spy };
  let routeId: string | null;
  let currentUser: unknown;
  let currentTenant: unknown;

  interface Probe {
    [key: string]: any;
  }

  function loadedAppointment(over: Record<string, unknown> = {}) {
    return {
      appointment: {
        id: 'appt-1',
        requestConfirmationNumber: 'C0001',
        appointmentStatus: AppointmentStatusType.Pending,
        appointmentDate: '2026-10-01T09:00:00',
        creationTime: '2026-09-01T09:00:00',
        applicantAttorneyEmail: 'grace@example.test',
        claimExaminerEmail: 'examiner@example.test',
        ...over,
      },
      patient: {
        id: 'patient-1',
        firstName: 'Ada',
        lastName: 'Lovelace',
        dateOfBirth: '1980-01-01T00:00:00',
        street: '1 Market Street',
        city: 'Encino',
        stateId: 'state-1',
        zipCode: '90001',
        cellPhoneNumber: '5555550100',
        appointmentLanguageId: 'lang-1',
      },
      appointmentType: { name: 'AME' },
      location: { name: 'Encino' },
      primaryInsurance: { name: 'Statewide Mutual' },
      appointmentDefenseAttorney: { defenseAttorney: { firmName: 'Turing & Co' } },
    };
  }

  function defaultRest(url: string): Observable<unknown> {
    if (url.includes('state-lookup')) {
      return of({ items: [{ id: 'state-1', displayName: 'California' }] });
    }
    if (url.includes('appointment-language-lookup')) {
      return of({ items: [{ id: 'lang-1', displayName: 'Spanish' }] });
    }
    if (url.includes('external-users/me')) return of({ firmName: 'Lovelace LLP' });
    if (url.includes('info-requests/open')) return of(null);
    if (url.includes('external-user-lookup')) return of({ items: [] });
    if (url.includes('appointment-accessors')) return of({ items: [] });
    if (url.includes('appointment-employer-details')) return of({ items: [] });
    if (url.includes('by-appointment')) return of([]);
    return of(null);
  }

  function create(
    options: {
      roles?: string[];
      id?: string | null;
      rest?: Record<string, Observable<unknown>>;
      status?: AppointmentStatusType;
      tenant?: unknown;
    } = {},
  ): Probe {
    restResponses = options.rest ?? {};
    routeId = options.id === undefined ? 'appt-1' : options.id;
    currentUser = {
      id: 'user-1',
      name: 'Ada',
      surname: 'Lovelace',
      email: 'ada@example.test',
      roles: options.roles ?? ['Patient'],
    };
    currentTenant = 'tenant' in options ? options.tenant : { name: 'Valley Orthopaedics' };

    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
    };
    getWithNav = jasmine
      .createSpy('getWithNavigationProperties')
      .and.returnValue(
        of(loadedAppointment(options.status ? { appointmentStatus: options.status } : {})),
      );
    getHistory = jasmine.createSpy('getHistory').and.returnValue(of([]));
    restRequest = jasmine.createSpy('request').and.callFake((req: { url: string }) => {
      for (const [fragment, response] of Object.entries(restResponses)) {
        if (req.url.includes(fragment)) return response;
      }
      return defaultRest(req.url);
    });

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeId } } },
        },
        { provide: Router, useValue: router },
        { provide: HttpClient, useValue: { get: () => of(null) } },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) => (k === 'currentTenant' ? currentTenant : currentUser),
            getAll: () => ({}),
          },
        },
        {
          provide: AppointmentService,
          useValue: {
            getWithNavigationProperties: getWithNav,
            update: () => of({}),
            getAppointmentCustomFieldValues: () => of([]),
          },
        },
        { provide: RestService, useValue: { request: restRequest } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => 'https://api.test' } },
        { provide: ToasterService, useValue: toaster },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ConfirmationService, useValue: { warn: () => of(null) } },
        {
          provide: AppointmentChangeRequestService,
          useValue: { getActiveForAppointment: () => of(null) },
        },
        { provide: AppointmentInfoRequestService, useValue: { getHistory } },
      ],
    });
    return TestBed.runInInjectionContext(
      () => new ExternalAppointmentDetailComponent(),
    ) as unknown as Probe;
  }

  /** An open info request flagging the given field keys. */
  function withFlags(c: Probe, keys: string[], hints: Record<string, string> = {}): void {
    c.infoRequest = {
      note: 'Please correct the highlighted details.',
      flaggedFields: keys.map((key) => ({ key, hint: hints[key] ?? null })),
    };
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the status banner', () => {
    it('derives the pill, variant and label from the status', () => {
      const c = create({ status: AppointmentStatusType.Approved });
      c.ngOnInit();
      expect(c.pill).toBeTruthy();
      expect(c.bannerVariant).toBe('approved');
      expect(c.statusLabel).toBeTruthy();
    });

    it('defaults to pending before the appointment loads', () => {
      const c = create();
      expect(c.pill).toBeTruthy();
      expect(c.callout).toBe(c.callout);
    });

    it('gives an in-flight reschedule its own copy', () => {
      /**
       * Phase 4c: a reschedule awaiting consent used to fall into the `rescheduled` callout
       * and tell the patient "the new date and time are shown above" while nothing had
       * moved. The two states are genuinely different and must read differently.
       */
      const c = create({ status: AppointmentStatusType.RescheduleRequested });
      c.ngOnInit();
      expect(c.bannerVariant).toBe('reschedule-requested');
      expect(c.callout.title).toBe('Reschedule requested');
      expect(c.callout.body).toContain('must agree');
    });

    it('gives an in-flight cancellation its own copy', () => {
      const c = create({ status: AppointmentStatusType.CancellationRequested });
      c.ngOnInit();
      expect(c.bannerVariant).toBe('cancellation-requested');
      expect(c.callout.body).toContain('still scheduled');
    });

    it('words the two attendance outcomes without blame', () => {
      // A Not Seen is often a clinic-side cause, and the patient cannot tell which from
      // here, so neither callout attributes fault.
      const noShow = create({ status: AppointmentStatusType.NoShow });
      noShow.ngOnInit();
      expect(noShow.callout.title).toBe('Appointment missed');

      TestBed.resetTestingModule();
      const notSeen = create({ status: AppointmentStatusType.NotSeen });
      notSeen.ngOnInit();
      expect(notSeen.callout.title).toBe('Evaluation not completed');
    });

    it('falls back to the pending copy for an unmapped variant', () => {
      const c = create();
      c.ngOnInit();
      expect(c.callout).toBeTruthy();
    });

    it('shows the outcome note only for settled outcomes', () => {
      /**
       * The two in-flight variants are excluded because no outcome exists yet. The
       * attendance outcomes ARE settled -- and a no-showed appointment already showed this
       * section back when it fell into the `cancelled` variant, so leaving them out would
       * silently DROP a section rather than relabel one.
       */
      for (const [status, expected] of [
        [AppointmentStatusType.Approved, true],
        [AppointmentStatusType.Rejected, true],
        [AppointmentStatusType.NoShow, true],
        [AppointmentStatusType.NotSeen, true],
        [AppointmentStatusType.RescheduleRequested, false],
        [AppointmentStatusType.CancellationRequested, false],
      ] as Array<[AppointmentStatusType, boolean]>) {
        TestBed.resetTestingModule();
        const c = create({ status });
        c.ngOnInit();
        expect(c.showOutcomeNote).withContext(AppointmentStatusType[status]).toBe(expected);
      }
    });
  });

  describe('the read-only ledger', () => {
    it('reads the appointment nav properties', () => {
      const c = create();
      c.ngOnInit();
      expect(c.apptTypeName).toBe('AME');
      expect(c.locationDisplayName).toBe('Encino');
      expect(c.confNo).toBe('C0001');
      expect(c.apptDate).toBe('2026-10-01T09:00:00');
      expect(c.requestedOn).toBe('2026-09-01T09:00:00');
    });

    it('is empty before the appointment loads', () => {
      const c = create();
      expect(c.apptTypeName).toBe('');
      expect(c.locationDisplayName).toBe('');
      expect(c.confNo).toBe('');
    });

    it('joins the patient display name from the form', () => {
      const c = create();
      c.ngOnInit();
      expect(c.patientDisplayName).toBe('Ada Lovelace');
    });

    it('renders a form value as a display string', () => {
      const c = create();
      c.ngOnInit();
      expect(c.fv('patientFirstName')).toBe('Ada');
      expect(c.fv('noSuchControl')).toBe('');
    });

    it('resolves the gender label from the option list', () => {
      const c = create();
      c.ngOnInit();
      expect(typeof c.genderLabel).toBe('string');
    });

    it('reports whether an interpreter is needed', () => {
      const c = create();
      c.ngOnInit();
      expect(c.needsInterpreter).toBeFalse();
      c.form.get('patientNeedsInterpreter')?.setValue(true);
      expect(c.needsInterpreter).toBeTrue();
    });

    it('resolves a language id to its name once the lookup loads', () => {
      const c = create();
      c.ngOnInit();
      expect(c.languageName('lang-1')).toBe('Spanish');
      expect(c.languageName('unknown')).toBe('');
      expect(c.languageName(null)).toBe('');
    });

    it('leaves the language blank when the lookup fails', () => {
      // Best-effort: the field degrades to plain text rather than blocking the page.
      const c = create({
        rest: { 'appointment-language-lookup': throwError(() => ({ status: 500 })) },
      });
      c.ngOnInit();
      expect(c.languageOptions).toEqual([]);
    });

    it('loads the state options', () => {
      const c = create();
      c.ngOnInit();
      expect(c.stateOptions.length).toBe(1);
    });

    /**
     * NOT TESTED: that a FAILING state lookup leaves `stateOptions` empty.
     *
     * The subclass's `loadStateOptions` handles the error correctly. The problem is that the
     * INHERITED `loadStateNames` (appointment-view.component.ts:320) hits the SAME endpoint
     * from the same `ngOnInit`, and subscribes with a `next` handler and no `error` one -- so
     * making that endpoint fail produces an unhandled RxJS error from code this spec is not
     * about. It surfaced as "An error was thrown in afterAll / [object Object] thrown" after
     * all 66 tests had passed, which is a teardown failure with no failing test attached to
     * it: the hardest kind to attribute.
     *
     * The missing error handler is a real defect, logged to docs/backlog.md. Adding one is a
     * production change and does not belong in a coverage PR. The language lookup below IS
     * tested for failure, because the parent never calls that endpoint.
     */

    it('scrolls to a section anchor', () => {
      const c = create();
      const anchor = document.createElement('div');
      anchor.id = 'section-documents';
      const scroll = spyOn(anchor, 'scrollIntoView');
      document.body.appendChild(anchor);

      try {
        c.scrollTo('section-documents');
        expect(scroll).toHaveBeenCalled();
      } finally {
        anchor.remove();
      }
    });

    it('does not throw scrolling to a missing anchor', () => {
      const c = create();
      expect(() => c.scrollTo('nope')).not.toThrow();
    });
  });

  describe('the navbar', () => {
    it('resolves the clinic, email and role', () => {
      const c = create({ roles: ['Patient'] });
      c.ngOnInit();
      expect(c.navClinicName).toBe('Valley Orthopaedics');
      expect(c.navUserEmailText).toBe('ada@example.test');
      expect(c.navRoleLabelText).toBe('Patient');
    });

    it('falls back to the app name with no tenant', () => {
      const c = create({ tenant: null });
      c.ngOnInit();
      expect(c.navClinicName).toBe('Appointment Portal');
    });

    it('prefers the firm name once the profile resolves', () => {
      // A firm account has no personal name; showing a raw email instead of the firm is
      // what this resolution prevents.
      const c = create();
      c.ngOnInit();
      expect(c.firmName).toBe('Lovelace LLP');
      expect(c.navDisplayName).toBeTruthy();
    });

    it('keeps a display name when the firm lookup fails', () => {
      const c = create({ rest: { 'external-users/me': throwError(() => ({ status: 500 })) } });
      c.ngOnInit();
      expect(c.firmName).toBe('');
      expect(c.navDisplayName).toBeTruthy();
    });

    it('routes each external role to its own profile page', () => {
      /**
       * Each role edits a different record; sending an attorney to the patient profile
       * would show them somebody else's form.
       */
      for (const [role, target] of [
        ['Applicant Attorney', '/user-management/attorneys/my-profile'],
        ['Defense Attorney', '/user-management/attorneys/my-profile'],
        ['Claim Examiner', '/user-management/claim-examiners/my-profile'],
        ['Patient', '/user-management/patients/my-profile'],
      ] as Array<[string, string]>) {
        TestBed.resetTestingModule();
        const c = create({ roles: [role] });
        c.openProfileNav();
        expect(router.navigateByUrl).withContext(role).toHaveBeenCalledWith(target);
      }
    });

    it('sends the documents and back links home', () => {
      const c = create();
      c.openDocumentsNav();
      c.backToHome();
      expect(router.navigateByUrl.calls.allArgs()).toEqual([['/'], ['/']]);
    });

    it('opens the query modal', () => {
      const c = create();
      c.openQuery();
      expect(c.submitQueryVisible).toBeTrue();
    });
  });

  describe('re-request and re-book overrides', () => {
    it('sends a re-request to the booking wizard', () => {
      // The override exists only to use shellRouter -- the parent's router is private --
      // and the contract is identical.
      const c = create();
      c.ngOnInit();
      c.reRequest();
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/request'], {
        queryParams: { mode: 'rerequest', source: 'C0001' },
      });
    });

    it('sends a re-book with the type key', () => {
      const c = create();
      c.ngOnInit();
      c.reBook();
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/request'], {
        queryParams: { type: 3, source: 'C0001' },
      });
    });

    it('does neither without a confirmation number', () => {
      const c = create();
      getWithNav.and.returnValue(of(loadedAppointment({ requestConfirmationNumber: null })));
      c.ngOnInit();

      c.reRequest();
      c.reBook();

      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('the fix-it flow', () => {
    it('has no flagged fields on an ordinary read-only view', () => {
      const c = create();
      c.ngOnInit();
      expect(c.hasFlaggedFields).toBeFalse();
      expect(c.flaggedKeys).toEqual([]);
      expect(c.totalFlagged).toBe(0);
    });

    it('stays an ordinary view when the open-request lookup fails', () => {
      // A 404 here just means no open request; it must not break the detail page.
      const c = create({ rest: { 'info-requests/open': throwError(() => ({ status: 404 })) } });
      c.ngOnInit();
      expect(c.infoRequest).toBeNull();
    });

    it('lists the flagged keys in the order staff selected them', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['dateOfBirth', 'documents', 'city']);
      expect(c.flaggedKeys).toEqual(['dateOfBirth', 'documents', 'city']);
      expect(c.hasFlaggedFields).toBeTrue();
    });

    it('separates the inline-editable keys from documents', () => {
      /**
       * Documents are replaced through the upload section, not typed into a field, so they
       * are acknowledged rather than edited. Treating them as editable would render a text
       * box for a PDF.
       */
      const c = create();
      c.ngOnInit();
      withFlags(c, ['dateOfBirth', 'documents']);
      expect(c.editableFlaggedKeys).toEqual(['dateOfBirth']);
      expect(c.documentFlagged).toBeTrue();
    });

    it('recognises a flagged claim-information section', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['claimInformation']);
      expect(c.claimInformationFlagged).toBeTrue();
    });

    it('seeds each editable field from the loaded appointment', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['dateOfBirth', 'city', 'applicantAttorneyEmail', 'appointmentInsuranceName']);

      c.seedEdits();

      expect(c.edits['dateOfBirth']).toBe('1980-01-01');
      expect(c.edits['city']).toBe('Encino');
      expect(c.edits['applicantAttorneyEmail']).toBe('grace@example.test');
      expect(c.edits['appointmentInsuranceName']).toBe('Statewide Mutual');
    });

    it('never seeds the social security number', () => {
      /**
       * The read DTO masks it, so echoing it back would either display a mask as if it were
       * the value or round-trip the mask into storage. A REMOVAL-shaped guarantee: the
       * patient fixture HAS the other fields populated, so a blank here is the override
       * rather than an empty source.
       */
      const c = create();
      c.ngOnInit();
      withFlags(c, ['socialSecurityNumber', 'city']);

      c.seedEdits();

      expect(c.edits['socialSecurityNumber']).toBe('');
      expect(c.edits['city']).toBe('Encino');
    });

    it('seeds the defense firm from the nested nav property', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['defenseAttorneyFirmName']);
      c.seedEdits();
      expect(c.edits['defenseAttorneyFirmName']).toBe('Turing & Co');
    });

    it('records an edit and marks the field addressed', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city']);

      c.onEdit('city', 'Van Nuys');

      expect(c.edits['city']).toBe('Van Nuys');
      expect(c.isFixed('city')).toBeTrue();
    });

    it('classifies the special field kinds', () => {
      const c = create();
      c.ngOnInit();
      expect(c.isLanguage('appointmentLanguageId')).toBeTrue();
      expect(c.isState('stateId')).toBeTrue();
      expect(c.isInlineEditable('documents')).toBeFalse();
      expect(c.isLanguage('city')).toBeFalse();
    });

    it('acknowledges a document replacement', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['documents']);

      c.ackDocumentReplaced();

      expect(c.isFixed('documents')).toBeTrue();
      expect(toaster.success).toHaveBeenCalled();
    });

    it('refuses to acknowledge claim information with no rows', () => {
      // Marking it done with an empty set would send the request back still missing the
      // very thing the clinic asked for.
      const c = create();
      c.ngOnInit();
      withFlags(c, ['claimInformation']);
      c.injuryDrafts = [];

      c.ackClaimInformationUpdated();

      expect(c.isFixed('claimInformation')).toBeFalse();
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('acknowledges claim information once a row exists', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['claimInformation']);
      c.injuryDrafts = [{ bodyPartsSummary: 'Left shoulder' }];

      c.ackClaimInformationUpdated();

      expect(c.isFixed('claimInformation')).toBeTrue();
      expect(toaster.success).toHaveBeenCalled();
    });

    it('tracks progress across the flagged set', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city', 'dateOfBirth', 'documents']);
      expect(c.fixedCount).toBe(0);
      expect(c.progressPct).toBe(0);
      expect(c.canResubmit).toBeFalse();

      c.onEdit('city', 'Van Nuys');
      c.onEdit('dateOfBirth', '1980-02-02');

      expect(c.fixedCount).toBe(2);
      expect(c.progressPct).toBe(67);
      expect(c.canResubmit).toBeFalse();
    });

    it('allows resubmission only once EVERY flagged item is addressed', () => {
      /**
       * The gate the whole flow exists for. Partial progress must not unlock the button,
       * or the request returns to the clinic still incomplete and the round trip repeats.
       */
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city', 'documents']);
      c.onEdit('city', 'Van Nuys');
      expect(c.canResubmit).toBeFalse();

      c.ackDocumentReplaced();

      expect(c.canResubmit).toBeTrue();
      expect(c.progressPct).toBe(100);
    });

    it('reports zero percent rather than dividing by zero with nothing flagged', () => {
      const c = create();
      c.ngOnInit();
      expect(c.progressPct).toBe(0);
    });

    it('labels a field and surfaces the staff hint', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['dateOfBirth'], { dateOfBirth: 'Please confirm the year.' });
      expect(c.fieldLabel('dateOfBirth')).toBeTruthy();
      expect(c.hintFor('dateOfBirth')).toBe('Please confirm the year.');
      expect(c.hintFor('city')).toBe('');
    });

    it('uses a date input only for the date of birth', () => {
      const c = create();
      c.ngOnInit();
      expect(c.inputType('dateOfBirth')).toBe('date');
      expect(c.inputType('city')).toBe('text');
    });

    it('loads the history rounds, and hides the section when they fail', () => {
      const c = create();
      getHistory.and.returnValue(of([{ id: 'r1' }]));
      c.ngOnInit();
      expect(c.historyRounds.length).toBe(1);

      TestBed.resetTestingModule();
      const failed = create();
      getHistory.and.returnValue(throwError(() => ({ status: 403 })));
      failed.ngOnInit();
      expect(failed.historyRounds).toEqual([]);
    });

    it('does not load history without a route id', () => {
      const c = create({ id: null });
      c.ngOnInit();
      expect(getHistory).not.toHaveBeenCalled();
    });
  });

  describe('the resubmit confirmation', () => {
    it('opens and closes', () => {
      const c = create();
      c.openResubmitConfirm();
      expect(c.resubmitConfirmVisible).toBeTrue();
      c.cancelResubmit();
      expect(c.resubmitConfirmVisible).toBeFalse();
    });

    it('closes on Escape', () => {
      // The scrim is a div and not focusable, so a template-bound keydown would never fire.
      const c = create();
      c.openResubmitConfirm();

      c.onResubmitEscapeKey();

      expect(c.resubmitConfirmVisible).toBeFalse();
    });

    it('is inert on Escape when the modal is closed', () => {
      const c = create();
      expect(() => c.onResubmitEscapeKey()).not.toThrow();
      expect(c.resubmitConfirmVisible).toBeFalse();
    });

    it('does nothing without a route id', async () => {
      // Returns BEFORE the reload -- see the file header for why that matters.
      const c = create({ id: null });
      await c.confirmResubmit();
      expect(c.isResubmitting).toBeFalse();
    });

    it('refuses a second resubmit while one is in flight', async () => {
      // Also returns before the reload.
      const c = create();
      c.ngOnInit();
      c.isResubmitting = true;
      restRequest.calls.reset();

      await c.confirmResubmit();

      expect(restRequest.calls.allArgs().some(([r]) => r.method === 'POST')).toBeFalse();
    });

    it('releases the flag and does NOT resubmit when saving the corrections fails', async () => {
      /**
       * The corrections POST fails, so `saveCorrections` returns false and confirmResubmit
       * returns BEFORE the resubmit call and before the reload. This is the branch that
       * makes the whole method testable at all.
       */
      const c = create({
        rest: { 'info-requests/corrections': throwError(() => ({ status: 403 })) },
      });
      c.ngOnInit();
      withFlags(c, ['city']);
      c.onEdit('city', 'Van Nuys');

      await c.confirmResubmit();

      expect(c.isResubmitting).toBeFalse();
      expect(
        restRequest.calls.allArgs().some(([r]) => r.url.includes('info-requests/resubmit')),
      ).toBeFalse();
    });
  });

  describe('saving corrections', () => {
    it('does nothing without a route id', async () => {
      const c = create({ id: null });
      await expectAsync(c.saveCorrections()).toBeResolvedTo(false);
    });

    it('posts the flagged corrections', async () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city', 'dateOfBirth']);
      c.onEdit('city', 'Van Nuys');
      c.onEdit('dateOfBirth', '1980-02-02');

      await c.saveCorrections();

      const post = restRequest.calls
        .allArgs()
        .map(([r]) => r)
        .find((r: any) => r.url.includes('info-requests/corrections'));
      expect(post).toBeDefined();
      expect(post.body.corrections.city).toBe('Van Nuys');
    });

    it('reports success with nothing to persist', async () => {
      // A document-only correction has no scalar map and no injury set; the upload already
      // happened in the Documents section, so there is genuinely nothing to POST.
      const c = create();
      c.ngOnInit();
      withFlags(c, ['documents']);
      restRequest.calls.reset();

      await expectAsync(c.saveCorrections()).toBeResolvedTo(true);
      expect(
        restRequest.calls.allArgs().some(([r]) => r.url.includes('info-requests/corrections')),
      ).toBeFalse();
    });

    it('includes the injury set only when claim information was flagged', async () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['claimInformation']);
      c.injuryDrafts = [{ bodyPartsSummary: 'Left shoulder' }];

      await c.saveCorrections();

      const post = restRequest.calls
        .allArgs()
        .map(([r]) => r)
        .find((r: any) => r.url.includes('info-requests/corrections'));
      expect(post.body.injuryDetails).toBeDefined();
    });

    it('omits the injury set when claim information was not flagged', async () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city']);
      c.onEdit('city', 'Van Nuys');

      await c.saveCorrections();

      const post = restRequest.calls
        .allArgs()
        .map(([r]) => r)
        .find((r: any) => r.url.includes('info-requests/corrections'));
      expect(post.body.injuryDetails).toBeUndefined();
    });

    it('returns false when the POST fails', async () => {
      const c = create({
        rest: { 'info-requests/corrections': throwError(() => ({ status: 500 })) },
      });
      c.ngOnInit();
      withFlags(c, ['city']);
      c.onEdit('city', 'Van Nuys');

      await expectAsync(c.saveCorrections()).toBeResolvedTo(false);
    });

    it('confirms a save-for-later', async () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city']);
      c.onEdit('city', 'Van Nuys');

      await c.saveLater();

      expect(toaster.success).toHaveBeenCalled();
    });

    it('says nothing when the save-for-later fails', async () => {
      const c = create({
        rest: { 'info-requests/corrections': throwError(() => ({ status: 500 })) },
      });
      c.ngOnInit();
      withFlags(c, ['city']);
      c.onEdit('city', 'Van Nuys');

      await c.saveLater();

      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('does not save-for-later while resubmitting', async () => {
      const c = create();
      c.ngOnInit();
      c.isResubmitting = true;
      restRequest.calls.reset();

      await c.saveLater();

      expect(
        restRequest.calls.allArgs().some(([r]) => r.url.includes('info-requests/corrections')),
      ).toBeFalse();
    });
  });

  describe('claim-information prefill', () => {
    it('does not fetch when claim information is not flagged', () => {
      const c = create();
      c.ngOnInit();
      withFlags(c, ['city']);
      restRequest.calls.reset();

      c.loadInjuryDraftsIfFlagged();

      expect(
        restRequest.calls.allArgs().some(([r]) => r.url.includes('injury-details')),
      ).toBeFalse();
    });

    it('prefills the editor from the corrections read', () => {
      const c = create({
        rest: { 'info-requests/injury-details': of([{ claimNumber: 'WC-00042' }]) },
      });
      c.ngOnInit();
      withFlags(c, ['claimInformation']);

      c.loadInjuryDraftsIfFlagged();

      expect(c.injuryDrafts.length).toBe(1);
    });

    it('starts from an empty set when the prefill fails', () => {
      // Best-effort: the requester can add rows from scratch.
      const c = create({
        rest: { 'info-requests/injury-details': throwError(() => ({ status: 403 })) },
      });
      c.ngOnInit();
      withFlags(c, ['claimInformation']);

      c.loadInjuryDraftsIfFlagged();

      expect(c.injuryDrafts).toEqual([]);
    });
  });
});
