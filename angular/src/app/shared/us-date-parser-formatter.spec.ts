import { UsDateParserFormatter } from './us-date-parser-formatter';

/**
 * QA #15 item 5: pins the US-format contract for every ngb datepicker input.
 * Pure class -- no TestBed. Parse failures MUST return null (not a best-guess
 * date) so ng-bootstrap raises the ngbDate control error and form gates block.
 */
describe('UsDateParserFormatter', () => {
  const f = new UsDateParserFormatter();

  describe('format', () => {
    it('renders MM/DD/YYYY zero-padded', () => {
      expect(f.format({ year: 2026, month: 2, day: 3 })).toBe('02/03/2026');
      expect(f.format({ year: 1985, month: 12, day: 31 })).toBe('12/31/1985');
    });

    it('renders empty for null', () => {
      expect(f.format(null)).toBe('');
    });
  });

  describe('parse', () => {
    it('accepts M/D/YYYY and MM/DD/YYYY as month-first (US)', () => {
      expect(f.parse('6/15/1985')).toEqual({ year: 1985, month: 6, day: 15 });
      expect(f.parse('06/15/1985')).toEqual({ year: 1985, month: 6, day: 15 });
      // Ambiguous-looking input is US semantics by contract: May 6th.
      expect(f.parse('05/06/2026')).toEqual({ year: 2026, month: 5, day: 6 });
    });

    it('accepts dash and dot separators', () => {
      expect(f.parse('6-15-1985')).toEqual({ year: 1985, month: 6, day: 15 });
      expect(f.parse('6.15.1985')).toEqual({ year: 1985, month: 6, day: 15 });
    });

    it('rejects day-first, out-of-range, and non-calendar dates', () => {
      expect(f.parse('31/12/2025')).toBeNull(); // month 31
      expect(f.parse('02/30/2024')).toBeNull(); // Feb 30 never exists
      expect(f.parse('00/10/2024')).toBeNull();
      expect(f.parse('13/01/2024')).toBeNull();
    });

    it('rejects partial or garbage input', () => {
      expect(f.parse('')).toBeNull();
      expect(f.parse('06/15')).toBeNull(); // no year
      expect(f.parse('06/15/85')).toBeNull(); // 2-digit year is ambiguous
      expect(f.parse('yesterday')).toBeNull();
      expect(f.parse('2026-07-07T00:00:00')).toBeNull();
    });

    it('accepts leap-day only on leap years', () => {
      expect(f.parse('02/29/2024')).toEqual({ year: 2024, month: 2, day: 29 });
      expect(f.parse('02/29/2025')).toBeNull();
    });
  });
});
