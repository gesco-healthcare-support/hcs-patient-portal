import { SsnMaskPipe } from './ssn-mask.pipe';

/**
 * Sweep #640 swapped `String.fromCharCode(0x2022)` for `fromCodePoint` (S7758). U+2022 is
 * inside the BMP so the two are identical here; the point of these specs is that the pipe
 * still redacts, since it is the only thing standing between an SSN and the screen.
 *
 * <p>The bullet is built from its code point rather than typed literally so the source stays
 * ASCII-only, which is a repo rule.</p>
 */
describe('SsnMaskPipe (sweep #640)', () => {
  const pipe = new SsnMaskPipe();
  const DOT = String.fromCodePoint(0x2022);

  it('is empty for no value', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });

  it('shows only the last four digits of a full SSN', () => {
    const out = pipe.transform('123-45-6789');
    expect(out).toBe(`${DOT}${DOT}${DOT}-${DOT}${DOT}-6789`);
    expect(out).not.toContain('123');
    expect(out).not.toContain('45');
  });

  it('accepts an unformatted SSN and redacts it the same way', () => {
    expect(pipe.transform('123456789')).toBe(`${DOT}${DOT}${DOT}-${DOT}${DOT}-6789`);
  });

  it('redacts a partial entry completely rather than revealing it', () => {
    // Fewer than four digits means there is no safe "last four", so nothing is shown.
    expect(pipe.transform('12')).toBe(`${DOT}${DOT}`);
    expect(pipe.transform('1')).toBe(DOT);
  });

  it('is empty when the value carries no digits at all', () => {
    expect(pipe.transform('--')).toBe('');
  });

  it('uses the bullet code point, not an asterisk', () => {
    expect(pipe.transform('123456789')).toContain(DOT);
    expect(pipe.transform('123456789')).not.toContain('*');
  });
});
