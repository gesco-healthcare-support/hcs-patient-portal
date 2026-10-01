import { neutraliseCsvCell } from './csv-cell.util';

/**
 * The shared neutralisation used by every CSV export that carries caller-supplied text.
 * The per-export specs assert the rendered cell; these assert the rule itself.
 */
describe('neutraliseCsvCell', () => {
  // Jasmine has no it.each; generate one spec per trigger at describe time.
  for (const hostile of [
    '=HYPERLINK("http://x","go")',
    '+1234',
    '-1+2',
    '@SUM(A1)',
    '\tlead',
    '\rlead',
  ]) {
    it(`prefixes an apostrophe when the value opens with ${JSON.stringify(hostile[0])}`, () => {
      const result = neutraliseCsvCell(hostile);

      expect(result).toBe(`'${hostile}`);
      expect(result[0]).toBe("'");
    });
  }

  it('leaves an ordinary value untouched', () => {
    expect(neutraliseCsvCell('DOE JANE')).toBe('DOE JANE');
    expect(neutraliseCsvCell('Mozilla/5.0 (Windows NT 10.0)')).toBe(
      'Mozilla/5.0 (Windows NT 10.0)',
    );
  });

  it('only looks at the first character, so a trigger later in the value is left alone', () => {
    // A spreadsheet reads a formula from the start of a cell. "A=B" is not a formula,
    // and prefixing it would corrupt a legitimate value for no gain.
    expect(neutraliseCsvCell('A=B')).toBe('A=B');
    expect(neutraliseCsvCell('555-0101')).toBe('555-0101');
  });

  it('handles null, undefined and empty without producing a stray apostrophe', () => {
    expect(neutraliseCsvCell(null)).toBe('');
    expect(neutraliseCsvCell(undefined)).toBe('');
    expect(neutraliseCsvCell('')).toBe('');
  });

  it('accepts a number, because the audit export passes status and duration', () => {
    expect(neutraliseCsvCell(200)).toBe('200');
    expect(neutraliseCsvCell(0)).toBe('0');
  });
});
