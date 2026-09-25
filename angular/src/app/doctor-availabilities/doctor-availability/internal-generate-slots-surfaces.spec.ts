import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { SystemParametersService } from '../../proxy/system-parameters-controllers/system-parameters.service';
import { InternalGenerateSlotsComponent } from './internal-generate-slots.component';
import { isoDate } from './avail-grid.util';

/**
 * Internal Scheduling -- generate slots.
 *
 * <p>It sat at 99 of 147 lines uncovered. An existing spec (sweep #641) covers pick-mode
 * `earliestGeneratedIso` and `setRange`; neither is repeated here.</p>
 *
 * <p>The behaviour worth holding is the booking lead time. A slot dated inside the tenant's lead
 * window can NEVER be booked, so generating one produces calendar rows that look available and
 * are not. The component warns before that happens, and the warning has to stay silent when the
 * lead time is unknown -- a fetch that 403s leaves it at zero, and a wrong warning is worse than
 * none. The other half is the generate guard chain: location, picked days, and the slot ceiling.</p>
 *
 * <p>Dates are computed relative to today rather than hardcoded, because the component captures
 * `today` at construction and a fixed date would rot.</p>
 *
 * <p>All locations, appointment types and identifiers below are synthetic.</p>
 */
describe('InternalGenerateSlotsComponent surfaces', () => {
  let service: Record<string, jasmine.Spy>;
  let systemParams: { get: jasmine.Spy };
  let toaster: {
    success: jasmine.Spy;
    warn: jasmine.Spy;
    info: jasmine.Spy;
    error: jasmine.Spy;
  };
  let router: { navigateByUrl: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  const today = new Date();

  function addDays(n: number): string {
    const d = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    d.setDate(d.getDate() + n);
    return isoDate(d);
  }

  function create(): Probe {
    service = {
      getLocationLookup: jasmine.createSpy('getLocationLookup').and.returnValue(of({ items: [] })),
      getAppointmentTypeLookup: jasmine
        .createSpy('getAppointmentTypeLookup')
        .and.returnValue(of({ items: [] })),
      generatePreview: jasmine.createSpy('generatePreview').and.returnValue(of([])),
      createRange: jasmine
        .createSpy('createRange')
        .and.returnValue(of({ insertedCount: 0, skippedConflictCount: 0 })),
    };
    systemParams = { get: jasmine.createSpy('get').and.returnValue(of({})) };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      info: jasmine.createSpy('info'),
      error: jasmine.createSpy('error'),
    };
    router = {
      navigateByUrl: jasmine.createSpy('navigateByUrl').and.returnValue(Promise.resolve(true)),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: DoctorAvailabilityService, useValue: service },
        { provide: SystemParametersService, useValue: systemParams },
        { provide: ToasterService, useValue: toaster },
        { provide: Router, useValue: router },
      ],
    });

    return TestBed.createComponent(InternalGenerateSlotsComponent)
      .componentInstance as unknown as Probe;
  }

  /**
   * A one-day preview carrying `slots` entries, `conflicts` of which collide.
   *
   * The field names matter and are not guessable: the backend groups by day and returns
   * `doctorAvailabilities` with an `isConflict` flag, which is what countPreviewSlots and
   * countPreviewConflicts reduce over. A fixture with plausible-but-wrong names counts zero
   * slots, which silently makes canSubmit() false and turns every submit test into a no-op.
   */
  function preview(slots: number, conflicts: number) {
    return [
      {
        dates: '09-18-2026',
        days: 'Friday',
        doctorAvailabilities: Array.from({ length: slots }, (_, i) => ({
          timeId: `time-${i}`,
          fromTime: '09:00',
          toTime: '09:30',
          isConflict: i < conflicts,
        })),
      },
    ];
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('initial load', () => {
    it('maps the location lookup and clears the loading flag', () => {
      const c = create();
      service['getLocationLookup'].and.returnValue(
        of({ items: [{ id: 'l-1', displayName: 'Encino' }] }),
      );
      c.ngOnInit();
      expect(c.locations()).toEqual([{ id: 'l-1', name: 'Encino' }]);
      expect(c.loading()).toBeFalse();
    });

    it('clears the loading flag even when the location lookup fails', () => {
      // Otherwise the form sits behind a spinner with no way forward.
      const c = create();
      service['getLocationLookup'].and.returnValue(throwError(() => ({ status: 500 })));
      c.ngOnInit();
      expect(c.loading()).toBeFalse();
    });

    it('maps the appointment-type lookup', () => {
      const c = create();
      service['getAppointmentTypeLookup'].and.returnValue(
        of({ items: [{ id: 't-1', displayName: 'AME' }] }),
      );
      c.ngOnInit();
      expect(c.appointmentTypes()).toEqual([{ id: 't-1', name: 'AME' }]);
    });

    it('leaves the type list empty when its lookup fails', () => {
      const c = create();
      service['getAppointmentTypeLookup'].and.returnValue(throwError(() => ({ status: 403 })));
      c.ngOnInit();
      expect(c.appointmentTypes()).toEqual([]);
    });

    it('substitutes empty strings for a lookup row missing its fields', () => {
      const c = create();
      service['getLocationLookup'].and.returnValue(of({ items: [{}] }));
      c.ngOnInit();
      expect(c.locations()).toEqual([{ id: '', name: '' }]);
    });

    it('reads the booking lead time from the system parameters', () => {
      const c = create();
      systemParams.get.and.returnValue(of({ appointmentLeadTime: 5 }));
      c.ngOnInit();
      expect(c.leadTimeDays()).toBe(5);
    });

    it('treats an absent lead time as zero, meaning unknown', () => {
      const c = create();
      systemParams.get.and.returnValue(of({}));
      c.ngOnInit();
      expect(c.leadTimeDays()).toBe(0);
    });

    it('leaves the lead time unknown when the parameter read is refused', () => {
      // Read access is permission-gated, so a 403 is an ordinary outcome here.
      const c = create();
      systemParams.get.and.returnValue(throwError(() => ({ status: 403 })));
      c.ngOnInit();
      expect(c.leadTimeDays()).toBe(0);
    });
  });

  describe('the booking lead-time warning', () => {
    it('says nothing while the lead time is unknown', () => {
      /**
       * A wrong warning is worse than none: it would tell staff their dates are
       * unbookable on every tenant whose parameter read is not permitted.
       */
      const c = create();
      c.leadTimeDays.set(0);
      expect(c.earliestBookableIso()).toBe('');
      expect(c.leadTimeWarning()).toBe('');
    });

    it('puts the earliest bookable date at today plus the lead time', () => {
      const c = create();
      c.leadTimeDays.set(7);
      expect(c.earliestBookableIso()).toBe(addDays(7));
    });

    it('says nothing when every generated date is already outside the window', () => {
      const c = create();
      c.leadTimeDays.set(3);
      c.mode.set('pick');
      c.selectedDates.set([addDays(10)]);
      expect(c.leadTimeWarning()).toBe('');
    });

    it('warns and names the earliest bookable date when a generated date is inside it', () => {
      const c = create();
      c.leadTimeDays.set(7);
      c.mode.set('pick');
      c.selectedDates.set([addDays(1)]);

      const warning = c.leadTimeWarning();
      expect(warning).toContain('7-day booking lead time');
      expect(warning).toContain(addDays(7));
    });

    it('says nothing when a generated date falls exactly on the earliest bookable day', () => {
      // The boundary is inclusive: a slot dated ON the earliest bookable day is bookable.
      const c = create();
      c.leadTimeDays.set(4);
      c.mode.set('pick');
      c.selectedDates.set([addDays(4)]);
      expect(c.leadTimeWarning()).toBe('');
    });

    it('says nothing when nothing is selected to generate', () => {
      const c = create();
      c.leadTimeDays.set(7);
      c.mode.set('pick');
      c.selectedDates.set([]);
      expect(c.leadTimeWarning()).toBe('');
    });
  });

  describe('the earliest generated date in range mode', () => {
    it('finds the first selected weekday on or after the start of the range', () => {
      const c = create();
      c.mode.set('range');
      c.fromDate.set('2026-09-14');
      c.toDate.set('2026-09-20');
      // 2026-09-14 is a Monday; select Wednesday only.
      const weekdays = [false, false, false, true, false, false, false];
      c.weekdays.set(weekdays);
      expect(c.earliestGeneratedIso()).toBe('2026-09-16');
    });

    it('returns the start date itself when its weekday is selected', () => {
      const c = create();
      c.mode.set('range');
      c.fromDate.set('2026-09-14');
      c.toDate.set('2026-09-20');
      c.weekdays.set([false, true, false, false, false, false, false]);
      expect(c.earliestGeneratedIso()).toBe('2026-09-14');
    });

    it('returns nothing when no selected weekday falls inside the range', () => {
      const c = create();
      c.mode.set('range');
      c.fromDate.set('2026-09-14');
      c.toDate.set('2026-09-15');
      // Only Sunday selected; the range is Monday-Tuesday.
      c.weekdays.set([true, false, false, false, false, false, false]);
      expect(c.earliestGeneratedIso()).toBeNull();
    });

    it('returns nothing when the range runs backwards', () => {
      const c = create();
      c.mode.set('range');
      c.fromDate.set('2026-09-20');
      c.toDate.set('2026-09-14');
      expect(c.earliestGeneratedIso()).toBeNull();
    });

    it('returns nothing when either end of the range is blank', () => {
      const c = create();
      c.mode.set('range');
      c.fromDate.set('');
      c.toDate.set('2026-09-20');
      expect(c.earliestGeneratedIso()).toBeNull();

      c.fromDate.set('2026-09-14');
      c.toDate.set('');
      expect(c.earliestGeneratedIso()).toBeNull();
    });
  });

  describe('the month calendar', () => {
    it('pads the first week so day one lands on its real weekday', () => {
      const c = create();
      const cursor = new Date(2027, 1, 1);
      c.monthCursor.set(cursor);

      const cells = c.monthCells();
      const leading = cells.filter((x: { blank: boolean }) => x.blank).length;
      expect(leading).toBe(cursor.getDay());
      expect(cells[leading].day).toBe(1);
    });

    it('emits one cell per day of the month', () => {
      const c = create();
      c.monthCursor.set(new Date(2027, 1, 1));
      const days = c.monthCells().filter((x: { blank: boolean }) => !x.blank);
      expect(days.length).toBe(new Date(2027, 2, 0).getDate());
    });

    it('disables days already in the past', () => {
      const c = create();
      c.monthCursor.set(new Date(2020, 0, 1));
      const days = c.monthCells().filter((x: { blank: boolean }) => !x.blank);
      expect(days.every((x: { disabled: boolean }) => x.disabled)).toBeTrue();
    });

    it('marks a picked day as selected', () => {
      const c = create();
      const pick = addDays(1);
      c.selectedDates.set([pick]);
      c.monthCursor.set(new Date(today.getFullYear(), today.getMonth(), 1));

      const cell = c.monthCells().find((x: { iso: string }) => x.iso === pick) as
        | { selected: boolean }
        | undefined;
      // The picked day may roll into next month, in which case the cursor month has no
      // cell for it -- assert only when it is on screen.
      if (cell) {
        expect(cell.selected).toBeTrue();
      }
      expect(c.selectedDates()).toEqual([pick]);
    });

    it('labels the month and year', () => {
      const c = create();
      c.monthCursor.set(new Date(2027, 1, 1));
      expect(c.monthLabel()).toBe('February 2027');
    });

    it('steps the cursor forward and back, crossing a year boundary', () => {
      const c = create();
      c.monthCursor.set(new Date(2026, 11, 1));
      c.shiftMonth(1);
      expect(c.monthLabel()).toBe('January 2027');
      c.shiftMonth(-1);
      expect(c.monthLabel()).toBe('December 2026');
    });

    it('adds a day on the first click and removes it on the second', () => {
      const c = create();
      const cell = { blank: false, iso: addDays(2), day: 2, disabled: false, selected: false };
      c.toggleDate(cell);
      expect(c.selectedDates()).toEqual([cell.iso]);
      c.toggleDate(cell);
      expect(c.selectedDates()).toEqual([]);
    });

    it('ignores a padding cell and a past day', () => {
      const c = create();
      c.toggleDate({ blank: true, iso: '', day: 0, disabled: true, selected: false });
      c.toggleDate({ blank: false, iso: '2020-01-01', day: 1, disabled: true, selected: false });
      expect(c.selectedDates()).toEqual([]);
    });

    it('clears every picked day', () => {
      const c = create();
      c.selectedDates.set([addDays(1), addDays(2)]);
      c.clearDates();
      expect(c.selectedDates()).toEqual([]);
    });
  });

  describe('form mutations', () => {
    it('discards a stale preview when the pattern changes', () => {
      // The preview was computed for the other mode; showing it beside the new form
      // would report slots the current settings would not generate.
      const c = create();
      c.preview.set(preview(2, 0));
      c.setMode('pick');
      expect(c.preview()).toBeNull();
      expect(c.mode()).toBe('pick');
    });

    it('flips exactly one weekday', () => {
      const c = create();
      const before = [...c.weekdays()];
      c.toggleWeekday(0);
      expect(c.weekdays()[0]).toBe(!before[0]);
      expect(c.weekdays().slice(1)).toEqual(before.slice(1));
    });

    it('appends a second time range with its own defaults', () => {
      const c = create();
      c.addRange();
      expect(c.timeRanges().length).toBe(2);
      expect(c.timeRanges()[1]).toEqual({
        fromTime: '13:00',
        toTime: '16:00',
        durationOverride: null,
      });
    });

    it('removes the addressed range', () => {
      const c = create();
      c.addRange();
      c.removeRange(0);
      expect(c.timeRanges().length).toBe(1);
      expect(c.timeRanges()[0].fromTime).toBe('13:00');
    });

    it('refuses to remove the last remaining range', () => {
      // A form with no time range can generate nothing and offers no way back.
      const c = create();
      c.removeRange(0);
      expect(c.timeRanges().length).toBe(1);
    });

    it('toggles an appointment type on and off', () => {
      const c = create();
      expect(c.isTypeOn('t-1')).toBeFalse();
      c.toggleType('t-1');
      expect(c.isTypeOn('t-1')).toBeTrue();
      c.toggleType('t-1');
      expect(c.isTypeOn('t-1')).toBeFalse();
    });

    it('keeps other selected types when one is toggled off', () => {
      const c = create();
      c.toggleType('t-1');
      c.toggleType('t-2');
      c.toggleType('t-1');
      expect(c.selectedTypeIds()).toEqual(['t-2']);
    });

    it('restores the defaults and says so', () => {
      const c = create();
      c.addRange();
      c.toggleType('t-1');
      c.selectedDates.set([addDays(1)]);
      c.preview.set(preview(1, 0));

      c.reset();

      expect(c.preview()).toBeNull();
      expect(c.timeRanges()).toEqual([
        { fromTime: '08:30', toTime: '11:30', durationOverride: null },
      ]);
      expect(c.selectedTypeIds()).toEqual([]);
      expect(c.selectedDates()).toEqual([]);
      expect(toaster.info).toHaveBeenCalled();
    });
  });

  describe('previewing', () => {
    it('refuses without a location and says which field is missing', () => {
      const c = create();
      c.genPreview();
      expect(toaster.warn).toHaveBeenCalledWith('Select a location first.');
      expect(service['generatePreview']).not.toHaveBeenCalled();
    });

    it('refuses in pick mode when no day is chosen', () => {
      const c = create();
      c.locationId.set('l-1');
      c.mode.set('pick');
      c.selectedDates.set([]);
      c.genPreview();
      expect(toaster.warn).toHaveBeenCalledWith('Pick at least one day on the calendar.');
      expect(service['generatePreview']).not.toHaveBeenCalled();
    });

    it('refuses a request that would breach the slot ceiling', () => {
      /**
       * The ceiling exists because the server expands the range row by row; a range
       * chosen carelessly can ask for tens of thousands of rows in one call.
       */
      const c = create();
      c.locationId.set('l-1');
      c.mode.set('range');
      c.fromDate.set('2026-01-01');
      c.toDate.set('2030-12-31');
      c.capacity.set(50);
      c.durationMinutes.set(5);
      expect(c.overLimit()).withContext('fixture must actually breach the limit').toBeTrue();

      c.genPreview();
      expect(service['generatePreview']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('stores the preview and releases the busy flag', () => {
      const c = create();
      c.locationId.set('l-1');
      service['generatePreview'].and.returnValue(of(preview(3, 1)));
      c.genPreview();
      expect(c.preview()?.length).toBe(1);
      expect(c.isBusy()).toBeFalse();
    });

    it('treats a null preview payload as an empty preview, not as no preview', () => {
      // preview() === null is what "nothing previewed yet" means, and it gates submit.
      const c = create();
      c.locationId.set('l-1');
      service['generatePreview'].and.returnValue(of(null));
      c.genPreview();
      expect(c.preview()).toEqual([]);
    });

    it('releases the busy flag when the preview request fails', () => {
      const c = create();
      c.locationId.set('l-1');
      service['generatePreview'].and.returnValue(throwError(() => ({ status: 500 })));
      c.genPreview();
      expect(c.isBusy()).toBeFalse();
    });

    it('discards the preview when it is cancelled', () => {
      const c = create();
      c.preview.set(preview(1, 0));
      c.cancelPreview();
      expect(c.preview()).toBeNull();
    });
  });

  describe('the preview summary', () => {
    it('counts total slots, conflicts and what is left to create', () => {
      const c = create();
      c.preview.set(preview(5, 2));
      expect(c.totalSlots()).toBe(5);
      expect(c.conflicts()).toBe(2);
      expect(c.creatable()).toBe(3);
    });

    it('allows submitting only when something would actually be created', () => {
      const c = create();
      expect(c.canSubmit()).withContext('nothing previewed yet').toBeFalse();

      c.preview.set(preview(2, 2));
      expect(c.canSubmit()).withContext('every slot conflicts').toBeFalse();

      c.preview.set(preview(2, 1));
      expect(c.canSubmit()).toBeTrue();
    });

    it('clamps the preview column count between one and seven', () => {
      const c = create();
      c.preview.set([]);
      expect(c.previewCols()).toBe(1);
    });
  });

  describe('submitting', () => {
    function ready(c: Probe): void {
      c.locationId.set('l-1');
      c.preview.set(preview(2, 0));
    }

    it('does nothing without a preview', () => {
      const c = create();
      c.submit();
      expect(service['createRange']).not.toHaveBeenCalled();
    });

    it('does nothing while another request is in flight', () => {
      const c = create();
      ready(c);
      c.isBusy.set(true);
      c.submit();
      expect(service['createRange']).not.toHaveBeenCalled();
    });

    it('reports the created count on a clean run', () => {
      const c = create();
      ready(c);
      service['createRange'].and.returnValue(of({ insertedCount: 12, skippedConflictCount: 0 }));
      c.submit();
      expect(toaster.success).toHaveBeenCalledWith('12 slot(s) created.');
    });

    it('reports what was skipped when the server auto-skipped conflicts', () => {
      // The server re-expands and skips collisions, so created and requested differ;
      // reporting only the created count would look like silent data loss.
      const c = create();
      ready(c);
      service['createRange'].and.returnValue(of({ insertedCount: 9, skippedConflictCount: 3 }));
      c.submit();
      expect(toaster.success).toHaveBeenCalledWith('9 slot(s) created; 3 conflict(s) skipped.');
    });

    it('treats absent counts as zero rather than printing undefined', () => {
      const c = create();
      ready(c);
      service['createRange'].and.returnValue(of({}));
      c.submit();
      expect(toaster.success).toHaveBeenCalledWith('0 slot(s) created.');
    });

    it('clears the preview and returns to the availability list', () => {
      const c = create();
      ready(c);
      c.submit();
      expect(c.preview()).toBeNull();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/doctor-management/doctor-availabilities');
    });

    it('releases the busy flag and stays put when the create fails', () => {
      const c = create();
      ready(c);
      service['createRange'].and.returnValue(throwError(() => ({ status: 500 })));
      c.submit();
      expect(c.isBusy()).toBeFalse();
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });
  });
});
