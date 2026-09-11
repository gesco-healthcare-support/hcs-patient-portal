import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup } from '@angular/forms';
import { of } from 'rxjs';
import { LocalizationService } from '@abp/ng.core';

import { AppointmentAddAttorneySectionComponent } from './appointment-add-attorney-section.component';
import { AppointmentAddClaimPartiesSectionComponent } from './appointment-add-claim-parties-section.component';
import { AppointmentAddEmployerDetailsComponent } from './appointment-add-employer-details.component';
import { AddressValidationProvider } from '../../shared/address/address-validation.provider';
import { PatientService } from '../../proxy/patients/patient.service';

/**
 * #792 -- the wizard sections carry element ids that are COMPUTED at runtime
 * (#780): nine attorney ids derived from `role`, loop-index ids inside `@for`.
 * The compiler proves those expressions resolve; nothing proves the ids they
 * produce are unique, or that a `[for]` lands on the id it was meant to. A
 * duplicate or a mismatched pair renders fine, passes every check, and breaks
 * label association only for someone using a screen reader.
 *
 * These specs render the section and assert against the real DOM, which is the
 * only place that question can be answered.
 */

/** A control is named if it has a label pointing at its id, sits inside a
 *  label, or carries an aria-label. Mirrors what assistive tech accepts. */
function accessibleName(el: Element, root: Element): string | null {
  const id = el.getAttribute('id');
  if (id) {
    const label = root.querySelector(`label[for="${CSS.escape(id)}"]`);
    if (label?.textContent?.trim()) return label.textContent.trim();
  }
  if (el.closest('label')) return el.closest('label')!.textContent?.trim() ?? '';
  const aria = el.getAttribute('aria-label');
  return aria?.trim() ? aria.trim() : null;
}

function controlsIn(root: Element): HTMLElement[] {
  return Array.from(root.querySelectorAll('input, select, textarea'));
}

const ATTORNEY_FIELDS = [
  'Enabled',
  'FirstName',
  'LastName',
  'Email',
  'FirmName',
  'WebAddress',
  'PhoneNumber',
  'FaxNumber',
  'Street',
  'City',
  'StateId',
  'ZipCode',
];

describe('Wizard section label association (#792)', () => {
  function attorneyForm(fb: FormBuilder): FormGroup {
    const controls: Record<string, unknown> = {};
    for (const role of ['applicantAttorney', 'defenseAttorney']) {
      for (const f of ATTORNEY_FIELDS) {
        controls[role + f] = [f === 'Enabled' ? true : ''];
      }
    }
    return fb.group(controls);
  }

  function renderAttorney(role: 'applicant' | 'defense'): HTMLElement {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: LocalizationService,
          useValue: { instant: (k: string) => k, get: (k: string) => k },
        },
        { provide: AddressValidationProvider, useValue: { autocomplete: () => of([]) } },
        {
          provide: PatientService,
          useValue: { getStateLookup: () => of({ items: [], totalCount: 0 }) },
        },
      ],
    });
    const fixture = TestBed.createComponent(AppointmentAddAttorneySectionComponent);
    const c = fixture.componentInstance;
    c.form = attorneyForm(TestBed.inject(FormBuilder));
    c.role = role;
    c.mandatory = true; // renders the card body without needing the Yes/No answer
    c.questionText = 'Is there an attorney?';
    c.getStateLookup = () => of({ items: [], totalCount: 0 }) as never;
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('renders every attorney control with an accessible name', () => {
    const host = renderAttorney('applicant');
    const unnamed = controlsIn(host)
      .filter((el) => !accessibleName(el, host))
      .map((el) => el.tagName.toLowerCase() + '#' + (el.getAttribute('id') ?? '(no id)'));
    expect(unnamed).toEqual([]);
  });

  it('gives every attorney control a unique id', () => {
    const host = renderAttorney('defense');
    const ids = controlsIn(host)
      .map((el) => el.getAttribute('id'))
      .filter(Boolean) as string[];
    const dupes = ids.filter((id, i) => ids.indexOf(id) !== i);
    expect(dupes).toEqual([]);
  });

  /**
   * The one that matters. One template serves both roles, so if the ids did not
   * derive from `role` the two would collide the moment anything renders them
   * together. This is the only mechanical proof that #780's derivation works.
   */
  it('produces disjoint id sets for the applicant and defense roles', () => {
    const applicant = controlsIn(renderAttorney('applicant'))
      .map((el) => el.getAttribute('id'))
      .filter(Boolean) as string[];
    TestBed.resetTestingModule();
    const defense = controlsIn(renderAttorney('defense'))
      .map((el) => el.getAttribute('id'))
      .filter(Boolean) as string[];

    expect(applicant.length).toBeGreaterThan(0);
    expect(defense).toHaveSize(applicant.length);
    expect(applicant.filter((id) => defense.includes(id))).toEqual([]);
    // Two id shapes are in play and both encode the role: the nine inputs use
    // `role + '-attorney-...'` (#780) and the state select uses the older
    // `stateSelectCid`, 'appointment-' + role + '-attorney-state-id'. The
    // assertion is that every id names its role, not that they share a prefix.
    expect(applicant.every((id) => id.includes('applicant-attorney-'))).toBeTrue();
    expect(defense.every((id) => id.includes('defense-attorney-'))).toBeTrue();
  });
});

