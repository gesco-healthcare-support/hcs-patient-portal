import { resolveExternalUserDisplayName } from './external-user-display-name';

/**
 * Phase 1 / C2 / D4 (firm-based AA/DA registration) -- pure tests for the
 * display-name resolver. Mirrors the C# ExternalUserDisplayNameUnitTests grid.
 *
 * Precedence (locked Adrian Q1 2026-06-11):
 *   First + Last present      -> "First Last"
 *   first-only / last-only    -> the present part
 *   names blank, firm present  -> FirmName   (the firm-account case)
 *   names + firm blank         -> Email
 *   everything blank           -> ""         (never undefined)
 */
describe('resolveExternalUserDisplayName', () => {
  it('returns "First Last" when both are present', () => {
    expect(resolveExternalUserDisplayName('Avery', 'Tester', 'Firm LLP', 'a@example.com')).toBe(
      'Avery Tester',
    );
  });

  it('returns the present name part when only one is set', () => {
    expect(resolveExternalUserDisplayName('Avery', null, 'Firm LLP', 'a@example.com')).toBe(
      'Avery',
    );
    expect(resolveExternalUserDisplayName(null, 'Tester', 'Firm LLP', 'a@example.com')).toBe(
      'Tester',
    );
  });

  it('falls back to FirmName when names are blank (firm account)', () => {
    expect(resolveExternalUserDisplayName(null, null, 'Firm LLP', 'a@example.com')).toBe(
      'Firm LLP',
    );
    expect(resolveExternalUserDisplayName('   ', '  ', 'Firm LLP', 'a@example.com')).toBe(
      'Firm LLP',
    );
  });

  it('falls back to email when names + firm are blank', () => {
    expect(resolveExternalUserDisplayName(null, null, '   ', 'a@example.com')).toBe(
      'a@example.com',
    );
  });

  it('returns "" (never undefined) when everything is blank', () => {
    expect(resolveExternalUserDisplayName(null, null, null, null)).toBe('');
    expect(resolveExternalUserDisplayName(undefined, undefined, undefined, undefined)).toBe('');
  });

  it('trims surrounding whitespace', () => {
    expect(resolveExternalUserDisplayName('  Avery  ', '  Tester  ', null, null)).toBe(
      'Avery Tester',
    );
    expect(resolveExternalUserDisplayName(null, null, null, '  a@example.com  ')).toBe(
      'a@example.com',
    );
  });
});
