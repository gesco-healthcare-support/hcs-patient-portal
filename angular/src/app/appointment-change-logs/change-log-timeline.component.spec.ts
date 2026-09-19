import { TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { Router } from '@angular/router';
import { ChangeLogTimelineComponent } from './change-log-timeline.component';
import { changeTypeMeta } from './clg-log.util';

/**
 * The shared change-log timeline.
 *
 * `rows` is a SIGNAL INPUT, so it can only be populated through
 * `componentRef.setInput` -- constructing the class directly leaves it at its default and
 * every grouping assertion would then pass against an empty list. The rows are set from a
 * `const` and the fixture is never change-detected: building a fresh object inside a getter
 * that a signal input feeds hangs change detection, and a hang logs no test name at all.
 *
 * The grouping itself belongs to clg-log.util and is tested in clg-log.util.spec.ts. What is
 * asserted here is the WIRING -- that the component's `entries` actually reflects its input --
 * and the local interaction state. Re-testing the util through the component would duplicate
 * coverage and pin the same logic twice.
 */
describe('ChangeLogTimelineComponent', () => {
  interface Probe {
    entries(): Array<{ key: string; diffs: unknown[] }>;
    meta(changeType: string | null | undefined): unknown;
    isOpen(key: string): boolean;
    toggle(key: string): void;
    openAppointment(appointmentId: string | null | undefined, event: Event): void;
  }

  let navigateByUrl: jasmine.Spy;

  const base = {
    appointmentId: 'appt-1',
    entityType: 'Appointment',
    changeType: 'Updated',
    changeTime: '2026-01-02T10:00:00',
    valueRedacted: false,
  };
  // Same appointment, entity, change type AND timestamp: one save, two fields.
  const twoFieldsOneSave = [
    { ...base, propertyName: 'AppointmentDate', oldValue: '2026-01-01', newValue: '2026-01-02' },
    { ...base, propertyName: 'LocationId', oldValue: 'loc-1', newValue: 'loc-2' },
  ];
  // A different timestamp makes it a different save.
  const aSecondSave = [
    twoFieldsOneSave[0],
    { ...base, changeTime: '2026-01-03T11:00:00', propertyName: 'Status' },
  ];

  beforeEach(() => {
    navigateByUrl = jasmine.createSpy('navigateByUrl').and.returnValue(Promise.resolve(true));
    TestBed.configureTestingModule({
      imports: [ChangeLogTimelineComponent],
      providers: [{ provide: Router, useValue: { navigateByUrl } }],
    });
  });

  function probe(rows?: unknown[]): Probe {
    const fixture = TestBed.createComponent(ChangeLogTimelineComponent);
    if (rows) {
      (fixture.componentRef as ComponentRef<unknown>).setInput('rows', rows);
    }
    return fixture.componentInstance as unknown as Probe;
  }

  describe('entries follow the input', () => {
    it('is empty when no rows are supplied', () => {
      expect(probe().entries()).toEqual([]);
    });

    it('collapses the fields of one save into a single entry', () => {
      const entries = probe(twoFieldsOneSave).entries();
      expect(entries).toHaveSize(1);
      expect(entries[0].diffs).toHaveSize(2);
    });

    // Positive control for the grouping above: rows that are NOT one save stay apart, so the
    // single entry above is grouping rather than the component dropping rows on the floor.
    it('keeps separate saves as separate entries', () => {
      expect(probe(aSecondSave).entries()).toHaveSize(2);
    });
  });

  it('delegates change-type presentation to the shared util', () => {
    expect(probe().meta('Updated')).toEqual(changeTypeMeta('Updated'));
    expect(probe().meta(null)).toEqual(changeTypeMeta(null));
  });

  describe('expanding an entry', () => {
    it('starts closed', () => {
      expect(probe().isOpen('any-key')).toBeFalse();
    });

    it('opens on the first toggle', () => {
      const cmp = probe();
      cmp.toggle('key-1');
      expect(cmp.isOpen('key-1')).toBeTrue();
    });

    it('closes again on a second toggle', () => {
      const cmp = probe();
      cmp.toggle('key-1');
      cmp.toggle('key-1');
      expect(cmp.isOpen('key-1')).toBeFalse();
    });

    it('tracks each entry separately', () => {
      const cmp = probe();
      cmp.toggle('key-1');
      expect(cmp.isOpen('key-1')).toBeTrue();
      expect(cmp.isOpen('key-2')).toBeFalse();
    });
  });

  describe('opening the appointment', () => {
    function clickEvent(): Event {
      const event = new MouseEvent('click');
      spyOn(event, 'stopPropagation');
      return event;
    }

    it('navigates to the appointment', () => {
      const event = clickEvent();
      probe().openAppointment('appt-1', event);
      expect(navigateByUrl).toHaveBeenCalledWith('/appointments/view/appt-1');
    });

    it('stops the click reaching the surrounding entry toggle', () => {
      const event = clickEvent();
      probe().openAppointment('appt-1', event);
      expect(event.stopPropagation).toHaveBeenCalled();
    });

    it('does not navigate without an appointment id', () => {
      const event = clickEvent();
      probe().openAppointment(null, event);
      expect(navigateByUrl).not.toHaveBeenCalled();
    });

    // The ordering matters: stopPropagation happens BEFORE the id guard, so a row with no
    // appointment still must not fall through to the toggle underneath it.
    it('still stops propagation when there is no appointment id', () => {
      const event = clickEvent();
      probe().openAppointment(undefined, event);
      expect(event.stopPropagation).toHaveBeenCalled();
    });
  });
});
