import { formatDateOfBirthForApi, normalizePatientDateOfBirth } from './date-of-birth.util';

describe('date-of-birth.util (#620)', () => {
  describe('formatDateOfBirthForApi', () => {
    it('formats a datepicker struct as yyyy-MM-dd', () => {
      expect(formatDateOfBirthForApi({ year: 1990, month: 5, day: 15 })).toBe('1990-05-15');
    });

    it('zero-pads single-digit months and days', () => {
      // The old implementation got this for free from toISOString; building the
      // string by hand does not, so it is pinned.
      expect(formatDateOfBirthForApi({ year: 2001, month: 1, day: 2 })).toBe('2001-01-02');
      expect(formatDateOfBirthForApi({ year: 2001, month: 12, day: 9 })).toBe('2001-12-09');
    });

    it('does not construct a Date, so no time zone can shift the day', () => {
      // This is the pin for the defect the extraction fixed, and it is written
      // to work anywhere. The obvious test -- asserting 1990-05-15 comes back
      // as 1990-05-15 -- CANNOT fail on this machine or on CI, because both
      // sit at or west of UTC where the old code was already right. It only
      // failed east of Greenwich, and TZ is not honoured by Node on Windows,
      // so it cannot be reproduced locally at all.
      //
      // A two-digit year distinguishes the two implementations regardless of
      // zone: `new Date(99, 0, 1)` remaps 0-99 into 1900-1999 and yields
      // '1999-01-01', whereas building the string preserves what was passed.
      // Not a realistic birth date -- the point is that a formatter must not
      // silently invent a century, and reaching for Date is what made it do so.
      expect(formatDateOfBirthForApi({ year: 99, month: 1, day: 1 })).toBe('99-01-01');
      expect(formatDateOfBirthForApi({ year: 50, month: 6, day: 15 })).toBe('50-06-15');

      // Ordinary dates, unchanged from the old behaviour where it was correct.
      expect(formatDateOfBirthForApi({ year: 1990, month: 5, day: 15 })).toBe('1990-05-15');
      expect(formatDateOfBirthForApi({ year: 2000, month: 1, day: 1 })).toBe('2000-01-01');
      expect(formatDateOfBirthForApi({ year: 1999, month: 12, day: 31 })).toBe('1999-12-31');
    });

    it('passes an already-formatted string through untouched', () => {
      expect(formatDateOfBirthForApi('1985-03-22')).toBe('1985-03-22');
      // Including the fuller form the API returns.
      expect(formatDateOfBirthForApi('1985-03-22T00:00:00')).toBe('1985-03-22T00:00:00');
    });

    it('returns null for empty, partial or unrecognised input', () => {
      expect(formatDateOfBirthForApi(null)).toBeNull();
      expect(formatDateOfBirthForApi(undefined)).toBeNull();
      expect(formatDateOfBirthForApi('')).toBeNull();
      expect(formatDateOfBirthForApi({ year: 1990, month: 5 })).toBeNull();
      expect(formatDateOfBirthForApi({ year: 1990 })).toBeNull();
      expect(formatDateOfBirthForApi({})).toBeNull();
    });
  });

  describe('normalizePatientDateOfBirth', () => {
    it('keeps a plausible date, returning the original string', () => {
      expect(normalizePatientDateOfBirth('1985-03-22')).toBe('1985-03-22');
      // Precision beyond the date is preserved rather than trimmed.
      expect(normalizePatientDateOfBirth('1985-03-22T00:00:00')).toBe('1985-03-22T00:00:00');
    });

    it('rejects a year before 1900', () => {
      expect(normalizePatientDateOfBirth('1899-12-31')).toBeNull();
      expect(normalizePatientDateOfBirth('1900-01-01')).toBe('1900-01-01');
    });

    it("rejects today's date, which is the registration sentinel", () => {
      const now = new Date();
      const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(
        now.getDate(),
      ).padStart(2, '0')}`;
      expect(normalizePatientDateOfBirth(today)).toBeNull();
    });

    it('returns null for empty or unparseable input', () => {
      expect(normalizePatientDateOfBirth(null)).toBeNull();
      expect(normalizePatientDateOfBirth(undefined)).toBeNull();
      expect(normalizePatientDateOfBirth('')).toBeNull();
      expect(normalizePatientDateOfBirth('not-a-date')).toBeNull();
      expect(normalizePatientDateOfBirth('15/05/1990')).toBeNull();
    });
  });
});
