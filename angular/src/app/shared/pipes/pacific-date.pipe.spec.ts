import { TestBed } from '@angular/core/testing';
import { CalendarDatePipe, PacificDatePipe } from './pacific-date.pipe';

/**
 * The DST cases are the point of these pipes, not decoration. Pacific time repeats 01:00-02:00
 * every November and skips 02:00-03:00 every March, so an offset chosen once is wrong for half the
 * year and wrong twice on the changeover days. 2026 transitions: DST starts 2026-03-08 10:00Z,
 * ends 2026-11-01 09:00Z.
 *
 * The calendarDate cases guard the opposite mistake: a date of birth has no zone, so any
 * conversion moves it, and a one-day shift on a medical-legal record is a wrong value rather than
 * a display preference.
 */
describe('PacificDatePipe / CalendarDatePipe', () => {
  let pacific: PacificDatePipe;
  let calendar: CalendarDatePipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PacificDatePipe, CalendarDatePipe],
    });
    pacific = TestBed.inject(PacificDatePipe);
    calendar = TestBed.inject(CalendarDatePipe);
  });

  describe('pacificDate (instants)', () => {
    it('renders a summer instant at the PDT offset', () => {
      // 22:00Z on 27 Aug is 15:00 Pacific (UTC-7).
      expect(pacific.transform('2026-08-27T22:00:00Z', 'MMM d, y, h:mm a')).toBe(
        'Aug 27, 2026, 3:00 PM',
      );
    });

    it('renders a winter instant at the PST offset', () => {
      // 22:00Z on 15 Dec is 14:00 Pacific (UTC-8).
      expect(pacific.transform('2026-12-15T22:00:00Z', 'MMM d, y, h:mm a')).toBe(
        'Dec 15, 2026, 2:00 PM',
      );
    });

    it('picks the offset that was in force, on both passes through the repeated November hour', () => {
      // 01:30 Pacific happens twice on 1 Nov 2026: once on PDT (08:30Z) and again on PST (09:30Z).
      // Both must render 1:30 AM -- an offset resolved once, or guessed, gets one of them wrong.
      expect(pacific.transform('2026-11-01T08:30:00Z', 'MMM d, y, h:mm a')).toBe(
        'Nov 1, 2026, 1:30 AM',
      );
      expect(pacific.transform('2026-11-01T09:30:00Z', 'MMM d, y, h:mm a')).toBe(
        'Nov 1, 2026, 1:30 AM',
      );
    });

    it('lands either side of the skipped March hour, never inside it', () => {
      // DST starts 2026-03-08 at 10:00Z: 02:00 PST jumps straight to 03:00 PDT.
      expect(pacific.transform('2026-03-08T09:59:00Z', 'h:mm a')).toBe('1:59 AM');
      expect(pacific.transform('2026-03-08T10:00:00Z', 'h:mm a')).toBe('3:00 AM');
    });

    it('reads an unzoned instant as UTC rather than as the viewer local time', () => {
      // ABP's own AuditLog.ExecutionTime and EntityChange.ChangeTime reach the client with no zone
      // designator -- those entities carry no value converter, so the clock pin does not reach
      // them. Both are rendered in this app, and read as local they would be 7 hours off.
      expect(pacific.transform('2026-08-27T22:00:00', 'MMM d, y, h:mm a')).toBe(
        'Aug 27, 2026, 3:00 PM',
      );
    });

    it('returns null for an absent or unparseable value rather than a fallback date', () => {
      expect(pacific.transform(null)).toBeNull();
      expect(pacific.transform(undefined)).toBeNull();
      expect(pacific.transform('')).toBeNull();
      expect(pacific.transform('not a date')).toBeNull();
    });
  });

  describe('calendarDate (dates somebody wrote down)', () => {
    it('renders the written date from a zoneless date-time string', () => {
      expect(calendar.transform('1985-09-03T00:00:00', 'MMM d, y')).toBe('Sep 3, 1985');
    });

    it('renders the written date from a bare date string', () => {
      // new Date('1985-09-03') is parsed as UTC midnight per the spec, which is 2 September in
      // Pacific. The pipe adds the time component so it is read as a local wall-clock date.
      expect(calendar.transform('1985-09-03', 'MMM d, y')).toBe('Sep 3, 1985');
    });

    it('still renders the written date if a zone designator leaks in', () => {
      // This is the exact regression the server-side exemption prevents: on 2026-08-27 pinning the
      // clock made a date of birth serialize as 1985-09-03T00:00:00Z, which a Pacific browser
      // renders as 2 September. The server fix is the real one; this keeps a repeat off the page.
      expect(calendar.transform('1985-09-03T00:00:00Z', 'MMM d, y')).toBe('Sep 3, 1985');
      expect(calendar.transform('1985-09-03T00:00:00+05:30', 'MMM d, y')).toBe('Sep 3, 1985');
    });

    it('returns null for an absent or unparseable value', () => {
      expect(calendar.transform(null)).toBeNull();
      expect(calendar.transform(undefined)).toBeNull();
      expect(calendar.transform('')).toBeNull();
      expect(calendar.transform('not a date')).toBeNull();
    });
  });
});
