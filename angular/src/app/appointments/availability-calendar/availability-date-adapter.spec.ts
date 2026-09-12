import { AvailabilityDateAdapter } from './availability-date-adapter';

/**
 * Phase 4a (2026-08-03). This adapter IS the fix for defect #6 -- the picked date rendering as an
 * empty input -- so its round trip is pinned directly rather than only through the component.
 *
 * <p>The `fromModel` cases matter most: returning null is what blanked the input, so every shape the
 * component can be handed must map to a struct, and only genuinely unusable values may return null.</p>
 */
describe('AvailabilityDateAdapter', () => {
  let adapter: AvailabilityDateAdapter;

  beforeEach(() => {
    adapter = new AvailabilityDateAdapter();
  });

  it('maps a YYYY-MM-DD key to a struct', () => {
    expect(adapter.fromModel('2026-08-13')).toEqual({ year: 2026, month: 8, day: 13 });
  });

  it('maps an ISO timestamp to its LOCAL calendar date, without a UTC shift', () => {
    // A plain calendar date must not move a day because of the host timezone.
    expect(adapter.fromModel('2026-08-13T00:00:00Z')).toEqual({ year: 2026, month: 8, day: 13 });
  });

  it('returns null only for values that are not dates', () => {
    expect(adapter.fromModel(null)).toBeNull();
    expect(adapter.fromModel('')).toBeNull();
    expect(adapter.fromModel('2026-08')).toBeNull();
  });

  it('maps a struct back to a YYYY-MM-DD key, zero padded', () => {
    expect(adapter.toModel({ year: 2026, month: 8, day: 6 })).toBe('2026-08-06');
    expect(adapter.toModel(null)).toBeNull();
  });

  it('round trips a key unchanged', () => {
    const key = '2026-12-01';
    expect(adapter.toModel(adapter.fromModel(key))).toBe(key);
  });
});
