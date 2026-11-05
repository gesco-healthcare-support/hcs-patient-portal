import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Observable, of } from 'rxjs';
import {
  ConfigStateService,
  LocalizationPipe,
  LocalizationService,
  RestService,
} from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentWizardComponent } from './appointment-wizard.component';
import { AppointmentService } from '../../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../../proxy/appointments/appointment-approval.service';
import { CustomFieldsService } from '../../proxy/custom-fields-controllers/custom-fields.service';
import { AppointmentDraftService } from '../../proxy/appointment-drafts/appointment-draft.service';
import { MyAttorneyProfileService } from '../../proxy/my-attorney-profiles/my-attorney-profile.service';
import { AddressValidationProvider } from '../../shared/address/address-validation.provider';
import { SsnMaskPipe } from '../../shared/pipes/ssn-mask.pipe';

/**
 * 2026-11-05 (decisions 3-5): the booking wizard's Authorized User section must match what the server lets
 * the booker add, or the atomic submit fails as a whole. A Patient booker gets no section (they ask staff),
 * a Claim Examiner booker the Claim Examiner role only, a law-office booker their own side only. Renders the
 * real wizard template and reads what it binds onto the section element. All addresses are synthetic.
 */
describe('AppointmentWizardComponent Authorized User section (rendered)', () => {
  const LOGIN = 'ada@example.test';

  /** Index of the review step, which hosts the Authorized User section, in the wizard's nine-step list. */
  const REVIEW_STEP = 8;

  function restFor(url: string): Observable<unknown> {
    if (url.includes('appointment-type-field-configs') || url.includes('options-by-type')) {
      return of([]);
    }
    if (url.includes('by-appointment')) {
      return of([]);
    }
    return of({ items: [], totalCount: 0 });
  }

  function render(role: string): ComponentFixture<AppointmentWizardComponent> {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null } },
            queryParamMap: of({ get: () => null }),
          },
        },
        {
          provide: Router,
          useValue: { navigateByUrl: () => undefined, navigate: () => undefined },
        },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) =>
              key === 'currentUser'
                ? { id: 'user-1', name: 'Ada', surname: 'Lovelace', email: LOGIN, roles: [role] }
                : null,
            getAll: () => ({}),
            getDeep: () => null,
          },
        },
        { provide: RestService, useValue: { request: (req: { url: string }) => restFor(req.url) } },
        { provide: LocalizationService, useValue: { instant: (key: string) => key } },
        { provide: AppointmentService, useValue: { submit: () => of({ appointmentId: 'a1' }) } },
        { provide: AppointmentApprovalService, useValue: { approveAppointment: () => of(null) } },
        {
          provide: CustomFieldsService,
          useValue: { getList: () => of({ items: [] }), getActiveForAppointmentType: () => of([]) },
        },
        {
          provide: AppointmentDraftService,
          useValue: {
            getMine: () => of(null),
            upsert: () => of({}),
            discardMine: () => of({}),
          },
        },
        { provide: MyAttorneyProfileService, useValue: { get: () => of(null) } },
        {
          provide: AddressValidationProvider,
          useValue: { autocomplete: () => of([]), validate: () => of({ status: 'ok' }) },
        },
        {
          provide: ConfirmationService,
          useValue: { warn: () => of(Confirmation.Status.confirm) },
        },
        {
          provide: ToasterService,
          useValue: { success: () => undefined, error: () => undefined, warn: () => undefined },
        },
      ],
    });
    TestBed.overrideComponent(AppointmentWizardComponent, {
      set: {
        imports: [ReactiveFormsModule, SsnMaskPipe, LocalizationPipe],
        schemas: [NO_ERRORS_SCHEMA],
      },
    });
    const fixture = TestBed.createComponent(AppointmentWizardComponent);
    fixture.detectChanges();
    return fixture;
  }

  function sectionFor(
    role: string,
  ): (HTMLElement & { roleOptions?: string[]; externalBooker?: boolean }) | null {
    const fixture = render(role);
    (fixture.componentInstance as unknown as { current: number }).current = REVIEW_STEP;
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('app-appointment-add-authorized-users');
  }

  afterEach(() => TestBed.resetTestingModule());

  it('renders no section for a Patient booker', () => {
    expect(sectionFor('Patient')).toBeNull();
  });

  it('offers a Claim Examiner booker the Claim Examiner role only', () => {
    const section = sectionFor('Claim Examiner');
    expect(section).not.toBeNull();
    expect(section!.roleOptions).toEqual(['Claim Examiner']);
    expect(section!.externalBooker).toBeTrue();
  });

  it("offers a law-office booker their own side's roles only", () => {
    expect(sectionFor('Defense Paralegal')!.roleOptions).toEqual([
      'Defense Attorney',
      'Defense Paralegal',
    ]);
    TestBed.resetTestingModule();
    expect(sectionFor('Applicant Attorney')!.roleOptions).toEqual([
      'Applicant Attorney',
      'Applicant Paralegal',
    ]);
  });

  it('offers staff every role and no assistant relabel', () => {
    const section = sectionFor('Staff Supervisor');
    expect(section!.roleOptions).toContain('Patient');
    expect(section!.externalBooker).toBeFalse();
  });
});
