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
/**
 * 2026-11-06: the re-evaluation entry. Renders the real wizard template and checks the two radio
 * cards (nothing preselected), the number box appearing only on the number path, the corrected
 * placeholder, the prefill banner on every step, and that mode 4 still goes out for "before this
 * portal". Children render as unknown elements; all data is synthetic.
 */
describe('AppointmentWizardComponent re-evaluation entry (rendered)', () => {
  const LOGIN = 'ada@example.test';

  function restFor(url: string): Observable<unknown> {
    if (url.includes('appointment-type-field-configs') || url.includes('options-by-type')) {
      return of([]);
    }
    if (url.includes('by-appointment')) {
      return of([]);
    }
    return of({ items: [], totalCount: 0 });
  }

  function render(
    query: Record<string, string>,
    roles: string[] = ['Staff Supervisor'],
  ): ComponentFixture<AppointmentWizardComponent> {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null } },
            queryParamMap: of({ get: (key: string) => query[key] ?? null }),
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
                ? {
                    id: 'user-1',
                    name: 'Ada',
                    surname: 'Lovelace',
                    email: LOGIN,
                    roles,
                  }
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

  type Wizard = {
    current: number;
    steps: unknown[];
    sourceConfirmationNumber: string | null;
    revalWithoutSource: boolean;
    revalEntryChoice: string | null;
    sourceDateOfBirthInput: string;
    resolveSubmitMode(): number;
  };
  const wiz = (f: ComponentFixture<AppointmentWizardComponent>): Wizard =>
    f.componentInstance as unknown as Wizard;
  const q = (f: ComponentFixture<AppointmentWizardComponent>, sel: string) =>
    (f.nativeElement as HTMLElement).querySelector(sel);

  afterEach(() => TestBed.resetTestingModule());

  it('offers staff two unselected radio cards at the top of a re-evaluation', () => {
    const f = render({ type: '2' });
    const radios = (f.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
      'input[name="wiz-reval-entry"]',
    );
    expect(radios.length).toBe(2);
    expect(Array.from(radios).some((r) => r.checked)).toBeFalse();
    expect(q(f, '#wiz-prior-confirmation-number')).toBeNull();
  });

  it('offers an outside booker a third card for a forgotten confirmation number (R3)', () => {
    const f = render({ type: '2' }, ['Applicant Paralegal']);
    const values = Array.from(
      (f.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
        'input[name="wiz-reval-entry"]',
      ),
    ).map((r) => r.value);
    expect(values).toEqual(['number', 'lookup', 'before']);

    (q(f, '#wiz-reval-entry-lookup') as HTMLInputElement).click();
    f.detectChanges();
    expect(q(f, 'app-confirmation-number-request')).not.toBeNull();
    expect(q(f, '#wiz-prior-confirmation-number')).toBeNull();
    expect(wiz(f).revalWithoutSource).toBeFalse();
  });

  it('moves to the number card when the booker says the number has arrived', () => {
    const f = render({ type: '2' }, ['Applicant Paralegal']);
    (q(f, '#wiz-reval-entry-lookup') as HTMLInputElement).click();
    f.detectChanges();

    q(f, 'app-confirmation-number-request')!.dispatchEvent(new Event('haveNumber'));
    f.detectChanges();

    expect(wiz(f).revalEntryChoice).toBe('number');
    expect(q(f, 'app-confirmation-number-request')).toBeNull();
    expect(q(f, '#wiz-prior-confirmation-number')).not.toBeNull();
  });

  it('shows no cards in a new request', () => {
    const f = render({});
    expect(q(f, '#wiz-reval-entry')).toBeNull();
  });

  it('opens the number box with the A00004 placeholder on the number card', () => {
    const f = render({ type: '2' });
    (q(f, '#wiz-reval-entry-number') as HTMLInputElement).click();
    f.detectChanges();
    const box = q(f, '#wiz-prior-confirmation-number') as HTMLInputElement;
    expect(box).not.toBeNull();
    expect(box.placeholder).toBe('e.g. A00004');
  });

  it('asks an outside booker for the patient date of birth beside the number (2026-11-06)', () => {
    const f = render({ type: '2' }, ['Applicant Paralegal']);
    (q(f, '#wiz-reval-entry-number') as HTMLInputElement).click();
    f.detectChanges();

    const dob = q(f, '#wiz-prior-patient-dob') as HTMLInputElement;
    expect(dob).not.toBeNull();
    expect(dob.type).toBe('date');
    expect(q(f, '#wiz-prior-patient-dob-hint')?.textContent).toContain('date of birth');

    dob.value = '1985-04-12';
    dob.dispatchEvent(new Event('input'));
    expect(wiz(f).sourceDateOfBirthInput).toBe('1985-04-12');
  });

  it('never asks staff for a date of birth: they can load any case in their office', () => {
    const f = render({ type: '2' });
    (q(f, '#wiz-reval-entry-number') as HTMLInputElement).click();
    f.detectChanges();

    expect(q(f, '#wiz-prior-confirmation-number')).not.toBeNull();
    expect(q(f, '#wiz-prior-patient-dob')).toBeNull();
  });

  it('sends the re-evaluation without a source (mode 4) on the before-this-portal card', () => {
    const f = render({ type: '2' });
    (q(f, '#wiz-reval-entry-before') as HTMLInputElement).click();
    f.detectChanges();
    expect(wiz(f).revalWithoutSource).toBeTrue();
    expect(q(f, '#wiz-prior-confirmation-number')).toBeNull();
    expect(wiz(f).resolveSubmitMode()).toBe(4);
  });

  it('hides the cards and shows the prefill banner once a source is loaded', () => {
    const f = render({ type: '2' });
    expect(q(f, '#wiz-prefill-banner')).toBeNull();
    wiz(f).sourceConfirmationNumber = 'A00004';
    f.detectChanges();
    expect(q(f, '#wiz-reval-entry')).toBeNull();
    const banner = q(f, '#wiz-prefill-banner');
    expect(banner).not.toBeNull();
    expect(banner!.getAttribute('role')).toBe('status');
  });

  it('repeats the banner on the review step and keeps it on every step', () => {
    const f = render({ type: '2' });
    wiz(f).sourceConfirmationNumber = 'A00004';
    wiz(f).current = 3;
    f.detectChanges();
    expect(q(f, '#wiz-prefill-banner')).not.toBeNull();
    wiz(f).current = wiz(f).steps.length - 1;
    f.detectChanges();
    expect(q(f, '#wiz-prefill-banner')).not.toBeNull();
    expect(q(f, '#wiz-prefill-banner-review')).not.toBeNull();
  });

  it('shows no banner when nothing was loaded for a new request', () => {
    const f = render({});
    wiz(f).sourceConfirmationNumber = 'A00004';
    f.detectChanges();
    expect(q(f, '#wiz-prefill-banner')).toBeNull();
  });
});
