import { BookingStatus } from '../../proxy/enums/booking-status.enum';
import type { DoctorAvailabilityWithNavigationPropertiesDto } from '../../proxy/doctor-availabilities/models';
import {
  bookingStatusToKey,
  bookingStatusLabel,
  buildWeekColumns,
  formatTimeRange,
  formatWeekRange,
  isoDate,
  startOfWeekMonday,
  weekDatesFor,
} from './avail-grid.util';

describe('avail-grid.util', () => {
  describe('bookingStatusToKey', () => {
    it('maps the three booking statuses to grid keys', () => {
      expect(bookingStatusToKey(BookingStatus.Available)).toBe('available');
      expect(bookingStatusToKey(BookingStatus.Booked)).toBe('booked');
      expect(bookingStatusToKey(BookingStatus.Reserved)).toBe('reserved');
    });

    it('defaults unknown/nullish status to available', () => {
      expect(bookingStatusToKey(undefined)).toBe('available');
      expect(bookingStatusToKey(null)).toBe('available');
    });
  });

  describe('bookingStatusLabel', () => {
    it('title-cases the status key', () => {
      expect(bookingStatusLabel(BookingStatus.Booked)).toBe('Booked');
      expect(bookingStatusLabel(BookingStatus.Reserved)).toBe('Reserved');
    });
  });

  describe('formatTimeRange', () => {
    it('shares the meridiem when both times are AM', () => {
      expect(formatTimeRange('08:30:00', '09:30:00')).toBe('8:30 - 9:30 AM');
    });

    it('shows both meridiems when the band crosses noon', () => {
      expect(formatTimeRange('11:30:00', '13:00:00')).toBe('11:30 AM - 1:00 PM');
    });

    it('renders midnight as 12 AM', () => {
      expect(formatTimeRange('00:00:00', '00:30:00')).toBe('12:00 - 12:30 AM');
    });

    it('returns empty string when both inputs are missing', () => {
      expect(formatTimeRange(undefined, undefined)).toBe('');
      expect(formatTimeRange(null, '')).toBe('');
    });
  });

  describe('isoDate', () => {
    it('formats a local date as yyyy-mm-dd with no shift', () => {
      expect(isoDate(new Date(2026, 5, 1))).toBe('2026-06-01');
      expect(isoDate(new Date(2026, 11, 31))).toBe('2026-12-31');
    });
  });

  describe('startOfWeekMonday', () => {
    it('returns the same Monday for any day of that week', () => {
      // 2026-06-15 is a Monday; 2026-06-21 is the Sunday of the same week.
      expect(isoDate(startOfWeekMonday(new Date(2026, 5, 15)))).toBe('2026-06-15');
      expect(isoDate(startOfWeekMonday(new Date(2026, 5, 18)))).toBe('2026-06-15');
      expect(isoDate(startOfWeekMonday(new Date(2026, 5, 21)))).toBe('2026-06-15');
    });
  });

  describe('weekDatesFor', () => {
    it('returns Mon..Sun for the anchor week at offset 0', () => {
      const week = weekDatesFor(new Date(2026, 5, 17), 0).map(isoDate);
      expect(week).toEqual([
        '2026-06-15',
        '2026-06-16',
        '2026-06-17',
        '2026-06-18',
        '2026-06-19',
        '2026-06-20',
        '2026-06-21',
      ]);
    });

    it('shifts by whole weeks for non-zero offsets', () => {
      expect(isoDate(weekDatesFor(new Date(2026, 5, 17), 1)[0])).toBe('2026-06-22');
      expect(isoDate(weekDatesFor(new Date(2026, 5, 17), -1)[0])).toBe('2026-06-08');
    });
  });

  describe('formatWeekRange', () => {
    it('collapses the month when the week stays in one month', () => {
      expect(formatWeekRange(weekDatesFor(new Date(2026, 5, 17), 0))).toBe('Jun 15 - 21, 2026');
    });

    it('shows both months when the week spans a boundary', () => {
      // Week containing 2026-07-01 starts Mon 2026-06-29.
      expect(formatWeekRange(weekDatesFor(new Date(2026, 6, 1), 0))).toBe('Jun 29 - Jul 5, 2026');
    });
  });

  describe('buildWeekColumns', () => {
    const row = (
      availableDate: string,
      fromTime: string,
      toTime: string,
      status: BookingStatus,
      capacity = 3,
    ): DoctorAvailabilityWithNavigationPropertiesDto => ({
      doctorAvailability: {
        id: `${availableDate}-${fromTime}`,
        availableDate,
        fromTime,
        toTime,
        bookingStatusId: status,
        capacity,
      },
    });

    it('buckets rows by date and orders slots by start time', () => {
      const week = weekDatesFor(new Date(2026, 5, 15), 0);
      const items = [
        row('2026-06-15T00:00:00', '13:00:00', '14:00:00', BookingStatus.Available),
        row('2026-06-15T00:00:00', '08:30:00', '09:30:00', BookingStatus.Booked),
        row('2026-06-17T00:00:00', '09:00:00', '10:00:00', BookingStatus.Reserved),
      ];

      const cols = buildWeekColumns(items, week);

      expect(cols.length).toBe(7);
      expect(cols[0].iso).toBe('2026-06-15');
      expect(cols[0].dow).toBe('Mon');
      expect(cols[0].slots.map((s) => s.fromTime)).toEqual(['08:30:00', '13:00:00']);
      expect(cols[0].total).toBe(2);
      // one Booked slot is non-available.
      expect(cols[0].busy).toBe(1);
      // empty day.
      expect(cols[1].total).toBe(0);
      // Wednesday's reserved slot.
      expect(cols[2].slots[0].statusKey).toBe('reserved');
    });

    it('counts both booked and reserved toward busy', () => {
      const week = weekDatesFor(new Date(2026, 5, 15), 0);
      const items = [
        row('2026-06-16T00:00:00', '08:00:00', '09:00:00', BookingStatus.Booked),
        row('2026-06-16T00:00:00', '09:00:00', '10:00:00', BookingStatus.Reserved),
        row('2026-06-16T00:00:00', '10:00:00', '11:00:00', BookingStatus.Available),
      ];

      const tuesday = buildWeekColumns(items, week)[1];
      expect(tuesday.total).toBe(3);
      expect(tuesday.busy).toBe(2);
    });
  });
});
