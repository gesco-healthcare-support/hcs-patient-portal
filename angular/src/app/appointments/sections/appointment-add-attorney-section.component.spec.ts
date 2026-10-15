import { SimpleChange } from '@angular/core';
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

/**
 * #791 -- the component had a handler for a case its own architecture forbids.
 * ngOnChanges explicitly watched changes['role'] and re-resolved the enabled
 * subscription, while the memoised address map did not, so a role change would
 * have left the two disagreeing: Defense fields rendered, Applicant address
 * controls autocompleted.
 *
 * It could not happen -- the wizard gives each role its own @switch branch, so
 * a role change means a fresh instance -- which is precisely the problem. The
 * handler advertised support the cache did not provide, and the next person to
 * replace that @switch would have found out the hard way.
 */
describe('AppointmentAddAttorneySectionComponent role change (#791)', () => {
  function make(role: 'applicant' | 'defense'): AppointmentAddAttorneySectionComponent {
    const fixture = TestBed.createComponent(AppointmentAddAttorneySectionComponent);
    const component = fixture.componentInstance;
    component.role = role;
    component.form = TestBed.inject(FormBuilder).group({}) as FormGroup;
    return component;
  }

  it('re-derives the address map when role changes on a live instance', () => {
    const component = make('applicant');
    expect(component.addressFields.street).toBe('applicantAttorneyStreet');

    component.role = 'defense';
    component.ngOnChanges({
      role: new SimpleChange('applicant', 'defense', false),
    });

    expect(component.addressFields).toEqual({
      street: 'defenseAttorneyStreet',
      city: 'defenseAttorneyCity',
      state: 'defenseAttorneyStateId',
      zip: 'defenseAttorneyZipCode',
    });
  });

  /** The memo must survive a change that is not `role`, or OnPush churns. */
  it('keeps the cached reference when only the form changes', () => {
    const component = make('applicant');
    const before = component.addressFields;

    component.ngOnChanges({
      form: new SimpleChange(null, component.form, false),
    });

    expect(component.addressFields).toBe(before);
  });
});
