import {
  REVAL_ENTRY_CARDS,
  STAFF_REVAL_ENTRY_CARDS,
  shouldAutoLoadSource,
  showPrefillBanner,
  showRevalEntryChoice,
  showSourceLookupFor,
} from './reval-entry.util';

describe('reval-entry util', () => {
  it('offers the number, the I-do-not-know-it and the before-this-portal cards, in that order', () => {
    expect(REVAL_ENTRY_CARDS.map((c) => c.value)).toEqual(['number', 'lookup', 'before']);
  });

  it('says on the lookup card that it only finds portal cases and points to the old-system card', () => {
    const hint = REVAL_ENTRY_CARDS.find((c) => c.value === 'lookup')!.hint;
    expect(hint).toBe(
      'Only for evaluations booked in this portal. Give the patient\'s name, date of birth and claim number, and we email the number to you. First exam in the old system? Choose "The first exam was before this portal".',
    );
  });

  it('gives outside bookers all three cards and staff the two without the lookup (R3)', () => {
    expect(REVAL_ENTRY_CARDS.map((c) => c.value)).toEqual(['number', 'lookup', 'before']);
    expect(STAFF_REVAL_ENTRY_CARDS.map((c) => c.value)).toEqual(['number', 'before']);
  });

  it('shows the choice only for a re-evaluation with nothing loaded', () => {
    expect(showRevalEntryChoice('reval', null)).toBeTrue();
    expect(showRevalEntryChoice('reval', 'A00004')).toBeFalse();
    expect(showRevalEntryChoice('reBook', null)).toBeFalse();
    expect(showRevalEntryChoice('new', null)).toBeFalse();
  });

  it('shows the number box after the number card is picked, or once a source is loaded', () => {
    expect(showSourceLookupFor('reval', null, null)).toBeFalse();
    expect(showSourceLookupFor('reval', 'before', null)).toBeFalse();
    expect(showSourceLookupFor('reval', 'lookup', null)).toBeFalse();
    expect(showSourceLookupFor('reval', 'number', null)).toBeTrue();
    expect(showSourceLookupFor('reval', null, 'A00004')).toBeTrue();
    expect(showSourceLookupFor('reBook', null, null)).toBeTrue();
    expect(showSourceLookupFor('new', 'number', null)).toBeFalse();
  });

  it('shows the prefill banner whenever an earlier case is loaded', () => {
    expect(showPrefillBanner('reval', 'A00004')).toBeTrue();
    expect(showPrefillBanner('reBook', 'A00004')).toBeTrue();
    expect(showPrefillBanner('reRequest', 'A00004')).toBeTrue();
    expect(showPrefillBanner('reval', null)).toBeFalse();
    expect(showPrefillBanner('new', 'A00004')).toBeFalse();
  });

  it('auto-loads ?source for re-evaluation and re-book, not for a new request', () => {
    expect(shouldAutoLoadSource('reval', 'A00004')).toBeTrue();
    expect(shouldAutoLoadSource('reBook', ' A00004 ')).toBeTrue();
    expect(shouldAutoLoadSource('reval', '  ')).toBeFalse();
    expect(shouldAutoLoadSource('reval', null)).toBeFalse();
    expect(shouldAutoLoadSource('new', 'A00004')).toBeFalse();
  });
});
