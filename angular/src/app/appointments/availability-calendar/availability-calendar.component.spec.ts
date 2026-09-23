import { SimpleChange } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NgbDateParserFormatter } from '@ng-bootstrap/ng-bootstrap';
import { Subject, of } from 'rxjs';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { UsDateParserFormatter } from '../../shared/us-date-parser-formatter';
import {
  AvailabilityCalendarComponent,
  AvailabilitySelection,
} from './availability-calendar.component';
import { toDateKey } from './availability-rules';

/**
 * Phase 4a (2026-08-03).
 *
 * <p>Phase 3's hard lesson drives the shape of this spec: a pure mapper tested only against itself
 * proves nothing about a third-party contract. `ngbDatepicker`'s `[markDisabled]` and `[dayTemplate]`
 * ARE the third-party contract here, so at least one test RENDERS the datepicker and asserts on the
 * DOM rather than only calling the component's helpers.</p>
 */
describe('AvailabilityCalendarComponent', () => {
  let fixture: ComponentFixture<AvailabilityCalendarComponent>;
  let component: AvailabilityCalendarComponent;
  let lookup: jasmine.Spy;

  // A date comfortably past a 3-day lead time and inside the 90-day ceiling.
  const bookable = new Date();
  bookable.setDate(bookable.getDate() + 10);
  const bookableKey = toDateKey(
    bookable.getFullYear(),
    bookable.getMonth() + 1,
    bookable.getDate(),
  );

  beforeEach(async () => {
    lookup = jasmine.createSpy('getDoctorAvailabilityLookup').and.returnValue(
      of([
        { id: 'slot-9am', availableDate: `${bookableKey}T00:00:00Z`, fromTime: '09:00' },
        { id: 'slot-2pm', availableDate: `${bookableKey}T00:00:00Z`, fromTime: '14:00' },
      ]),
    );

    await TestBed.configureTestingModule({
      imports: [AvailabilityCalendarComponent],
      providers: [
        { provide: DoctorAvailabilityService, useValue: { getDoctorAvailabilityLookup: lookup } },
        // Mirrors app.config.ts, which formats every datepicker as MM/DD/YYYY app-wide. Display
        // format is app policy; the component pins only the date ADAPTER (its own model shape).
        { provide: NgbDateParserFormatter, useClass: UsDateParserFormatter },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AvailabilityCalendarComponent);
    component = fixture.componentInstance;
    component.typeChosen = true;
    component.leadDays = 3;
    component.ceilingDays = 90;
  });

  async function loadFor(locationId: string | null): Promise<void> {
    component.locationId = locationId;
    component.appointmentTypeId = 'type-1';
    component.ngOnChanges({
      locationId: {
        currentValue: locationId,
        previousValue: null,
        firstChange: true,
        isFirstChange: () => true,
      },
    });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads availability for the selected location', async () => {
    await loadFor('loc-1');
    expect(lookup).toHaveBeenCalledWith({ locationId: 'loc-1', appointmentTypeId: 'type-1' });
  });

  it('does not call the API without a location', async () => {
    await loadFor(null);
    expect(lookup).not.toHaveBeenCalled();
  });

  it('offers a time option per slot on the chosen date, sorted and labelled', async () => {
    await loadFor('loc-1');
    component.selectedDate = bookableKey;
    component.ngOnChanges({
      selectedDate: {
        currentValue: bookableKey,
        previousValue: null,
        firstChange: false,
        isFirstChange: () => false,
      },
    });
    fixture.detectChanges();

    const options = Array.from(
      fixture.nativeElement.querySelectorAll('select option'),
    ) as HTMLOptionElement[];
    // SELECT placeholder + the two slots.
    expect(options).toHaveSize(3);
    expect(options[1].textContent?.trim()).toBe('9:00 AM');
    expect(options[2].textContent?.trim()).toBe('2:00 PM');
  });

  it('emits the slot id when a time is chosen', async () => {
    await loadFor('loc-1');
    component.selectedDate = bookableKey;
    component.ngOnChanges({
      selectedDate: {
        currentValue: bookableKey,
        previousValue: null,
        firstChange: false,
        isFirstChange: () => false,
      },
    });

    let emitted: AvailabilitySelection | undefined;
    component.slotSelected.subscribe((value) => (emitted = value));
    (component as unknown as { onTimePicked: (v: string | null) => void }).onTimePicked('14:00');

    expect(emitted).toEqual({
      date: bookableKey,
      time: '14:00',
      doctorAvailabilityId: 'slot-2pm',
    });
  });

  /**
   * Regression for defect #6 (2026-08-03): the picked date rendered as an EMPTY input on every one
   * of three binding mechanisms, because each fed an `NgbDateStruct` to an `NgbDateAdapter<string>`.
   * No spec asserted the DISPLAYED text, so 452 green specs shipped an unusable picker. Asserting
   * `input.value` is the only assertion that fails on the wrong model shape -- the component's own
   * state, the emitted output and the time options were all correct throughout.
   */
  it('DISPLAYS the selected date in the input', async () => {
    await loadFor('loc-1');
    component.selectedDate = bookableKey;
    component.ngOnChanges({
      selectedDate: {
        currentValue: bookableKey,
        previousValue: null,
        firstChange: false,
        isFirstChange: () => false,
      },
    });
    fixture.detectChanges();

    const pad = (n: number) => String(n).padStart(2, '0');
    const expected = `${pad(bookable.getMonth() + 1)}/${pad(
      bookable.getDate(),
    )}/${bookable.getFullYear()}`;

    const input = fixture.nativeElement.querySelector('input[ngbDatepicker]') as HTMLInputElement;
    expect(input.value).toBe(expected);
  });

  // ---- The contract test: real DOM, not just our helpers ----
  it('RENDERS the datepicker and disables days that have no availability', async () => {
    await loadFor('loc-1');

    // Open the real ngbDatepicker so it applies [markDisabled] itself.
    const input = fixture.nativeElement.querySelector('input[ngbDatepicker]') as HTMLInputElement;
    expect(input).withContext('the datepicker input must render').toBeTruthy();
    input.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const dayCells = Array.from(
      fixture.nativeElement.querySelectorAll('.ngb-dp-day'),
    ) as HTMLElement[];
    expect(dayCells.length)
      .withContext('the datepicker must actually render day cells')
      .toBeGreaterThan(0);

    const disabled = dayCells.filter((cell) => cell.classList.contains('disabled'));
    expect(disabled.length)
      .withContext('most days have no availability, so ngbDatepicker must disable them')
      .toBeGreaterThan(0);

    // And the availability highlight our dayTemplate emits must appear for the one bookable date.
    const highlighted = fixture.nativeElement.querySelectorAll(
      '.appointment-date-day.available-day',
    );
    expect(highlighted.length)
      .withContext('the bookable date must be highlighted via the day template')
      .toBeGreaterThan(0);
  });

  /**
   * What the user does in the widgets, and the reload edge cases.
   *
   * <p>The date and time controls are driven with `setValue`, which is what the widgets do:
   * `valueChanges` fires synchronously, so the component's own subscriptions run exactly as
   * they do on a real pick, with no timers involved. Parent-driven writes use
   * `emitEvent: false` and must NOT come back out as a selection -- two tests pin that.</p>
   */
  describe('interaction and reload edge cases', () => {
    interface Probe {
      dateControl: { value: string | null; setValue(v: string | null): void };
      timeControl: { value: string | null; setValue(v: string | null): void };
      timeOptions: { value: string; doctorAvailabilityId: string }[];
      isLoading: boolean;
      onDatePicked(v: unknown): void;
      onClear(): void;
    }
    const probe = () => component as unknown as Probe;

    function emissions(): AvailabilitySelection[] {
      const out: AvailabilitySelection[] = [];
      component.slotSelected.subscribe((v) => out.push(v));
      return out;
    }

    function slot(id: string, fromTime: string) {
      return { id, availableDate: `${bookableKey}T00:00:00Z`, fromTime };
    }

    it('writes a parent-set time into the time control without echoing it back', () => {
      const out = emissions();
      component.selectedTime = '09:00';
      component.ngOnChanges({ selectedTime: new SimpleChange(null, '09:00', false) });

      expect(probe().timeControl.value).toBe('09:00');
      expect(out).withContext('a parent write must not look like a user pick').toEqual([]);
    });

    it('offers the times for a date the user picks, and reports the date with no time yet', async () => {
      await loadFor('loc-1');
      const out = emissions();

      probe().dateControl.setValue(bookableKey);

      expect(probe().timeOptions.map((o) => o.value)).toEqual(['09:00', '14:00']);
      expect(out).toEqual([{ date: bookableKey, time: null, doctorAvailabilityId: null }]);
    });

    it('empties the times when the picked date is cleared, and reports nothing', async () => {
      await loadFor('loc-1');
      probe().dateControl.setValue(bookableKey);
      const out = emissions();

      probe().dateControl.setValue(null);

      expect(probe().timeOptions).toEqual([]);
      expect(out).toEqual([]);
    });

    it('reports the date with no time when the user clears the time', () => {
      component.selectedDate = bookableKey;
      const out = emissions();

      probe().timeControl.setValue(null);

      expect(out).toEqual([{ date: bookableKey, time: null, doctorAvailabilityId: null }]);
    });

    it('clears both widgets and announces it, without reporting a selection', async () => {
      await loadFor('loc-1');
      component.selectedDate = bookableKey;
      component.ngOnChanges({ selectedDate: new SimpleChange(null, bookableKey, false) });
      let cleared = 0;
      component.dateCleared.subscribe(() => (cleared += 1));
      const out = emissions();

      probe().onClear();

      expect(probe().dateControl.value).toBeNull();
      expect(probe().timeControl.value).toBeNull();
      expect(probe().timeOptions).toEqual([]);
      expect(cleared).toBe(1);
      expect(out).toEqual([]);
    });

    /**
     * The pinned adapter only ever emits `YYYY-MM-DD`, so these shapes arrive only from a host
     * that supplies a different adapter. The normaliser exists so such a host degrades instead
     * of throwing; these pin that it does.
     */
    it('normalises the other date shapes a differently-adapted host could send', () => {
      const out = emissions();

      probe().onDatePicked({ year: 2026, month: 11, day: 3 });
      probe().onDatePicked('11/3/2026');

      expect(out.map((e) => e.date)).toEqual(['2026-11-03', '2026-11-03']);
    });

    it('treats an unreadable or incomplete date as no date', () => {
      const out = emissions();

      probe().onDatePicked('garbage');
      probe().onDatePicked({ year: 2026, month: 0, day: 3 });

      expect(out).toEqual([]);
      expect(probe().timeOptions).toEqual([]);
    });

    it('discards a response that arrives after a newer request was made', async () => {
      // Answered out of order: the newer request first, then the stale one. Applying the
      // stale one would offer the previous location's times for the new location.
      const first = new Subject<unknown[]>();
      const second = new Subject<unknown[]>();
      lookup.and.returnValues(first, second);
      component.locationId = 'loc-1';
      component.ngOnChanges({ locationId: new SimpleChange(null, 'loc-1', true) });
      component.locationId = 'loc-2';
      component.ngOnChanges({ locationId: new SimpleChange('loc-1', 'loc-2', false) });

      second.next([slot('slot-new', '10:00')]);
      await fixture.whenStable();
      first.next([slot('slot-old', '08:00')]);
      await fixture.whenStable();

      probe().dateControl.setValue(bookableKey);
      expect(probe().timeOptions.map((o) => o.doctorAvailabilityId)).toEqual(['slot-new']);
      expect(probe().isLoading).toBeFalse();
    });

    it('drops a chosen date that the reloaded availability no longer offers', async () => {
      component.selectedDate = '2000-01-01';
      const out = emissions();

      await loadFor('loc-1');

      expect(out).toEqual([{ date: null, time: null, doctorAvailabilityId: null }]);
      expect(probe().timeOptions).toEqual([]);
    });

    it('keeps a chosen date that is still offered and lists its times', async () => {
      component.selectedDate = bookableKey;
      const out = emissions();

      await loadFor('loc-1');

      expect(probe().timeOptions.map((o) => o.value)).toEqual(['09:00', '14:00']);
      expect(out).toEqual([]);
    });
  });
});