/**
 * The same question asked of the sections that render cheaply. These use static
 * ids rather than computed ones, so the risk is lower -- but the check is
 * generic, which means it also catches the NEXT unlabelled control added to any
 * of them, not only the ones this sweep touched. That is most of its value.
 */
describe('Wizard section label association, static-id sections (#792)', () => {
  const CLAIM_PARTIES = [
    'appointmentInsuranceName',
    'appointmentInsuranceSuite',
    'appointmentInsurancePhoneNumber',
    'appointmentInsuranceFaxNumber',
    'appointmentInsuranceStreet',
    'appointmentInsuranceCity',
    'appointmentInsuranceStateId',
    'appointmentInsuranceZip',
    'appointmentClaimExaminerName',
    'appointmentClaimExaminerEmail',
    'appointmentClaimExaminerSuite',
    'appointmentClaimExaminerPhoneNumber',
    'appointmentClaimExaminerFax',
    'appointmentClaimExaminerStreet',
    'appointmentClaimExaminerCity',
    'appointmentClaimExaminerStateId',
    'appointmentClaimExaminerZip',
  ];
  const EMPLOYER = [
    'employerName',
    'employerOccupation',
    'employerPhoneNumber',
    'employerStreet',
    'employerCity',
    'employerStateId',
    'employerZipCode',
  ];

  function configure() {
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: LocalizationService,
          useValue: { instant: (k: string) => k, get: (k: string) => k },
        },
        { provide: AddressValidationProvider, useValue: { autocomplete: () => of([]) } },
        {
          provide: PatientService,
          useValue: { getStateLookup: () => of({ items: [], totalCount: 0 }) },
        },
      ],
    });
  }

  function groupOf(names: string[]): FormGroup {
    const controls: Record<string, unknown> = {};
    for (const n of names) controls[n] = [''];
    return TestBed.inject(FormBuilder).group(controls);
  }

  afterEach(() => TestBed.resetTestingModule());

  function assertAllNamed(host: HTMLElement): void {
    const unnamed = controlsIn(host)
      .filter((el) => !accessibleName(el, host))
      .map((el) => el.tagName.toLowerCase() + '#' + (el.getAttribute('id') ?? '(no id)'));
    expect(unnamed).toEqual([]);
    const ids = controlsIn(host)
      .map((el) => el.getAttribute('id'))
      .filter(Boolean) as string[];
    expect(ids.filter((id, i) => ids.indexOf(id) !== i)).toEqual([]);
  }

  for (const only of ['insurance', 'examiner'] as const) {
    it(`names every control in the claim-parties section (only="${only}")`, () => {
      configure();
      const fixture = TestBed.createComponent(AppointmentAddClaimPartiesSectionComponent);
      const c = fixture.componentInstance;
      c.form = groupOf(CLAIM_PARTIES);
      c.only = only;
      c.getStateLookup = () => of({ items: [], totalCount: 0 }) as never;
      fixture.detectChanges();
      assertAllNamed(fixture.nativeElement as HTMLElement);
    });
  }

  it('names every control in the employer-details section', () => {
    configure();
    const fixture = TestBed.createComponent(AppointmentAddEmployerDetailsComponent);
    const c = fixture.componentInstance;
    c.form = groupOf(EMPLOYER);
    c.getStateLookup = () => of({ items: [], totalCount: 0 }) as never;
    fixture.detectChanges();
    assertAllNamed(fixture.nativeElement as HTMLElement);
  });
});
