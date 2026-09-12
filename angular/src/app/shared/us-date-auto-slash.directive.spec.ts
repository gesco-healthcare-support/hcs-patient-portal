import { formatPartialUsDate } from './us-date-auto-slash.directive';

describe('formatPartialUsDate', () => {
  it('returns empty for empty input', () => {
    expect(formatPartialUsDate('')).toBe('');
  });

  it('inserts separators progressively as digits arrive', () => {
    expect(formatPartialUsDate('0')).toBe('0');
    expect(formatPartialUsDate('06')).toBe('06');
    expect(formatPartialUsDate('061')).toBe('06/1');
    expect(formatPartialUsDate('0615')).toBe('06/15');
    expect(formatPartialUsDate('06151')).toBe('06/15/1');
    expect(formatPartialUsDate('06151985')).toBe('06/15/1985');
  });

  it('truncates to 8 digits (MMDDYYYY)', () => {
    expect(formatPartialUsDate('0615198599')).toBe('06/15/1985');
  });

  it('is idempotent on an already-formatted value', () => {
    expect(formatPartialUsDate('06/15/1985')).toBe('06/15/1985');
  });

  it('strips non-digits (letters and stray separators) before regrouping', () => {
    expect(formatPartialUsDate('ab06cd15')).toBe('06/15');
    expect(formatPartialUsDate('06-15-1985')).toBe('06/15/1985');
    expect(formatPartialUsDate('06.15.1985')).toBe('06/15/1985');
  });

  it('drops a trailing separator when a group is deleted (backspace)', () => {
    expect(formatPartialUsDate('06/1')).toBe('06/1');
    expect(formatPartialUsDate('06/')).toBe('06');
    expect(formatPartialUsDate('06/15/')).toBe('06/15');
  });
});
