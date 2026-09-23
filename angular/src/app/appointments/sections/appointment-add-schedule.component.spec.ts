import { FormControl, FormGroup } from '@angular/forms';

import { AppointmentAddScheduleComponent } from './appointment-add-schedule.component';

/**
 * The booking form's schedule section: the adapter between the availability calendar and the
 * parent's form controls.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new` over a bare form holding the
 * three controls it reads and writes.</p>
 *
 * <p>The event rule is the part worth pinning. The parent subscribes to `appointmentDate` to apply
 * its role-horizon interception, so the date MUST be written with events; time and slot id are
 * written silently because no parent rule depends on them.</p>
 */
describe('AppointmentAddScheduleComponent', () => {
  interface Probe {
    form: FormGroup;
    selectedDateKey: string | null;
    onSlotSelected(selection: {
      date: string | null;
      time: string | null;
      doctorAvailabilityId: string | null;
    }): void;
  }

  function create(date: string | null = null): Probe {
    const c = new AppointmentAddScheduleComponent() as unknown as Probe;
    c.form = new FormGroup({
      appointmentDate: new FormControl<string | null>(date),
      appointmentTime: new FormControl<string | null>(null),
      doctorAvailabilityId: new FormControl<string | null>(null),
    });
    return c;
  }

  describe('the date key handed to the calendar', () => {
    it('takes the date part of a stored date-time', () => {
      expect(create('2026-11-03T00:00:00').selectedDateKey).toBe('2026-11-03');
    });

    it('is null for no date', () => {
      expect(create(null).selectedDateKey).toBeNull();
    });

    it('is null for a value that is not a year-first date', () => {
      expect(create('11/03/2026').selectedDateKey).toBeNull();
      expect(create('2026-11').selectedDateKey).toBeNull();
    });
  });

  describe('a slot chosen in the calendar', () => {
    it('writes the date, time and slot id onto the form', () => {
      const c = create();

      c.onSlotSelected({ date: '2026-11-03', time: '09:00', doctorAvailabilityId: 'slot-1' });

      expect(c.form.getRawValue()).toEqual({
        appointmentDate: '2026-11-03',
        appointmentTime: '09:00',
        doctorAvailabilityId: 'slot-1',
      });
    });

    it('announces the date change and writes time and slot silently', () => {
      const c = create();
      const events: string[] = [];
      for (const name of ['appointmentDate', 'appointmentTime', 'doctorAvailabilityId']) {
        c.form.get(name)!.valueChanges.subscribe(() => events.push(name));
      }

      c.onSlotSelected({ date: '2026-11-03', time: '09:00', doctorAvailabilityId: 'slot-1' });

      expect(events).toEqual(['appointmentDate']);
    });
  });
});
