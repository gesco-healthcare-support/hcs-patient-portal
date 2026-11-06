import { isExamDatePassed, pacificToday } from './exam-date-passed.util';

describe('exam-date-passed util', () => {
  // 2026-11-06 20:00 UTC is 12:00 on the 6th in Pacific time.
  const noon6th = new Date('2026-11-06T20:00:00Z');

  it('reads today in Pacific time, not UTC', () => {
    // 2026-11-07 03:00 UTC is still the evening of the 6th in Pacific.
    expect(pacificToday(new Date('2026-11-07T03:00:00Z'))).toBe('2026-11-06');
    expect(pacificToday(noon6th)).toBe('2026-11-06');
  });

  it('is true only for an exam day strictly before today', () => {
    expect(isExamDatePassed('2026-11-05T09:00:00', noon6th)).toBeTrue();
    expect(isExamDatePassed('2026-11-06T09:00:00', noon6th)).toBeFalse();
    expect(isExamDatePassed('2026-11-07T09:00:00', noon6th)).toBeFalse();
  });

  it('keeps the exam day itself "not passed" late in the Pacific evening', () => {
    expect(isExamDatePassed('2026-11-06T09:00:00', new Date('2026-11-07T07:59:00Z'))).toBeFalse();
    expect(isExamDatePassed('2026-11-06T09:00:00', new Date('2026-11-07T08:00:00Z'))).toBeTrue();
  });

  it('treats a missing or unreadable date as not passed', () => {
    expect(isExamDatePassed(undefined, noon6th)).toBeFalse();
    expect(isExamDatePassed(null, noon6th)).toBeFalse();
    expect(isExamDatePassed('not a date', noon6th)).toBeFalse();
  });
});
