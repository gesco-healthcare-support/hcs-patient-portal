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
 * #628, the last two findings in this scope. Both were written once, removed
 * from #810 when the changed-lines gate read 52%, and are back now with the
 * tests that were missing:
 *
 *  - typescript:S3358, a nested ternary choosing the attorney prefix inside
 *    prefillBookingAttorney's subscribe callback
 *  - typescript:S6660, an `if` alone in an `else` in applyEnglishInterpreterLock
 *
 * Neither is a behaviour change, which is exactly why they needed covering:
 * a rewritten conditional that quietly inverts a branch reads identically.
 */
describe('wizard prefill and interpreter lock (#628)', () => {
  interface Probe {
    form: FormGroup;
    englishLanguageId: string | null;
    prefillBookingAttorney(): void;
    applyEnglishInterpreterLock(id: string | null): void;
  }

  let profileCalls: number;

  function create(opts: { roles?: string[]; profile?: unknown } = {}): Probe {
    profileCalls = 0;
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
            getOne: (k: string) => (k === 'currentUser' ? { roles: opts.roles ?? [] } : null),
            getAll: () => ({}),
            getDeep: () => null,
          },
        },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: AppointmentService, useValue: {} },
        { provide: AppointmentApprovalService, useValue: {} },
        { provide: CustomFieldsService, useValue: { getList: () => of({ items: [] }) } },
        { provide: AppointmentDraftService, useValue: { get: () => of(null) } },
        {
          provide: MyAttorneyProfileService,
          useValue: {
            get: () => {
              profileCalls += 1;
              return of(opts.profile ?? null);
            },
          },
        },
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

  describe('prefillBookingAttorney (S3358)', () => {
    const PROFILE = { firstName: 'Ada', lastName: 'Nakamura', firmName: 'Nakamura LLP' };

    it('fills the defense section for a defense-attorney profile', () => {
      const c = create({ roles: ['Defense Attorney'], profile: { ...PROFILE, kind: 'defense' } });

      c.prefillBookingAttorney();

      expect(c.form.get('defenseAttorneyFirstName')!.value).toBe('Ada');
      expect(c.form.get('applicantAttorneyFirstName')!.value).toBeNull();
    });

    it('fills the applicant section for an applicant-attorney profile', () => {
      const c = create({
        roles: ['Applicant Attorney'],
        profile: { ...PROFILE, kind: 'applicant' },
      });

      c.prefillBookingAttorney();

      expect(c.form.get('applicantAttorneyFirstName')!.value).toBe('Ada');
      expect(c.form.get('defenseAttorneyFirstName')!.value).toBeNull();
    });

    /** The arm the ternary rewrite had to preserve: no kind means no prefill. */
    it('fills nothing when the profile has no kind', () => {
      const c = create({ roles: ['Applicant Attorney'], profile: { ...PROFILE, kind: null } });

      c.prefillBookingAttorney();

      expect(c.form.get('applicantAttorneyFirstName')!.value).toBeNull();
      expect(c.form.get('defenseAttorneyFirstName')!.value).toBeNull();
    });

    /**
     * The role gate exists to avoid a 404 and its console error for bookers who
     * have no attorney profile at all.
     */
    it('never calls the profile endpoint for a non-attorney booker', () => {
      const c = create({ roles: ['Patient'], profile: { ...PROFILE, kind: 'applicant' } });

      c.prefillBookingAttorney();

      expect(profileCalls).toBe(0);
    });

    it('does not clobber a section the booker has already filled', () => {
      const c = create({
        roles: ['Applicant Attorney'],
        profile: { ...PROFILE, kind: 'applicant' },
      });
      c.form.get('applicantAttorneyFirstName')!.setValue('Typed already');

      c.prefillBookingAttorney();

      expect(c.form.get('applicantAttorneyFirstName')!.value).toBe('Typed already');
    });
  });

  describe('applyEnglishInterpreterLock (S6660)', () => {
    it('forces the interpreter off and clears the vendor for English', () => {
      const c = create();
      c.englishLanguageId = 'en';
      c.form.get('needsInterpreter')!.setValue(true);
      c.form.get('interpreterVendorName')!.setValue('Acme Interpreting');

      c.applyEnglishInterpreterLock('en');

      expect(c.form.get('needsInterpreter')!.value).toBeFalse();
      expect(c.form.get('interpreterVendorName')!.value).toBeNull();
    });

    /** The else arm the rewrite collapsed: a non-English pick must re-enable. */
    it('re-enables a disabled interpreter control for a non-English language', () => {
      const c = create();
      c.englishLanguageId = 'en';
      c.form.get('needsInterpreter')!.disable({ emitEvent: false });

      c.applyEnglishInterpreterLock('es');

      expect(c.form.get('needsInterpreter')!.disabled).toBeFalse();
    });

    it('leaves an already-enabled control alone for a non-English language', () => {
      const c = create();
      c.englishLanguageId = 'en';
      c.form.get('needsInterpreter')!.setValue(true);

      c.applyEnglishInterpreterLock('es');

      expect(c.form.get('needsInterpreter')!.value).toBeTrue();
      expect(c.form.get('needsInterpreter')!.disabled).toBeFalse();
    });

    /**
     * Before the language lookup resolves the method must not touch anything --
     * the lookup's success branch re-calls it once the cache is set.
     */
    it('does nothing before the English language id is known', () => {
      const c = create();
      c.englishLanguageId = null;
      c.form.get('needsInterpreter')!.setValue(true);

      c.applyEnglishInterpreterLock('en');

      expect(c.form.get('needsInterpreter')!.value).toBeTrue();
    });
  });
});
