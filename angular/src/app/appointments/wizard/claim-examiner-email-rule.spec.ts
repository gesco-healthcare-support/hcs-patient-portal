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
 * 2026-11-13: the claim examiner EMAIL is optional only for a law-office booker once the other side's
 * attorney has a real email (the server enforces the same rule from the caller's roles). The control's
 * validity is what blocks Continue, so it is asserted directly. Synthetic addresses only.
 */
describe('AppointmentWizardComponent claim examiner email rule', () => {
  interface Probe {
    form: FormGroup;
    claimExaminerEmailRequired: boolean;
  }

  function create(roles: string[]): Probe {
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

  const ce = (c: Probe) => c.form.get('appointmentClaimExaminerEmail')!;

  function giveOtherSide(c: Probe, prefix: 'defenseAttorney' | 'applicantAttorney', email: string) {
    c.form.get(prefix + 'Enabled')!.setValue(true);
    c.form.get(prefix + 'Email')!.setValue(email);
  }

  it('applicant-side booker: required until the defense attorney email is real, then optional', () => {
    const c = create(['Applicant Paralegal']);
    expect(c.claimExaminerEmailRequired).toBeTrue();
    expect(ce(c).hasError('required')).toBeTrue();

    giveOtherSide(c, 'defenseAttorney', 'jane.doe@firm.test');

    expect(c.claimExaminerEmailRequired).toBeFalse();
    expect(ce(c).hasError('required')).toBeFalse();
    expect(ce(c).valid).toBeTrue();
  });

  it('defense-side booker: the applicant attorney email is what frees it', () => {
    const c = create(['Defense Attorney']);
    giveOtherSide(c, 'applicantAttorney', 'jane.doe@firm.test');
    expect(c.claimExaminerEmailRequired).toBeFalse();
    expect(ce(c).valid).toBeTrue();
  });

  it('a typed value must still be a real email', () => {
    const c = create(['Applicant Attorney']);
    giveOtherSide(c, 'defenseAttorney', 'jane.doe@firm.test');

    ce(c).setValue('not-an-email');
    expect(ce(c).hasError('email')).toBeTrue();
    ce(c).setValue('unknown@gmail.com');
    expect(ce(c).hasError('placeholderEmail')).toBeTrue();
    ce(c).setValue('real.examiner@insurer.test');
    expect(ce(c).valid).toBeTrue();
  });

  it('a placeholder or malformed other-side email does not free it, and clearing it brings it back', () => {
    const c = create(['Applicant Attorney']);
    giveOtherSide(c, 'defenseAttorney', 'unknown@firm.test');
    expect(c.claimExaminerEmailRequired).toBeTrue();

    c.form.get('defenseAttorneyEmail')!.setValue('jane.doe@firm.test');
    expect(c.claimExaminerEmailRequired).toBeFalse();
    c.form.get('defenseAttorneyEmail')!.setValue('');
    expect(c.claimExaminerEmailRequired).toBeTrue();
    expect(ce(c).hasError('required')).toBeTrue();
  });

  it('staff, patient and claim examiner bookers always keep it required', () => {
    for (const role of ['admin', 'Intake Staff', 'Patient', 'Claim Examiner']) {
      const c = create([role]);
      giveOtherSide(c, 'defenseAttorney', 'jane.doe@firm.test');
      giveOtherSide(c, 'applicantAttorney', 'jane.doe@firm.test');
      expect(c.claimExaminerEmailRequired).withContext(role).toBeTrue();
      expect(ce(c).hasError('required')).withContext(role).toBeTrue();
      TestBed.resetTestingModule();
    }
  });
});
