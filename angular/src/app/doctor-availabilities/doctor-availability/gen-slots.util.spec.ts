import { BookingStatus } from '../../proxy/enums/booking-status.enum';
import type { DoctorAvailabilitySlotsPreviewDto } from '../../proxy/doctor-availabilities/models';
import {
  buildGenerateInput,
  countPreviewConflicts,
  countPreviewSlots,
  estimateSlotCount,
  exceedsLimit,
  mapPreviewToDays,
  selectedWeekdayIndices,
  type GenFormState,
} from './gen-slots.util';

function baseState(overrides: Partial<GenFormState> = {}): GenFormState {
  return {
    locationId: 'loc-1',
    mode: 'range',
    fromDate: '2026-06-15',
    toDate: '2026-06-19',
    weekdays: [false, true, true, true, true, true, false], // Mon-Fri
    selectedDates: [],
    timeRanges: [{ fromTime: '09:00', toTime: '12:00', durationOverride: null }],
    capacity: 3,
    durationMinutes: 60,
    appointmentTypeIds: [],
    ...overrides,
  };
}

describe('gen-slots.util', () => {
  describe('selectedWeekdayIndices', () => {
    it('returns the indices toggled on (0=Sun..6=Sat)', () => {
      expect(selectedWeekdayIndices([false, true, true, true, true, true, false])).toEqual([
        1, 2, 3, 4, 5,
      ]);
      expect(selectedWeekdayIndices([true, false, false, false, false, false, true])).toEqual([
        0, 6,
      ]);
    });
  });

  describe('buildGenerateInput', () => {
    it('builds the range + weekday DTO and normalizes time ranges', () => {
      const dto = buildGenerateInput(
        baseState({ timeRanges: [{ fromTime: '09:00', toTime: '12:00', durationOverride: 30 }] }),
      );
      expect(dto.fromDate).toBe('2026-06-15T00:00:00');
      expect(dto.toDate).toBe('2026-06-19T00:00:00');
      expect(dto.selectedDays).toEqual([1, 2, 3, 4, 5]);
      expect(dto.selectedDates).toBeUndefined();
      expect(dto.bookingStatusId).toBe(BookingStatus.Available);
      expect(dto.timeRanges).toEqual([
        { fromTime: '09:00:00', toTime: '12:00:00', appointmentDurationMinutes: 30 },
      ]);
    });

    it('leaves an unset time empty rather than appending seconds to nothing', () => {
      const dto = buildGenerateInput(
        baseState({ timeRanges: [{ fromTime: '', toTime: '12:00', durationOverride: null }] }),
      );
      expect(dto.timeRanges![0].fromTime).toBe('');
      expect(dto.timeRanges![0].toTime).toBe('12:00:00');
    });

    it('passes a null duration override through when blank or non-positive', () => {
      const dto = buildGenerateInput(
        baseState({ timeRanges: [{ fromTime: '09:00', toTime: '12:00', durationOverride: 0 }] }),
      );
      expect(dto.timeRanges![0].appointmentDurationMinutes).toBeNull();
    });

    it('builds the explicit-date DTO sorted, without range fields', () => {
      const dto = buildGenerateInput(
        baseState({
          mode: 'pick',
          selectedDates: ['2026-06-22', '2026-06-18', '2026-06-15'],
        }),
      );
      expect(dto.selectedDates).toEqual(['2026-06-15', '2026-06-18', '2026-06-22']);
      expect(dto.fromDate).toBeUndefined();
      expect(dto.selectedDays).toBeUndefined();
    });
  });

  describe('estimateSlotCount', () => {
    it('counts weekday-matching days times slots-per-day in range mode', () => {
      // Mon 15 .. Fri 19 = 5 weekdays; 09:00-12:00 / 60 = 3 slots -> 15.
      expect(estimateSlotCount(baseState())).toBe(15);
    });

    it('treats no weekday selection as every day', () => {
      // Mon 15 .. Sun 21 = 7 days; 3 slots -> 21.
      expect(
        estimateSlotCount(
          baseState({
            toDate: '2026-06-21',
            weekdays: [false, false, false, false, false, false, false],
          }),
        ),
      ).toBe(21);
    });

    it('counts only the picked days in explicit-date mode', () => {
      expect(
        estimateSlotCount(
          baseState({
            mode: 'pick',
            selectedDates: ['2026-06-15', '2026-06-18'],
            timeRanges: [{ fromTime: '09:00', toTime: '11:00', durationOverride: null }],
          }),
        ),
      ).toBe(4); // 2 days * 2 slots
    });

    it('honors per-range duration overrides', () => {
      // 09:00-10:00 at 30-min override = 2 slots/day * 5 days = 10.
      expect(
        estimateSlotCount(
          baseState({ timeRanges: [{ fromTime: '09:00', toTime: '10:00', durationOverride: 30 }] }),
        ),
      ).toBe(10);
    });

    it('returns 0 when there are no time ranges', () => {
      expect(estimateSlotCount(baseState({ timeRanges: [] }))).toBe(0);
    });

    // The estimate feeds exceedsLimit, so a half-filled or contradictory form must count 0,
    // never a wrong number. baseState() alone counts 15 (above), which is each test's control.
    it('counts no slots when the date range is incomplete', () => {
      expect(estimateSlotCount(baseState({ toDate: '' }))).toBe(0);
    });

    it('counts no slots when the range ends before it starts', () => {
      expect(estimateSlotCount(baseState({ fromDate: '2026-06-19', toDate: '2026-06-15' }))).toBe(
        0,
      );
    });

    it('counts no slots for a range with no duration', () => {
      expect(estimateSlotCount(baseState({ durationMinutes: 0 }))).toBe(0);
    });

    it('counts no slots for a time it cannot read', () => {
      expect(
        estimateSlotCount(
          baseState({
            timeRanges: [{ fromTime: 'ab:cd', toTime: '12:00', durationOverride: null }],
          }),
        ),
      ).toBe(0);
    });

    it('counts no slots for an inverted time range', () => {
      // Without the guard this is Math.floor(-180 / 60) per day: a NEGATIVE count.
      expect(
        estimateSlotCount(
          baseState({
            timeRanges: [{ fromTime: '12:00', toTime: '09:00', durationOverride: null }],
          }),
        ),
      ).toBe(0);
    });
  });

  describe('exceedsLimit', () => {
    it('flags counts over 5000', () => {
      expect(exceedsLimit(5000)).toBeFalse();
      expect(exceedsLimit(5001)).toBeTrue();
    });
  });

  describe('preview helpers', () => {
    const preview: DoctorAvailabilitySlotsPreviewDto[] = [
      {
        dates: '06-15-2026',
        days: 'Monday',
        doctorAvailabilities: [
          { fromTime: '09:00:00', toTime: '10:00:00', timeId: 1, isConflict: false },
          { fromTime: '10:00:00', toTime: '11:00:00', timeId: 2, isConflict: true },
        ],
      },
      {
        dates: '06-16-2026',
        days: 'Tuesday',
        doctorAvailabilities: [
          { fromTime: '09:00:00', toTime: '10:00:00', timeId: 1, isConflict: false },
        ],
      },
    ];

    it('counts total preview slots', () => {
      expect(countPreviewSlots(preview)).toBe(3);
    });

    it('counts conflicting preview slots', () => {
      expect(countPreviewConflicts(preview)).toBe(1);
    });

    it('maps the preview into per-day grid columns with labels', () => {
      const days = mapPreviewToDays(preview);
      expect(days).toHaveSize(2);
      expect(days[0].label).toBe('Mon 15');
      expect(days[0].conflicts).toBe(1);
      expect(days[0].slots[1].conflict).toBeTrue();
      expect(days[0].slots[0].timeLabel).toBe('9:00 - 10:00 AM');
      expect(days[1].label).toBe('Tue 16');
    });

    it('falls back to the raw weekday when the date cannot be parsed', () => {
      const days = mapPreviewToDays([
        { dates: 'not-a-date', days: 'Monday', doctorAvailabilities: [] },
      ]);
      expect(days[0].label).toBe('Monday');
    });
  });
});
