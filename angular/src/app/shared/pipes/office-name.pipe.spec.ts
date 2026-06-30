import { OfficeNamePipe } from './office-name.pipe';

describe('OfficeNamePipe (QA item A)', () => {
  const pipe = new OfficeNamePipe();

  it('prefixes and capitalizes a lowercase office slug', () => {
    expect(pipe.transform('falkinstein')).toBe('Dr. Falkinstein');
  });

  it('is idempotent for an already-prefixed name', () => {
    expect(pipe.transform('Dr. Falkinstein')).toBe('Dr. Falkinstein');
    expect(pipe.transform('dr. falkinstein')).toBe('dr. falkinstein');
  });

  it('returns empty string for null, undefined, or whitespace', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('   ')).toBe('');
  });

  it('does not mistake a name starting with "dr" (no separator) for a prefix', () => {
    expect(pipe.transform('drummond')).toBe('Dr. Drummond');
  });
});
