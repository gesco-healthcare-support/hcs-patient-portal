import type { NgbTypeaheadSelectItemEvent } from '@ng-bootstrap/ng-bootstrap';

import { AppointmentAddPatientDemographicsComponent } from './appointment-add-patient-demographics.component';

/**
 * The booking form's patient section: the two pieces of logic behind the patient-by-email
 * typeahead. What the input shows after a pick, and which patient id a pick raises to the parent.
 *
 * <p>This section had no spec. It injects nothing, so it is built with `new`.</p>
 *
 * <p>All emails and ids below are synthetic.</p>
 */
describe('AppointmentAddPatientDemographicsComponent patient typeahead', () => {
  const pick = (item: unknown) => ({ item }) as unknown as NgbTypeaheadSelectItemEvent;

  function selections(c: AppointmentAddPatientDemographicsComponent): (string | null)[] {
    const out: (string | null)[] = [];
    c.patientSelected.subscribe((id) => out.push(id));
    return out;
  }

  it('shows a picked patient by email, and passes typed text through unchanged', () => {
    const c = new AppointmentAddPatientDemographicsComponent();
    expect(c.patientInputFormatter({ id: 'p-1', displayName: 'ada@example.test' })).toBe(
      'ada@example.test',
    );
    expect(c.patientInputFormatter('ada@exa')).toBe('ada@exa');
  });

  it('shows nothing rather than undefined for a pick with no email', () => {
    const c = new AppointmentAddPatientDemographicsComponent();
    expect(c.patientInputFormatter({ id: 'p-1' } as never)).toBe('');
  });

  it('raises the picked patient id to the parent', () => {
    const c = new AppointmentAddPatientDemographicsComponent();
    const out = selections(c);
    c.onPatientResultSelected(pick({ id: 'p-1', displayName: 'ada@example.test' }));
    expect(out).toEqual(['p-1']);
  });

  it('raises null for a pick that carries no patient id', () => {
    const c = new AppointmentAddPatientDemographicsComponent();
    const out = selections(c);

    c.onPatientResultSelected(pick({ displayName: 'ada@example.test' }));
    c.onPatientResultSelected(pick(undefined));

    expect(out).toEqual([null, null]);
  });
});
