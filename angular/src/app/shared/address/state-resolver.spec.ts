import { resolveStateId, StateLookupOption, USPS_STATE_NAMES } from './state-resolver';

/**
 * F2 / address validation (2026-05-29) -- pure tests for the state resolver
 * that maps a provider's state string (name or USPS code) to a lookup StateId.
 */
describe('resolveStateId', () => {
  const lookup: StateLookupOption[] = [
    { id: 'id-ca', name: 'California' },
    { id: 'id-tx', name: 'Texas' },
    { id: 'id-ny', name: 'New York' },
  ];

  it('resolves a USPS 2-letter code (any case)', () => {
    expect(resolveStateId('CA', lookup)).toBe('id-ca');
    expect(resolveStateId('ca', lookup)).toBe('id-ca');
  });

  it('resolves a full name case-insensitively', () => {
    expect(resolveStateId('California', lookup)).toBe('id-ca');
    expect(resolveStateId('california', lookup)).toBe('id-ca');
  });

  it('resolves multi-word names + their codes', () => {
    expect(resolveStateId('New York', lookup)).toBe('id-ny');
    expect(resolveStateId('NY', lookup)).toBe('id-ny');
  });

  it('returns null for an unknown state', () => {
    expect(resolveStateId('ZZ', lookup)).toBeNull();
    expect(resolveStateId('Atlantis', lookup)).toBeNull();
  });

  it('returns null for empty / nullish input', () => {
    expect(resolveStateId(null, lookup)).toBeNull();
    expect(resolveStateId(undefined, lookup)).toBeNull();
    expect(resolveStateId('   ', lookup)).toBeNull();
  });

  it('returns null when the resolved name is not present in the lookup', () => {
    // WY is a valid code but Wyoming is not in this lookup.
    expect(resolveStateId('WY', lookup)).toBeNull();
  });

  it('covers all 50 states + DC in the USPS map', () => {
    expect(Object.keys(USPS_STATE_NAMES).length).toBe(51);
  });
});
