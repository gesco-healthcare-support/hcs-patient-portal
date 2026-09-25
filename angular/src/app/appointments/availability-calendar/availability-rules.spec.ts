import {
  buildAvailableDateKeys,
  daysFromTodayKey,
  isBeforeMinimumBookingDateKey,
  isBeyondCeilingKey,
  isSelectableDate,
  toDateKey,
  toDateKeyFromApi,
} from './availability-rules';

/**
 * Phase 4a (2026-08-03) -- the booking availability rules, lifted verbatim out of
 * AppointmentAddComponent so the reschedule flow (4b) can apply the SAME rules instead of
 * duplicating them.
 *
 * These decide whether a date can be booked, so each edge is pinned individually. Two are easy to
 * get wrong and are called out below: the 90-day ceiling applies to EVERY role (the 60-day external
 * horizon is an interception on selection, NOT a disable), and "no availability loaded" must
 * disable everything rather than allow everything.
 */
describe('availability-rules', () => {
  // Local midnight, matching the production helpers which deliberately avoid UTC.
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const keyOf = (offsetDays: number): string => {
    const d = new Date(today);
    d.setDate(d.getDate() + offsetDays);
    return toDateKey(d.getFullYear(), d.getMonth() + 1, d.getDate());
  };
  const structOf = (offsetDays: number) => {
    const d = new Date(today);
    d.setDate(d.getDate() + offsetDays);
    return { year: d.getFullYear(), month: d.getMonth() + 1, day: d.getDate() };
  };

  describe('toDateKey', () => {
    it('zero-pads to YYYY-MM-DD', () => {
      expect(toDateKey(2026, 8, 3)).toBe('2026-08-03');
      expect(toDateKey(2026, 12, 25)).toBe('2026-12-25');
    });
  });

  describe('toDateKeyFromApi', () => {
    it('takes the date part of an ISO timestamp', () => {
      expect(toDateKeyFromApi('2026-08-03T09:30:00Z')).toBe('2026-08-03');
    });
    it('passes a plain date through', () => {
      expect(toDateKeyFromApi('2026-08-03')).toBe('2026-08-03');
    });
    it('rejects empty or too-short values', () => {
      expect(toDateKeyFromApi(null)).toBeNull();
      expect(toDateKeyFromApi('')).toBeNull();
      expect(toDateKeyFromApi('2026-08')).toBeNull();
    });
  });

  describe('daysFromTodayKey', () => {
    it('counts whole days from local midnight', () => {
      expect(daysFromTodayKey(keyOf(0))).toBe(0);
      expect(daysFromTodayKey(keyOf(5))).toBe(5);
      expect(daysFromTodayKey(keyOf(-2))).toBe(-2);
    });
  });

  describe('isBeforeMinimumBookingDateKey', () => {
    it('blocks anything inside the lead time', () => {
      expect(isBeforeMinimumBookingDateKey(keyOf(0), 3)).toBeTrue();
      expect(isBeforeMinimumBookingDateKey(keyOf(2), 3)).toBeTrue();
    });
    it('allows the first date on or after the threshold', () => {
      expect(isBeforeMinimumBookingDateKey(keyOf(3), 3)).toBeFalse();
      expect(isBeforeMinimumBookingDateKey(keyOf(10), 3)).toBeFalse();
    });
  });

  describe('isBeyondCeilingKey', () => {
    it('allows the ceiling day itself and blocks past it', () => {
      expect(isBeyondCeilingKey(keyOf(90), 90)).toBeFalse();
      expect(isBeyondCeilingKey(keyOf(91), 90)).toBeTrue();
    });
  });

  describe('buildAvailableDateKeys', () => {
    it('collects distinct date keys from api values, ignoring unusable ones', () => {
      const keys = buildAvailableDateKeys([
        '2026-08-03T09:00:00Z',
        '2026-08-03T14:00:00Z',
        '2026-08-04',
        null,
        '',
      ]);
      expect(Array.from(keys).sort()).toEqual(['2026-08-03', '2026-08-04']);
    });
  });

  describe('isSelectableDate', () => {
    const availableKeys = new Set<string>([keyOf(5), keyOf(95)]);

    it('is selectable when lead time, ceiling and availability all pass', () => {
      expect(
        isSelectableDate(structOf(5), {
          typeChosen: true,
          leadDays: 3,
          ceilingDays: 90,
          availableKeys,
        }),
      ).toBeTrue();
    });

    it('is not selectable inside the lead time even with availability', () => {
      const keys = new Set<string>([keyOf(1)]);
      expect(
        isSelectableDate(structOf(1), {
          typeChosen: true,
          leadDays: 3,
          ceilingDays: 90,
          availableKeys: keys,
        }),
      ).toBeFalse();
    });

    it('is not selectable beyond the ceiling even with availability', () => {
      // Guards the 2026-06-11 rule: the 90-day ceiling applies to EVERY role.
      expect(
        isSelectableDate(structOf(95), {
          typeChosen: true,
          leadDays: 3,
          ceilingDays: 90,
          availableKeys,
        }),
      ).toBeFalse();
    });

    it('is not selectable when the date has no availability', () => {
      expect(
        isSelectableDate(structOf(7), {
          typeChosen: true,
          leadDays: 3,
          ceilingDays: 90,
          availableKeys,
        }),
      ).toBeFalse();
    });

    it('is not selectable when nothing has loaded yet', () => {
      // An empty set must mean "nothing bookable", not "everything bookable" -- the inverse
      // would offer staff slots that do not exist.
      expect(
        isSelectableDate(structOf(5), {
          typeChosen: true,
          leadDays: 3,
          ceilingDays: 90,
          availableKeys: new Set<string>(),
        }),
      ).toBeFalse();
    });

    it('disables nothing before an appointment type is chosen', () => {
      // Mirrors markAppointmentDateDisabled's first guard: with no type selected the picker is
      // not yet meaningful, so it does not grey anything out.
      expect(
        isSelectableDate(structOf(1), {
          typeChosen: false,
          leadDays: 3,
          ceilingDays: 90,
          availableKeys: new Set<string>(),
        }),
      ).toBeTrue();
    });
  });
});
