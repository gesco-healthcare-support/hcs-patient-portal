import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NgbDateParserFormatter } from '@ng-bootstrap/ng-bootstrap';
import { of } from 'rxjs';
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
    expect(options.length).toBe(3);
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
});
