import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Injector } from '@angular/core';
import { of } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentWizardComponent } from './appointment-wizard.component';
import { AppointmentService } from '../../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../../proxy/custom-fields-controllers/custom-fields.service';
import { AppointmentDraftService } from '../../proxy/appointment-drafts/appointment-draft.service';
import { MyAttorneyProfileService } from '../../proxy/my-attorney-profiles/my-attorney-profile.service';
import { AddressValidationProvider } from '../../shared/address/address-validation.provider';

/**
 * #628 lifted the attorney-question guard out of validateCurrentStep for
 * typescript:S3776. The method had no coverage, so the extraction would have
 * gone in on inspection alone -- and it guards a rule that exists precisely
 * because a booker once walked past it.
 *
 * Item 5 (2026-08-18): the attorney question has no default answer. Unanswered
 * must block Continue, because a null there means neither yes nor no and would
 * be submitted as such. Where the viewer IS that attorney type the question is
 * never rendered, so the guard has to assert the answer instead of demanding
 * one -- otherwise those bookers are stuck on a question they cannot see.
 *
 * Both halves are pinned below. They are the reason the extraction is safe.
 */
describe('AppointmentWizardComponent step validation (#628)', () => {
  interface Probe {
    form: FormGroup;
    attorneyAnswerMissing: boolean;
    attorneyAnswerIsValid(key: 'applicant' | 'defense'): boolean;
    current: number;
    validateCurrentStep(): boolean;
  }

  /** Index into the wizard's STEPS array. */
  const STEP = { applicant: 2, defense: 3 } as const;

  function create(roles: string[] = []): Probe {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        Injector,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null } },
            queryParamMap: of({ get: () => null }),
          },
        },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) => (k === 'currentUser' ? { roles } : null),
            getAll: () => ({}),
            getDeep: () => null,
          },
        },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: AppointmentService, useValue: {} },
        { provide: AppointmentApprovalService, useValue: {} },
        { provide: CustomFieldsService, useValue: { getList: () => of({ items: [] }) } },
        { provide: AppointmentDraftService, useValue: { get: () => of(null) } },
        { provide: MyAttorneyProfileService, useValue: { get: () => of(null) } },
        { provide: AddressValidationProvider, useValue: { autocomplete: () => of([]) } },
        { provide: ConfirmationService, useValue: { warn: () => of(null) } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    return TestBed.runInInjectionContext(
      () => new AppointmentWizardComponent(),
    ) as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  for (const [key, control] of [
    ['applicant', 'applicantAttorneyEnabled'],
    ['defense', 'defenseAttorneyEnabled'],
  ] as Array<['applicant' | 'defense', string]>) {
    describe(`${key} attorney`, () => {
      it('blocks Continue while the question is unanswered', () => {
        const c = create();
        c.form.get(control)!.setValue(null);

        expect(c.attorneyAnswerIsValid(key)).toBeFalse();
        expect(c.attorneyAnswerMissing).toBeTrue();
      });

      it('allows Continue once the booker answers yes', () => {
        const c = create();
        c.form.get(control)!.setValue(true);

        expect(c.attorneyAnswerIsValid(key)).toBeTrue();
        expect(c.attorneyAnswerMissing).toBeFalse();
      });

      /** "No" is an answer. The rule is that the question was addressed. */
      it('allows Continue once the booker answers no', () => {
        const c = create();
        c.form.get(control)!.setValue(false);

        expect(c.attorneyAnswerIsValid(key)).toBeTrue();
        expect(c.attorneyAnswerMissing).toBeFalse();
      });

      /**
       * The viewer IS this attorney type, so the question is never rendered.
       * Demanding an answer would strand them; the guard asserts the value the
       * hidden control would have carried.
       */
      it('asserts the answer instead of demanding one when the viewer is that attorney', () => {
        const c = create([key === 'applicant' ? 'Applicant Attorney' : 'Defense Attorney']);
        c.form.get(control)!.setValue(null);

        expect(c.attorneyAnswerIsValid(key)).toBeTrue();
        expect(c.form.get(control)!.value).toBeTrue();
        expect(c.attorneyAnswerMissing).toBeFalse();
      });

      /** An IT admin booking on someone's behalf is not that attorney. */
      it('still asks an IT admin, even one holding the attorney role', () => {
        const c = create([
          key === 'applicant' ? 'Applicant Attorney' : 'Defense Attorney',
          'IT Admin',
        ]);
        c.form.get(control)!.setValue(null);

        expect(c.attorneyAnswerIsValid(key)).toBeFalse();
        expect(c.attorneyAnswerMissing).toBeTrue();
      });
    });
  }
  /**
   * The call site, so the extraction is covered end to end rather than only the
   * lifted method.
   *
   * These assert attorneyAnswerMissing rather than the returned boolean, except
   * where the boolean is the point. The applicant step also carries required
   * name and firm fields, so a bare form fails validation for reasons that have
   * nothing to do with this guard -- asserting `true` there would mean filling
   * the whole step and would pass or fail for the wrong reasons.
   */
  describe('validateCurrentStep wiring', () => {
    it('refuses the applicant step while the question is unanswered', () => {
      const c = create();
      c.current = STEP.applicant;
      c.form.get('applicantAttorneyEnabled')!.setValue(null);

      expect(c.validateCurrentStep()).toBeFalse();
      expect(c.attorneyAnswerMissing).toBeTrue();
    });

    it('stops flagging the question once it is answered', () => {
      const c = create();
      c.current = STEP.applicant;
      c.form.get('applicantAttorneyEnabled')!.setValue(false);

      c.validateCurrentStep();

      expect(c.attorneyAnswerMissing).toBeFalse();
    });

    /** The defense control being null must not block the applicant step. */
    it('does not consult the other attorney question', () => {
      const c = create();
      c.current = STEP.applicant;
      c.form.get('applicantAttorneyEnabled')!.setValue(true);
      c.form.get('defenseAttorneyEnabled')!.setValue(null);

      c.validateCurrentStep();

      expect(c.attorneyAnswerMissing).toBeFalse();
    });
  });
});
