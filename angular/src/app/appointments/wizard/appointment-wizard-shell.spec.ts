import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { Injector } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentWizardComponent } from './appointment-wizard.component';
import { AppointmentService } from '../../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../../proxy/custom-fields-controllers/custom-fields.service';
import { AppointmentDraftService } from '../../proxy/appointment-drafts/appointment-draft.service';
import { MyAttorneyProfileService } from '../../proxy/my-attorney-profiles/my-attorney-profile.service';
import { AddressValidationProvider } from '../../shared/address/address-validation.provider';
import { PREFILL_SECTIONS } from '../shared/prefill-sections';

/**
 * The wizard SHELL -- the stepper, the review-step mirror, the draft lifecycle and the
 * leave guard.
 *
 * <p>`validate-current-step.spec.ts` pins the attorney-question guard and
 * `wizard-copy.util.spec.ts` pins the wording. Neither reaches the 145 lines around them:
 * how a step is labelled done or errored, how a GUID id becomes a display name on the
 * review page, when a draft is written or discarded, and what happens when a booker walks
 * away from a half-filled request.</p>
 *
 * <p>The copy getters are asserted by DELEGATION rather than by string, deliberately. The
 * exact wording already has a test that owns it; repeating the strings here would mean
 * two files to update for one copy change, and would not test the thing this file is for
 * -- which is that the wizard passes the right audience and mode into the resolver.</p>
 *
 * <p>The class is constructed directly in an injection context, as its sibling specs do,
 * so nothing is change-detected and only the methods a test calls run.</p>
 *
 * <p>Names, offices and confirmation numbers below are synthetic.</p>
 */
