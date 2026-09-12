import { isStrikeListGateBlocked } from './strike-list-gate';

/**
 * AF6 (2026-06-05): pure-function unit tests for the PQME strike-list submit
 * gate. No TestBed -- same pattern as document-upload.validation.spec.ts.
 */
describe('isStrikeListGateBlocked', () => {
  const marked = [{ isStrikeList: true }];
  const unmarked = [{ isStrikeList: false }, { isStrikeList: false }];

  it('never blocks a non-PQME appointment', () => {
    expect(isStrikeListGateBlocked(false, true, unmarked)).toBe(false);
  });

  it('does not block PQME when the box is unchecked', () => {
    expect(isStrikeListGateBlocked(true, false, unmarked)).toBe(false);
  });

  it('blocks PQME + box checked when no document is marked', () => {
    expect(isStrikeListGateBlocked(true, true, unmarked)).toBe(true);
  });

  it('blocks PQME + box checked with no documents staged at all', () => {
    expect(isStrikeListGateBlocked(true, true, [])).toBe(true);
  });

  it('does not block once a document is marked as the strike list', () => {
    expect(isStrikeListGateBlocked(true, true, marked)).toBe(false);
  });
});
