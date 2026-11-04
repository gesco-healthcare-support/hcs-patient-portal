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
import type { NotifyParalegalState } from '../shared/notify-paralegal.util';

/**
 * Paralegal booking (PR-P2): an attorney booker's own-side email is locked and prefilled, the
 * prefill is not wiped by the profile load, a paralegal booker gets a blank REQUIRED attorney
 * section, and the "send all notifications to my paralegal" tick lands in the submit accessors.
 */
describe('wizard paralegal booking', () => {
  interface Probe {
    form: FormGroup;
    current: number;
    stepErrorSummary: { control: string }[];
    notifyParalegal: NotifyParalegalState;
    appointmentAuthorizedUsers: unknown[];
    authorizedUserRoleOptions: string[];
    isInternalBooker: boolean;
    attorneyAnswerIsValid(key: 'applicant' | 'defense'): boolean;
    attorneyEmailLocked(role: 'applicant' | 'defense'): boolean;
    prefillBookingAttorney(): void;
    loadExternalUserProfile(): void;
    validateCurrentStep(): boolean;
    shouldShowAuthorizedUserSection(): boolean;
    buildSubmitPayload(): { accessors?: { email: string; role: string }[] };
  }

  const STEP = { applicant: 2, defense: 3 } as const;
  let profileCalls: number;

  function create(
    opts: {
      roles?: string[];
      email?: string;
      profile?: unknown;
      restResponse?: unknown;
    } = {},
  ): Probe {
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
            getOne: (k: string) =>
              k === 'currentUser' ? { roles: opts.roles ?? [], email: opts.email } : null,
            getAll: () => ({}),
            getDeep: () => null,
          },
        },
        { provide: RestService, useValue: { request: () => of(opts.restResponse ?? null) } },
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

  describe('attorney email lock', () => {
    it('locks the own-side email of an attorney booker once it has a value', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      c.form.get('applicantAttorneyEmail')!.setValue('ada@example.test');

      expect(c.attorneyEmailLocked('applicant')).toBeTrue();
    });

    it('does not lock an empty email (no stranded required field)', () => {
      const c = create({ roles: ['Applicant Attorney'] });

      expect(c.attorneyEmailLocked('applicant')).toBeFalse();
    });

    it('does not lock the other side, a paralegal, staff or an IT admin', () => {
      const email = 'x@example.test';
      for (const [roles, side] of [
        [['Applicant Attorney'], 'defense'],
        [['Applicant Paralegal'], 'applicant'],
        [['Defense Paralegal'], 'defense'],
        [['Staff Supervisor'], 'applicant'],
        [['Applicant Attorney', 'IT Admin'], 'applicant'],
      ] as Array<[string[], 'applicant' | 'defense']>) {
        const c = create({ roles });
        c.form.get(side + 'AttorneyEmail')!.setValue(email);
        expect(c.attorneyEmailLocked(side)).withContext(roles.join()).toBeFalse();
        TestBed.resetTestingModule();
      }
    });
  });

  describe('attorney prefill', () => {
    it('falls back to the login email when the profile has none', () => {
      const c = create({
        roles: ['Defense Attorney'],
        email: 'login@example.test',
        profile: { kind: 'defense', firstName: 'Dee', lastName: 'Fence' },
      });

      c.prefillBookingAttorney();

      expect(c.form.get('defenseAttorneyEmail')!.value).toBe('login@example.test');
    });

    it('never prefills a paralegal booker (and never calls the profile endpoint)', () => {
      const c = create({
        roles: ['Applicant Paralegal'],
        profile: { kind: 'applicant', firstName: 'Ada' },
      });

      c.prefillBookingAttorney();

      expect(profileCalls).toBe(0);
      expect(c.form.get('applicantAttorneyFirstName')!.value).toBeNull();
    });
  });

  describe('prefill race', () => {
    const PROFILE = {
      identityUserId: 'u1',
      firstName: 'Ada',
      lastName: 'Booker',
      email: 'a@b.test',
    };

    it('keeps attorney fields that were prefilled before the profile load returned', () => {
      const c = create({ roles: ['Applicant Attorney'], restResponse: PROFILE });
      c.form.patchValue({
        applicantAttorneyFirstName: 'Ada',
        applicantAttorneyEmail: 'ada@example.test',
        applicantAttorneyFirmName: 'Ada LLP',
      });

      c.loadExternalUserProfile();

      expect(c.form.get('applicantAttorneyFirstName')!.value).toBe('Ada');
      expect(c.form.get('applicantAttorneyEmail')!.value).toBe('ada@example.test');
      expect(c.form.get('applicantAttorneyFirmName')!.value).toBe('Ada LLP');
    });

    it('still resets a section that is blank (and clears the patient fields)', () => {
      const c = create({ roles: ['Applicant Attorney'], restResponse: PROFILE });
      c.form.patchValue({ firstName: 'Stale' });

      c.loadExternalUserProfile();

      expect(c.form.get('firstName')!.value).toBeNull();
      expect(c.form.get('applicantAttorneyFirstName')!.value).toBeNull();
    });
  });

  describe('paralegal booker', () => {
    for (const [role, key, other] of [
      ['Applicant Paralegal', 'applicant', 'defense'],
      ['Defense Paralegal', 'defense', 'applicant'],
    ] as Array<[string, 'applicant' | 'defense', 'applicant' | 'defense']>) {
      it(`${role}: own-side attorney section is required and the question is not asked`, () => {
        const c = create({ roles: [role] });
        c.form.get(key + 'AttorneyEnabled')!.setValue(null);

        expect(c.attorneyAnswerIsValid(key)).toBeTrue();
        expect(c.form.get(key + 'AttorneyEnabled')!.value).toBeTrue();
      });

      it(`${role}: the other side is still asked`, () => {
        const c = create({ roles: [role] });
        c.form.get(other + 'AttorneyEnabled')!.setValue(null);

        expect(c.attorneyAnswerIsValid(other)).toBeFalse();
      });

      it(`${role}: is external, sees the authorized-user section, own side's paralegal only`, () => {
        const c = create({ roles: [role] });

        expect(c.isInternalBooker).toBeFalse();
        expect(c.shouldShowAuthorizedUserSection()).toBeTrue();
        expect(c.authorizedUserRoleOptions).toContain(role);
        expect(c.authorizedUserRoleOptions).not.toContain(
          role === 'Applicant Paralegal' ? 'Defense Paralegal' : 'Applicant Paralegal',
        );
      });
    }

    it('staff are offered both paralegal roles', () => {
      const c = create({ roles: ['Staff Supervisor'] });

      expect(c.authorizedUserRoleOptions).toContain('Applicant Paralegal');
      expect(c.authorizedUserRoleOptions).toContain('Defense Paralegal');
    });
  });

  describe('send all notifications to my paralegal', () => {
    const TICKED: NotifyParalegalState = {
      enabled: true,
      firstName: 'Jane',
      lastName: 'Paralegal',
      email: 'jane.paralegal@example.test',
    };

    it('blocks Continue on the own-side step while the ticked fields are incomplete', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      c.current = STEP.applicant;
      c.notifyParalegal = { ...TICKED, email: '' };

      expect(c.validateCurrentStep()).toBeFalse();
      expect(c.stepErrorSummary.map((e) => e.control)).toContain('notifyParalegal.email');
    });

    it('does not consult the tick on the other side step', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      c.current = STEP.defense;
      c.notifyParalegal = { ...TICKED, email: '' };

      c.validateCurrentStep();

      expect(c.stepErrorSummary.map((e) => e.control)).not.toContain('notifyParalegal.email');
    });

    /** The submit payload needs a patient to build; any valid-looking one will do. */
    function withPatient(c: Probe): void {
      c.form.patchValue({ firstName: 'Pat', lastName: 'Test', dateOfBirth: '1980-01-15' });
    }

    it('adds the paralegal to the submit accessors with the booker side role', () => {
      const c = create({ roles: ['Defense Attorney'] });
      c.notifyParalegal = TICKED;
      withPatient(c);

      const accessors = c.buildSubmitPayload().accessors ?? [];

      expect(accessors.map((a) => [a.email, a.role])).toEqual([
        ['jane.paralegal@example.test', 'Defense Paralegal'],
      ]);
    });

    it('adds nothing when unticked, and never twice for the same email', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      withPatient(c);
      expect(c.buildSubmitPayload().accessors ?? []).toEqual([]);

      c.notifyParalegal = TICKED;
      c.appointmentAuthorizedUsers = [
        {
          firstName: 'J',
          lastName: 'P',
          email: 'JANE.paralegal@example.test',
          userRole: 'Applicant Paralegal',
          accessTypeId: 24,
        },
      ];
      expect(c.buildSubmitPayload().accessors?.length).toBe(1);
    });
  });
});