describe('AppointmentWizardComponent shell', () => {
  const DRAFT_KEY = 'ra-wizard-draft';

  /** Index into the wizard's STEPS array, which is fixed at nine. */
  const STEP = {
    schedule: 0,
    patient: 1,
    applicant: 2,
    defense: 3,
    insurance: 4,
    examiner: 5,
    claim: 6,
    docs: 7,
    review: 8,
  } as const;

  let draftService: {
    getMine: jasmine.Spy;
    upsert: jasmine.Spy;
    discardMine: jasmine.Spy;
  };
  let confirmation: { warn: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy; navigate: jasmine.Spy };
  let myAttorneyProfile: { get: jasmine.Spy };
  let restCalls: string[];

  interface Probe {
    [key: string]: any;
  }

  /** Shape-correct responses per endpoint -- several callers index `.items` directly. */
  function restFor(url: string): Observable<unknown> {
    if (url.includes('appointment-type-field-configs') || url.includes('options-by-type')) {
      return of([]);
    }
    if (url.includes('by-appointment')) {
      return of([]);
    }
    if (url.includes('external-users/me')) {
      return of({ firmName: 'Hopper & Co' });
    }
    if (url.includes('wcab-office-lookup')) {
      return of({ items: [{ id: 'wcab-1', displayName: 'Van Nuys' }] });
    }
    if (url.includes('state-lookup')) {
      return of({ items: [{ id: 'state-1', displayName: 'California' }] });
    }
    if (url.includes('appointment-language-lookup')) {
      return of({ items: [{ id: 'lang-1', displayName: 'Spanish' }] });
    }
    if (url.includes('appointment-type-lookup')) {
      return of({ items: [{ id: 'type-1', displayName: 'AME' }] });
    }
    if (url.includes('location-lookup')) {
      return of({ items: [{ id: 'loc-1', displayName: 'Encino' }] });
    }
    return of({ items: [], totalCount: 0 });
  }

  function create(
    options: {
      roles?: string[];
      queryParams?: Record<string, string>;
      draft?: unknown;
      draftThrows?: boolean;
      confirmStatus?: unknown;
      attorneyProfile?: unknown;
      attorneyProfileThrows?: boolean;
    } = {},
  ): Probe {
    const params = options.queryParams ?? {};
    restCalls = [];
    draftService = {
      getMine: jasmine
        .createSpy('getMine')
        .and.returnValue(
          options.draftThrows ? throwError(() => ({ status: 500 })) : of(options.draft ?? null),
        ),
      upsert: jasmine.createSpy('upsert').and.returnValue(of({})),
      discardMine: jasmine.createSpy('discardMine').and.returnValue(of({})),
    };
    confirmation = {
      warn: jasmine
        .createSpy('warn')
        .and.returnValue(of(options.confirmStatus ?? Confirmation.Status.confirm)),
    };
    router = {
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
      navigate: jasmine.createSpy('navigate'),
    };
    myAttorneyProfile = {
      get: jasmine
        .createSpy('get')
        .and.returnValue(
          options.attorneyProfileThrows
            ? throwError(() => ({ status: 404 }))
            : of(options.attorneyProfile ?? null),
        ),
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        Injector,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null } },
            queryParamMap: of({ get: (key: string) => params[key] ?? null }),
          },
        },
        { provide: Router, useValue: router },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) =>
              key === 'currentUser'
                ? {
                    id: 'user-1',
                    name: 'Ada',
                    surname: 'Lovelace',
                    email: 'ada@example.test',
                    userName: 'ada',
                    roles: options.roles ?? [],
                  }
                : null,
            getAll: () => ({}),
            getDeep: () => null,
          },
        },
        {
          provide: RestService,
          useValue: {
            request: (req: { url: string }) => {
              restCalls.push(req.url);
              return restFor(req.url);
            },
          },
        },
        { provide: AppointmentService, useValue: { submit: () => of({ appointmentId: 'a1' }) } },
        { provide: AppointmentApprovalService, useValue: { approveAppointment: () => of(null) } },
        {
          provide: CustomFieldsService,
          useValue: { getList: () => of({ items: [] }), getActiveForAppointmentType: () => of([]) },
        },
        { provide: AppointmentDraftService, useValue: draftService },
        { provide: MyAttorneyProfileService, useValue: myAttorneyProfile },
        {
          provide: AddressValidationProvider,
          useValue: { autocomplete: () => of([]), validate: () => of({ status: 'ok' }) },
        },
        { provide: ConfirmationService, useValue: confirmation },
        {
          provide: ToasterService,
          useValue: { success: () => undefined, error: () => undefined, warn: () => undefined },
        },
      ],
    });

    return TestBed.runInInjectionContext(
      () => new AppointmentWizardComponent(),
    ) as unknown as Probe;
  }

  beforeEach(() => localStorage.removeItem(DRAFT_KEY));

  afterEach(() => {
    localStorage.removeItem(DRAFT_KEY);
    TestBed.resetTestingModule();
  });

  describe('step geometry', () => {
    it('exposes nine steps ending at Review', () => {
      const c = create();
      expect(c.steps.length).toBe(9);
      expect(c.steps[STEP.review].key).toBe('review');
    });

    it('starts on the first step', () => {
      const c = create();
      expect(c.current).toBe(0);
      expect(c.currentStep.key).toBe('schedule');
    });

    it('knows when it is on the last step', () => {
      const c = create();
      expect(c.isLastStep).toBeFalse();
      c.current = STEP.review;
      expect(c.isLastStep).toBeTrue();
    });
  });

  describe('stepState', () => {
    it('marks the step the booker is on', () => {
      const c = create();
      c.current = STEP.patient;
      expect(c.stepState(STEP.patient)).toBe('current');
    });

    it('marks a step already passed as done', () => {
      const c = create();
      c.current = STEP.applicant;
      c.furthest = STEP.applicant;
      expect(c.stepState(STEP.schedule)).toBe('done');
    });

    it('marks a step never reached as disabled', () => {
      const c = create();
      c.current = STEP.schedule;
      c.furthest = STEP.schedule;
      expect(c.stepState(STEP.review)).toBe('disabled');
    });

    it('marks a step whose Continue was refused as errored', () => {
      const c = create();
      c.current = STEP.applicant;
      c.furthest = STEP.applicant;
      c.erroredSteps.add(STEP.patient);
      expect(c.stepState(STEP.patient)).toBe('error');
    });

    it('flags Schedule when a source-anchored booking has no source loaded', () => {
      /**
       * F-M05: a reval cannot be submitted without a source, and the booker who walked
       * past the lookup would otherwise discover that only at a Submit that does nothing.
       * Flagging the step is what makes the dead end visible in the stepper.
       */
      const c = create({ queryParams: { type: '2' } });
      c.current = STEP.patient;
      expect(c.bookingMode).toBe('reval');
      expect(c.isSourceLoadRequired).toBeTrue();
      expect(c.stepState(STEP.schedule)).toBe('error');
    });

    it('stops flagging Schedule once the source is loaded', () => {
      const c = create({ queryParams: { type: '2' } });
      c.current = STEP.patient;
      c.furthest = STEP.patient;
      c.sourceConfirmationNumber = 'C0001';
      expect(c.stepState(STEP.schedule)).toBe('done');
    });

    it('does not flag Schedule for a plain new booking', () => {
      const c = create();
      c.current = STEP.patient;
      c.furthest = STEP.patient;
      expect(c.stepState(STEP.schedule)).toBe('done');
    });
  });

  describe('navigation', () => {
    it('jumps back to a step already reached', () => {
      const c = create();
      c.furthest = STEP.claim;
      c.current = STEP.claim;
      c.jumpTo(STEP.patient);
      expect(c.current).toBe(STEP.patient);
    });

    it('refuses to jump forward past the furthest step reached', () => {
      // The stepper is a progress indicator, not a free-navigation control: skipping
      // ahead would bypass the per-step validation the Continue button applies.
      const c = create();
      c.furthest = STEP.patient;
      c.current = STEP.schedule;
      c.jumpTo(STEP.review);
      expect(c.current).toBe(STEP.schedule);
    });

    it('clears the error summary when jumping', () => {
      /**
       * A REMOVAL, so the summary is seeded first. Left behind, the previous step's
       * field list would appear above a step those fields do not belong to.
       */
      const c = create();
      c.furthest = STEP.claim;
      c.current = STEP.claim;
      c.stepErrorSummary = [{ control: 'firstName', label: 'First name' }];
      c.jumpTo(STEP.patient);
      expect(c.stepErrorSummary).toEqual([]);
    });

    it('steps back and clears the summary', () => {
      const c = create();
      c.current = STEP.defense;
      c.stepErrorSummary = [{ control: 'firstName', label: 'First name' }];
      c.prevStep();
      expect(c.current).toBe(STEP.applicant);
      expect(c.stepErrorSummary).toEqual([]);
    });

    it('does not step back past the first step', () => {
      const c = create();
      c.current = 0;
      c.prevStep();
      expect(c.current).toBe(0);
    });

    it('advances and records the furthest step on a valid Continue', () => {
      const c = create();
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.current = STEP.schedule;

      c.nextStep();

      expect(c.current).toBe(STEP.patient);
      expect(c.furthest).toBe(STEP.patient);
    });

    it('does not advance past the last step', () => {
      const c = create();
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.current = STEP.review;
      c.furthest = STEP.review;

      c.nextStep();

      expect(c.current).toBe(STEP.review);
    });

    it('keeps the furthest step when the booker moves back then forward', () => {
      // `Math.max` -- without it, stepping back would retract the progress record and
      // re-lock steps the booker had already completed.
      const c = create();
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.current = STEP.claim;
      c.furthest = STEP.claim;
      c.jumpTo(STEP.patient);

      c.nextStep();

      expect(c.furthest).toBe(STEP.claim);
    });

    it('blocks Continue and flags the step when the step is invalid', () => {
      // A bare Schedule step is genuinely invalid (type, location, date and slot are all
      // required), so this uses the real validator rather than a spy.
      const c = create();
      c.current = STEP.schedule;

      c.nextStep();

      expect(c.current).toBe(STEP.schedule);
      expect(c.erroredSteps.has(STEP.schedule)).toBeTrue();
      expect(c.stepErrorSummary.length).toBeGreaterThan(0);
    });

    it('clears the step flag once the booker fixes it', () => {
      /**
       * A REMOVAL that needs the flag seeded: `erroredSteps.delete` is invisible against
       * an empty set, and a sticky flag would leave a red step the booker cannot clear.
       */
      const c = create();
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.current = STEP.schedule;
      c.erroredSteps.add(STEP.schedule);

      c.nextStep();

      expect(c.erroredSteps.has(STEP.schedule)).toBeFalse();
    });

    it('marks invalid controls touched so the fields redden', () => {
      const c = create();
      c.current = STEP.schedule;

      c.nextStep();

      expect(c.form.get('appointmentTypeId')?.touched).toBeTrue();
    });

    it('checkpoints the draft on a successful Continue', () => {
      const c = create();
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.draftEnabled = true;

      c.nextStep();

      expect(draftService.upsert).toHaveBeenCalled();
    });
  });

  describe('the "what has changed?" picker', () => {
    it('opens on arrival at the Patient step of a prefilled booking', () => {
      /**
       * Item 5: the question is asked on ARRIVAL at the first step the answer governs,
       * not when the source loads -- two of the three entry paths auto-load during
       * construction, which would put a modal over a half-rendered wizard.
       */
      const c = create({ queryParams: { type: '2' } });
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.sourceConfirmationNumber = 'C0001';
      c.current = STEP.schedule;

      c.nextStep();

      expect(c.currentStep.key).toBe('patient');
      expect(c.prefillPickerVisible).toBeTrue();
    });

    it('does not open for a plain new booking', () => {
      const c = create();
      spyOn<any>(c, 'validateCurrentStep').and.returnValue(true);
      c.current = STEP.schedule;

      c.nextStep();

      expect(c.prefillPickerVisible).toBeFalse();
    });

    it('knows which steps the picker governs', () => {
      const c = create();
      for (const step of [
        STEP.patient,
        STEP.applicant,
        STEP.defense,
        STEP.insurance,
        STEP.examiner,
      ]) {
        c.current = step;
        expect(c.stepUsesPrefillSection).withContext(c.currentStep.key).toBeTrue();
      }
      for (const step of [STEP.schedule, STEP.claim, STEP.docs, STEP.review]) {
        c.current = step;
        expect(c.stepUsesPrefillSection).withContext(c.currentStep.key).toBeFalse();
      }
    });

    it('applies an answer straight away when no edited section is affected', () => {
      const c = create({ queryParams: { type: '2' } });
      const selection: Record<string, boolean> = {};
      PREFILL_SECTIONS.forEach(({ key }) => (selection[key] = false));

      c.onPrefillSelectionConfirmed(selection);

      expect(confirmation.warn).not.toHaveBeenCalled();
      expect(c.prefillSelection).toBe(selection);
      expect(c.prefillPickerVisible).toBeFalse();
    });

    it('warns before discarding a section the booker has already edited', () => {
      /**
       * Marking a section changed CLEARS it. The fixture therefore dirties a control the
       * section owns -- against a clean form the branch is unreachable and this test
       * would pass with the whole confirmation deleted.
       */
      const c = create({ queryParams: { type: '2' } });
      const first = PREFILL_SECTIONS[0];
      const selection: Record<string, boolean> = {};
      PREFILL_SECTIONS.forEach(({ key }) => (selection[key] = false));
      selection[first.key] = true;
      spyOn<any>(c, 'prefillSectionIsDirty').and.callFake((key: string) => key === first.key);

      c.onPrefillSelectionConfirmed(selection);

      expect(confirmation.warn).toHaveBeenCalled();
      expect(confirmation.warn.calls.mostRecent().args[0]).toContain(first.label);
    });

    it('applies the answer when the booker confirms the loss', () => {
      const c = create({ queryParams: { type: '2' }, confirmStatus: Confirmation.Status.confirm });
      const first = PREFILL_SECTIONS[0];
      const selection: Record<string, boolean> = {};
      PREFILL_SECTIONS.forEach(({ key }) => (selection[key] = false));
      selection[first.key] = true;
      spyOn<any>(c, 'prefillSectionIsDirty').and.returnValue(true);
      const apply = spyOn<any>(c, 'applyPrefillSelection');

      c.onPrefillSelectionConfirmed(selection);

      expect(apply).toHaveBeenCalledWith(selection);
    });

    it('keeps the data AND the picker open when the booker declines', () => {
      /**
       * Declining must not close the dialog: the booker said "keep my changes", which
       * usually means they want to correct the answer rather than abandon the question.
       */
      const c = create({ queryParams: { type: '2' }, confirmStatus: Confirmation.Status.reject });
      const first = PREFILL_SECTIONS[0];
      const selection: Record<string, boolean> = {};
      PREFILL_SECTIONS.forEach(({ key }) => (selection[key] = false));
      selection[first.key] = true;
      spyOn<any>(c, 'prefillSectionIsDirty').and.returnValue(true);
      const apply = spyOn<any>(c, 'applyPrefillSelection');

      c.onPrefillSelectionConfirmed(selection);

      expect(apply).not.toHaveBeenCalled();
      expect(c.prefillPickerVisible).toBeTrue();
    });
  });

  describe('review-step display helpers', () => {
    it('resolves the appointment type name from its id', () => {
      const c = create();
      c.typeNames.set('type-1', 'AME');
      c.form.get('appointmentTypeId')?.setValue('type-1');
      expect(c.typeName()).toBe('AME');
    });

    it('shows a dash for an unresolved appointment type', () => {
      // The review must never print a raw GUID at a booker.
      const c = create();
      c.form.get('appointmentTypeId')?.setValue('type-unknown');
      expect(c.typeName()).toBe('-');
    });

    it('resolves the location name from its id', () => {
      const c = create();
      c.locationNames.set('loc-1', 'Encino');
      c.form.get('locationId')?.setValue('loc-1');
      expect(c.locationName()).toBe('Encino');
    });

    it('shows a dash for an unresolved location', () => {
      const c = create();
      expect(c.locationName()).toBe('-');
    });

    it('shows a field value as entered', () => {
      const c = create();
      c.form.get('employerName')?.setValue('Acme Manufacturing');
      expect(c.fieldVal('employerName')).toBe('Acme Manufacturing');
    });

    it('shows a dash for an empty, null or unknown field', () => {
      const c = create();
      c.form.get('employerName')?.setValue('');
      expect(c.fieldVal('employerName')).toBe('-');
      c.form.get('employerName')?.setValue(null);
      expect(c.fieldVal('employerName')).toBe('-');
      expect(c.fieldVal('noSuchControl')).toBe('-');
    });

    it('stringifies a non-string field value', () => {
      const c = create();
      c.form.get('genderId')?.setValue(2);
      expect(c.fieldVal('genderId')).toBe('2');
    });

    it('joins the patient name parts that are present', () => {
      const c = create();
      c.form.patchValue({ firstName: 'Ada', middleName: 'Byron', lastName: 'Lovelace' });
      expect(c.patientFullName()).toBe('Ada Byron Lovelace');
    });

    it('skips a missing middle name rather than doubling the space', () => {
      const c = create();
      c.form.patchValue({ firstName: 'Ada', lastName: 'Lovelace' });
      expect(c.patientFullName()).toBe('Ada Lovelace');
    });

    it('shows a dash when no patient name is entered', () => {
      const c = create();
      expect(c.patientFullName()).toBe('-');
    });

    it('resolves state, language and WCAB names from their ids', () => {
      const c = create();
      c.stateNames.set('state-1', 'California');
      c.languageNames.set('lang-1', 'Spanish');
      c.wcabNames.set('wcab-1', 'Van Nuys');
      expect(c.stateName('state-1')).toBe('California');
      expect(c.languageName('lang-1')).toBe('Spanish');
      expect(c.wcabName('wcab-1')).toBe('Van Nuys');
    });

    it('shows a dash for an unset or unknown id', () => {
      const c = create();
      for (const resolve of [c.stateName, c.languageName, c.wcabName]) {
        expect(resolve.call(c, null)).toBe('-');
        expect(resolve.call(c, undefined)).toBe('-');
        expect(resolve.call(c, 'missing')).toBe('-');
      }
    });

    it('joins an attorney name', () => {
      const c = create();
      c.form.patchValue({
        applicantAttorneyFirstName: 'Grace',
        applicantAttorneyLastName: 'Hopper',
      });
      expect(c.attorneyName('applicantAttorney')).toBe('Grace Hopper');
    });

    it('reads each attorney section separately', () => {
      // One shared prefix would make the defense row mirror the applicant's.
      const c = create();
      c.form.patchValue({
        applicantAttorneyFirstName: 'Grace',
        defenseAttorneyFirstName: 'Alan',
        defenseAttorneyLastName: 'Turing',
      });
      expect(c.attorneyName('applicantAttorney')).toBe('Grace');
      expect(c.attorneyName('defenseAttorney')).toBe('Alan Turing');
    });

    it('shows a dash for an empty attorney section', () => {
      const c = create();
      expect(c.attorneyName('defenseAttorney')).toBe('-');
    });
  });

  describe('copy getters delegate audience and mode', () => {
    it('produces an eyebrow, title and subtitle', () => {
      const c = create();
      expect(c.eyebrow).toBeTruthy();
      expect(c.wizardTitle).toBeTruthy();
      expect(c.wizardSubtitle).toBeTruthy();
    });

    it('titles a re-evaluation differently from a new booking', () => {
      // The mode reaches the resolver. Both flows sharing one title would tell a booker
      // they are starting a fresh request when they are re-evaluating.
      const plain = create().wizardTitle;
      TestBed.resetTestingModule();
      const reval = create({ queryParams: { type: '2' } }).wizardTitle;
      expect(reval).not.toBe(plain);
    });

    it('gives staff a different submit note from an external booker', () => {
      // Staff can edit after submitting; the patient-voiced "contact staff" warning
      // would be wrong for them.
      const external = create({ roles: ['Patient'] }).reviewSubmitNote;
      TestBed.resetTestingModule();
      const internal = create({ roles: ['Staff Supervisor'] }).reviewSubmitNote;
      expect(internal).not.toBe(external);
    });

    it('carries a source-lookup banner for a source-anchored mode', () => {
      const c = create({ queryParams: { type: '2' } });
      expect(c.sourceLookupBanner).toBeTruthy();
    });

    it('reads the signed-in email for the navbar', () => {
      const c = create();
      expect(c.navUserEmail).toBe('ada@example.test');
    });
  });

  describe('the header back control', () => {
    it('sends staff to the appointments list', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.backLabel).toBe('Back to appointments');
      c.backOut();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments');
    });

    it('sends an external booker home', () => {
      // External users have no sidebar to fall back on, so landing them on the internal
      // list would strand them.
      const c = create({ roles: ['Applicant Attorney'] });
      expect(c.backLabel).toBe('Back to home');
      c.backOut();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
    });

    it('opens the query modal', () => {
      const c = create();
      c.openQuery();
      expect(c.submitQueryVisible).toBeTrue();
    });

    it('leaves the wizard for the documents home', () => {
      const c = create();
      c.openDocuments();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
    });
  });

  describe('post-booking navigation', () => {
    it('lands staff on the appointments list', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.navigateAfterBooking();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments');
    });

    it('discards the draft once the booking succeeded', () => {
      // The draft is consumed: leaving it would offer the booker a resume for a request
      // they have already submitted.
      const c = create();
      c.draftEnabled = true;
      c.navigateAfterBooking();
      expect(draftService.discardMine).toHaveBeenCalled();
      expect(c.submitted).toBeTrue();
    });

    it('does not discard a draft that was never enabled', () => {
      const c = create();
      c.draftEnabled = false;
      c.navigateAfterBooking();
      expect(draftService.discardMine).not.toHaveBeenCalled();
    });
  });

  describe('drafts', () => {
    it('is enabled only for a fresh new booking', () => {
      const c = create();
      c.ngOnInit();
      expect(c.draftEnabled).toBeTrue();
    });

    it('is disabled for a re-evaluation, whose prefill would collide', () => {
      const c = create({ queryParams: { type: '2' } });
      c.ngOnInit();
      expect(c.draftEnabled).toBeFalse();
      expect(draftService.getMine).not.toHaveBeenCalled();
    });

    it('offers to resume a stored server draft', () => {
      const c = create({ draft: { payloadJson: '{"v":{},"step":0}', label: 'AME' } });
      c.ngOnInit();
      expect(confirmation.warn).toHaveBeenCalled();
      expect(confirmation.warn.calls.mostRecent().args[0]).toContain('AME');
    });

    it('restores the form and step when the booker resumes', () => {
      const c = create({
        draft: { payloadJson: '{"v":{"employerName":"Acme Manufacturing"},"step":3}' },
        confirmStatus: Confirmation.Status.confirm,
      });

      c.ngOnInit();

      expect(c.form.get('employerName')?.value).toBe('Acme Manufacturing');
      expect(c.current).toBe(3);
      expect(c.furthest).toBe(3);
      expect(c.draftState).toBe('saved');
    });

    it('discards the stored draft when the booker starts fresh', () => {
      const c = create({
        draft: { payloadJson: '{"v":{"employerName":"Acme"},"step":3}' },
        confirmStatus: Confirmation.Status.reject,
      });

      c.ngOnInit();

      expect(draftService.discardMine).toHaveBeenCalled();
      expect(c.form.get('employerName')?.value).toBeNull();
      expect(c.current).toBe(0);
    });

    it('falls back to the local cache when there is no server draft', () => {
      localStorage.setItem(DRAFT_KEY, '{"v":{"employerName":"Acme Manufacturing"},"step":2}');
      const c = create({ draft: null });

      c.ngOnInit();

      expect(c.form.get('employerName')?.value).toBe('Acme Manufacturing');
      expect(c.current).toBe(2);
    });

    it('falls back to the local cache when the server lookup fails', () => {
      // A draft service outage must not cost the booker the work already typed.
      localStorage.setItem(DRAFT_KEY, '{"v":{"employerName":"Acme Manufacturing"},"step":1}');
      const c = create({ draftThrows: true });

      c.ngOnInit();

      expect(c.form.get('employerName')?.value).toBe('Acme Manufacturing');
    });

    it('ignores a corrupt local cache rather than failing to open', () => {
      localStorage.setItem(DRAFT_KEY, 'not json at all');
      const c = create({ draft: null });
      expect(() => c.ngOnInit()).not.toThrow();
      expect(c.current).toBe(0);
    });

    it('ignores a corrupt server payload', () => {
      const c = create({ draft: { payloadJson: '{oops' } });
      expect(() => c.ngOnInit()).not.toThrow();
    });

    it('ignores an empty server payload', () => {
      const c = create();
      c.applyDraftPayload(undefined);
      expect(c.current).toBe(0);
    });

    it('writes the local cache on autosave', () => {
      const c = create();
      c.current = 4;
      c.form.get('employerName')?.setValue('Acme Manufacturing');

      c.saveDraft();

      const cached = JSON.parse(localStorage.getItem(DRAFT_KEY) ?? '{}');
      expect(cached.step).toBe(4);
      expect(cached.v.employerName).toBe('Acme Manufacturing');
    });

    it('checkpoints to the server with a non-PHI label', () => {
      /**
       * The resume label is the appointment TYPE name, never the patient's. It appears in
       * a prompt that can be read over a shoulder, so a name there would be a disclosure.
       */
      const c = create();
      c.draftEnabled = true;
      c.typeNames.set('type-1', 'AME');
      c.form.get('appointmentTypeId')?.setValue('type-1');
      c.current = 2;

      c.persistServerDraft();

      const payload = draftService.upsert.calls.mostRecent().args[0];
      expect(payload.label).toBe('AME');
      expect(payload.currentStep).toBe(2);
      expect(c.draftState).toBe('saved');
    });

    it('sends a null label when the type is not yet chosen', () => {
      const c = create();
      c.draftEnabled = true;
      c.persistServerDraft();
      expect(draftService.upsert.calls.mostRecent().args[0].label).toBeNull();
    });

    it('does not checkpoint when drafts are disabled', () => {
      const c = create();
      c.draftEnabled = false;
      c.persistServerDraft();
      expect(draftService.upsert).not.toHaveBeenCalled();
    });

    it('returns to idle when the checkpoint fails', () => {
      // A "saved" indicator over a failed write is the worst of the three states.
      const c = create();
      draftService.upsert.and.returnValue(throwError(() => ({ status: 500 })));
      c.draftEnabled = true;

      c.persistServerDraft();

      expect(c.draftState).toBe('idle');
    });

    it('clears both stores on discard', () => {
      const c = create();
      localStorage.setItem(DRAFT_KEY, '{"v":{},"step":1}');

      c.discardServerDraft();

      expect(draftService.discardMine).toHaveBeenCalled();
      expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
      expect(c.draftState).toBe('idle');
    });

    it('survives a failing discard', () => {
      const c = create();
      draftService.discardMine.and.returnValue(throwError(() => ({ status: 500 })));
      expect(() => c.discardServerDraft()).not.toThrow();
    });
  });

  describe('the leave guard', () => {
    it('leaves without prompting when nothing was typed', () => {
      const c = create();
      c.draftEnabled = true;
      expect(c.canDeactivate()).toBeTrue();
      expect(c.leavePromptVisible).toBeFalse();
    });

    it('leaves without prompting after a successful submit', () => {
      const c = create();
      c.draftEnabled = true;
      c.submitted = true;
      c.form.markAsDirty();
      expect(c.canDeactivate()).toBeTrue();
    });

    it('leaves without prompting for a reval session', () => {
      const c = create({ queryParams: { type: '2' } });
      c.draftEnabled = false;
      c.form.markAsDirty();
      expect(c.canDeactivate()).toBeTrue();
    });

    it('prompts when a dirty new booking is abandoned', () => {
      const c = create();
      c.draftEnabled = true;
      c.form.markAsDirty();

      const result = c.canDeactivate();

      expect(c.leavePromptVisible).toBeTrue();
      expect(result instanceof Promise).toBeTrue();
    });

    it('saves and leaves on Save', async () => {
      const c = create();
      c.draftEnabled = true;
      c.form.markAsDirty();
      const pending = c.canDeactivate() as Promise<boolean>;

      c.onLeaveSave();

      await expectAsync(pending).toBeResolvedTo(true);
      expect(draftService.upsert).toHaveBeenCalled();
      expect(c.leavePromptVisible).toBeFalse();
    });

    it('discards and leaves on Discard', async () => {
      const c = create();
      c.draftEnabled = true;
      c.form.markAsDirty();
      const pending = c.canDeactivate() as Promise<boolean>;

      c.onLeaveDiscard();

      await expectAsync(pending).toBeResolvedTo(true);
      expect(draftService.discardMine).toHaveBeenCalled();
    });

    it('stays put on Stay', async () => {
      const c = create();
      c.draftEnabled = true;
      c.form.markAsDirty();
      const pending = c.canDeactivate() as Promise<boolean>;

      c.onLeaveStay();

      await expectAsync(pending).toBeResolvedTo(false);
      expect(c.leavePromptVisible).toBeFalse();
    });

    it('drops the resolver so a later answer cannot resolve it twice', () => {
      // Resolving a settled promise is harmless, but a retained resolver would keep the
      // previous navigation's continuation alive across a second prompt.
      const c = create();
      c.draftEnabled = true;
      c.form.markAsDirty();
      c.canDeactivate();

      c.onLeaveStay();

      expect(c.leaveResolver).toBeUndefined();
    });
  });

  describe('ngOnInit lookups', () => {
    it('caches the type, location, state and language names for the review step', () => {
      const c = create();
      c.ngOnInit();
      expect(c.typeNames.get('type-1')).toBe('AME');
      expect(c.locationNames.get('loc-1')).toBe('Encino');
      expect(c.stateNames.get('state-1')).toBe('California');
      expect(c.languageNames.get('lang-1')).toBe('Spanish');
    });

    it('fetches the WCAB venue names the parent has no helper for', () => {
      const c = create();
      c.ngOnInit();
      expect(c.wcabNames.get('wcab-1')).toBe('Van Nuys');
      expect(restCalls.some((u) => u.includes('wcab-office-lookup'))).toBeTrue();
    });

    it('resolves the navbar display name from the profile firm', () => {
      const c = create();
      c.ngOnInit();
      expect(c.firmName).toBe('Hopper & Co');
      expect(c.navDisplayName).toBeTruthy();
    });

    it('unsubscribes the autosave stream on destroy', () => {
      const c = create();
      c.ngOnInit();
      c.ngOnDestroy();
      expect(c.draftSub.closed).toBeTrue();
    });
  });

  describe('booker own-attorney prefill', () => {
    it('does not ask for a profile when the booker is not an attorney', () => {
      // Gating on the role avoids a spurious 404 and its console error for patient and
      // claim-examiner bookers, who have no profile to fetch.
      const c = create({ roles: ['Patient'] });
      c.ngOnInit();
      expect(myAttorneyProfile.get).not.toHaveBeenCalled();
    });

    it('fills a blank applicant section from the booker profile', () => {
      const c = create({
        roles: ['Applicant Attorney'],
        attorneyProfile: {
          kind: 'applicant',
          firstName: 'Grace',
          lastName: 'Hopper',
          email: 'grace@example.test',
          firmName: 'Hopper & Co',
        },
      });

      c.ngOnInit();

      expect(c.form.get('applicantAttorneyFirstName')?.value).toBe('Grace');
      expect(c.form.get('applicantAttorneyEmail')?.value).toBe('grace@example.test');
      expect(c.form.get('applicantAttorneyEnabled')?.value).toBeTrue();
    });

    it('fills the defense section for a defense booker', () => {
      const c = create({
        roles: ['Defense Attorney'],
        attorneyProfile: { kind: 'defense', firstName: 'Alan', lastName: 'Turing' },
      });

      c.ngOnInit();

      expect(c.form.get('defenseAttorneyFirstName')?.value).toBe('Alan');
      expect(c.form.get('applicantAttorneyFirstName')?.value).toBeNull();
    });

    it('does not clobber a section the booker has already filled', () => {
      /**
       * A negative guarantee that needs the section POPULATED first. Against a blank
       * form the guard is unreachable, and a resumed draft or a reval prefill -- both of
       * which load their values before this runs -- would be silently overwritten.
       */
      const c = create({
        roles: ['Applicant Attorney'],
        attorneyProfile: { kind: 'applicant', firstName: 'Grace', lastName: 'Hopper' },
      });
      c.form.get('applicantAttorneyFirstName')?.setValue('Ada');

      c.ngOnInit();

      expect(c.form.get('applicantAttorneyFirstName')?.value).toBe('Ada');
    });

    it('does nothing when the profile has no recognised kind', () => {
      const c = create({ roles: ['Applicant Attorney'], attorneyProfile: { kind: 'other' } });
      c.ngOnInit();
      expect(c.form.get('applicantAttorneyFirstName')?.value).toBeNull();
      expect(c.form.get('defenseAttorneyFirstName')?.value).toBeNull();
    });

    it('is a silent no-op when the booker has no profile', () => {
      const c = create({ roles: ['Applicant Attorney'], attorneyProfileThrows: true });
      expect(() => c.ngOnInit()).not.toThrow();
      expect(c.form.get('applicantAttorneyFirstName')?.value).toBeNull();
    });
  });
});
