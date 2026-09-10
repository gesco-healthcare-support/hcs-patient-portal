import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup } from '@angular/forms';

import { AppointmentAddAttorneySectionComponent } from './appointment-add-attorney-section.component';

/**
 * #121 phase T5 -- the Applicant and Defense attorney cards are ONE template.
 * Every asymmetry between them (formControlName prefix, heading, checkbox id,
 * state-select cid, address field map) derives from the single `role` Input,
 * so that derivation is the component's entire contract and is what these
 * specs pin. Added with sweep #632, which restructured the `addressFields`
 * memo to lift its assignment out of the return expression (typescript:S1121);
 * the reference-identity spec below is what makes that restructure safe.
 */
describe('AppointmentAddAttorneySectionComponent role derivation', () => {
  function make(role: 'applicant' | 'defense'): AppointmentAddAttorneySectionComponent {
    const fixture = TestBed.createComponent(AppointmentAddAttorneySectionComponent);
    const component = fixture.componentInstance;
    component.role = role;
    // The 12 attorney controls live on the PARENT form; the child only reads
    // them by name. An empty group is enough -- these specs never render.
    component.form = TestBed.inject(FormBuilder).group({}) as FormGroup;
    return component;
  }

  it('derives the formControlName prefix from role', () => {
    expect(make('applicant').prefix).toBe('applicantAttorney');
    expect(make('defense').prefix).toBe('defenseAttorney');
  });

  it('derives the heading, checkbox id and state-select cid from role', () => {
    const applicant = make('applicant');
    expect(applicant.headingText).toBe('Applicant Attorney Details');
    expect(applicant.checkboxId).toBe('applicant-attorney-enabled');
    expect(applicant.stateSelectCid).toBe('appointment-applicant-attorney-state-id');

    const defense = make('defense');
    expect(defense.headingText).toBe('Defense Attorney Details');
    expect(defense.checkboxId).toBe('defense-attorney-enabled');
    expect(defense.stateSelectCid).toBe('appointment-defense-attorney-state-id');
  });

  it('maps the four autocomplete address fields onto the role prefix', () => {
    expect(make('defense').addressFields).toEqual({
      street: 'defenseAttorneyStreet',
      city: 'defenseAttorneyCity',
      state: 'defenseAttorneyStateId',
      zip: 'defenseAttorneyZipCode',
    });
  });

  /**
   * The memo is load-bearing, not a micro-optimisation: `addressFields` is
   * bound into an OnPush child ([fields] on app-address-autocomplete), so a
   * fresh object per change-detection pass would mark that child dirty every
   * cycle. Reference identity is the property that prevents it, and this spec
   * fails if the caching is ever dropped.
   */
  it('returns the same object reference on repeated reads', () => {
    const component = make('applicant');
    expect(component.addressFields).toBe(component.addressFields);
  });
});
