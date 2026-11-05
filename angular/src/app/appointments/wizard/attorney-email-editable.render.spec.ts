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
 * 2026-11-04: an attorney booker's own attorney email is EDITABLE again. #1524 locked it
 * by binding `[isReadOnly]` on the wizard's two attorney sections to "the field holds my
 * login email"; Levon reversed that, so a booker can type the lawyer of record as before.
 *
 * <p>The lock lived only in the TEMPLATE binding, so a component-level spec cannot see it
 * come back. This spec renders the real wizard template and reads the value the template
 * passes to each attorney section, in the exact state that used to lock: an attorney login
 * whose own-side attorney email equals the login.</p>
 *
 * <p>The children are not under test, so the wizard's imports are reduced to what the
 * template itself needs (forms and two pipes) and every child element renders as an
 * unknown element. Angular still writes each property binding onto that element, which is
 * what makes the bound `isReadOnly` readable here. All addresses are synthetic.</p>
 */
describe('AppointmentWizardComponent attorney email (rendered)', () => {
  const LOGIN = 'ada@example.test';

  /** Index into the wizard's fixed nine-step list. */
  const STEP = { applicant: 2, defense: 3 } as const;

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

  /** Puts the wizard on a side's step with that side's attorney email set to the login. */
  function onOwnSideWithLoginEmail(
    fixture: ComponentFixture<AppointmentWizardComponent>,
    side: 'applicant' | 'defense',
  ): HTMLElement & { isReadOnly?: boolean } {
    const wizard = fixture.componentInstance as unknown as {
      current: number;
      form: { get(name: string): { setValue(v: string): void } | null };
    };
    wizard.current = STEP[side];
    wizard.form.get(side + 'AttorneyEmail')!.setValue(LOGIN);
    fixture.detectChanges();

    const section = fixture.nativeElement.querySelector('app-appointment-add-attorney-section') as
      | (HTMLElement & { isReadOnly?: boolean })
      | null;
    expect(section)
      .withContext(side + ' attorney section rendered')
      .not.toBeNull();
    return section!;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('leaves the applicant attorney email editable for an applicant attorney typing their own login', () => {
    const fixture = render('Applicant Attorney');
    const section = onOwnSideWithLoginEmail(fixture, 'applicant');
    expect(section.isReadOnly).toBeFalse();
  });

  it('leaves the defense attorney email editable for a defense attorney typing their own login', () => {
    const fixture = render('Defense Attorney');
    const section = onOwnSideWithLoginEmail(fixture, 'defense');
    expect(section.isReadOnly).toBeFalse();
  });
});
