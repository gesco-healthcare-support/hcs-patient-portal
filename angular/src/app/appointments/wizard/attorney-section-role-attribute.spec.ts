import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup } from '@angular/forms';
import { of } from 'rxjs';
import { LocalizationService } from '@abp/ng.core';

import { AppointmentAddAttorneySectionComponent } from '../sections/appointment-add-attorney-section.component';
import { AddressValidationProvider } from '../../shared/address/address-validation.provider';
import { PatientService } from '../../proxy/patients/patient.service';

/**
 * #628 -- `Web:S6821` reports `role="applicant"` and `role="defense"` on the
 * wizard's two attorney-section usages as invalid ARIA roles. It is easy to
 * read that as a false positive: `role` is an `@Input()` on the component, not
 * an ARIA attribute.
 *
 * It is not a false positive. Angular writes a STATIC attribute into the DOM
 * as well as using it to set the matching input, so the rendered custom element
 * really does carry `role="applicant"` -- a value in no ARIA role vocabulary.
 * Assistive technology sees an element claiming an unknown role, which is worse
 * than an element claiming none.
 *
 * A property binding sets the input WITHOUT emitting the attribute, so the fix
 * is `[role]="'applicant'"`. These specs pin both halves of that claim, because
 * the whole change rests on a framework behaviour rather than on our code.
 */
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

function attorneyForm(fb: FormBuilder): FormGroup {
  const controls: Record<string, unknown> = {};
  for (const role of ['applicantAttorney', 'defenseAttorney']) {
    for (const f of ATTORNEY_FIELDS) {
      controls[role + f] = [f === 'Enabled' ? true : ''];
    }
  }
  return fb.group(controls);
}

/** The shape the wizard used before this change. */
@Component({
  standalone: true,
  imports: [AppointmentAddAttorneySectionComponent],
  template: `
    <app-appointment-add-attorney-section
      [form]="form"
      role="applicant"
      [mandatory]="true"
      [getStateLookup]="getStateLookup"
    />
  `,
})
class StaticRoleHost {
  form!: FormGroup;
  getStateLookup = () => of({ items: [], totalCount: 0 }) as never;
}

/** The shape it uses now. */
@Component({
  standalone: true,
  imports: [AppointmentAddAttorneySectionComponent],
  template: `
    <app-appointment-add-attorney-section
      [form]="form"
      [role]="'applicant'"
      [mandatory]="true"
      [getStateLookup]="getStateLookup"
    />
  `,
})
class BoundRoleHost {
  form!: FormGroup;
  getStateLookup = () => of({ items: [], totalCount: 0 }) as never;
}

describe('attorney section role attribute (#628, Web:S6821)', () => {
  function render<T extends { form: FormGroup }>(host: new () => T): HTMLElement {
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
    const fixture = TestBed.createComponent(host);
    fixture.componentInstance.form = attorneyForm(TestBed.inject(FormBuilder));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => TestBed.resetTestingModule());

  /**
   * The bug, pinned so nobody reverts the fix believing it was cosmetic. If
   * Angular ever stopped reflecting static attributes this spec would fail and
   * the fix could be dropped -- which is the only circumstance in which it
   * should be.
   */
  it('a static role= attribute really does reach the DOM', () => {
    const host = render(StaticRoleHost);
    const el = host.querySelector('app-appointment-add-attorney-section')!;
    expect(el.getAttribute('role')).toBe('applicant');
  });

  it('a bound [role] sets the input without emitting the attribute', () => {
    const host = render(BoundRoleHost);
    const el = host.querySelector('app-appointment-add-attorney-section')!;
    expect(el.hasAttribute('role')).toBeFalse();
  });

  /** Binding must not break what the attribute was actually there to do. */
  it('the bound form still drives the role-derived ids', () => {
    const host = render(BoundRoleHost);
    const ids = Array.from(host.querySelectorAll('input'))
      .map((el) => el.getAttribute('id'))
      .filter(Boolean) as string[];

    expect(ids.length).toBeGreaterThan(0);
    expect(ids.every((id) => id.startsWith('applicant-attorney-'))).toBeTrue();
  });
});
