import { unitForForm, unitToDto } from './patient-unit.mapper';

/**
 * Covers the mapping that the booking wizard could not be live-gated on without completing a
 * nine-step form. The defect being pinned: the wizard used to send the unit as `address`, the column
 * no staff screen writes, so the Case Tracker received a stale value or none at all.
 *
 * What these DO cover: the direction of the mapping, the prefill precedence, and the blank handling
 * that decides between "clear it" and "leave it alone". What they CANNOT cover: a call site that
 * stops using these helpers. That risk is held down by both submit sites being one-liners that
 * delegate here.
 */
describe('patient unit mapper', () => {
  describe('unitForForm', () => {
    it('prefers the column staff corrections land in', () => {
      // Both populated means the patient booked with one unit and staff later corrected it.
      // Showing the booking value would invite someone to "fix" it back.
      expect(unitForForm({ apptNumber: 'STE 900', address: '4B' })).toBe('STE 900');
    });

    it('falls back to the legacy column so pre-existing units still show', () => {
      // Rows booked before 2026-08-13 were deliberately not backfilled. Without this the box would
      // look empty and staff would retype a unit that was already recorded.
      expect(unitForForm({ apptNumber: null, address: '4B' })).toBe('4B');
    });

    it('returns null, not undefined, when there is no unit', () => {
      // patchValue(undefined) leaves the previous value in place; null actually clears the control.
      expect(unitForForm({ apptNumber: null, address: null })).toBeNull();
      expect(unitForForm(null)).toBeNull();
      expect(unitForForm(undefined)).toBeNull();
    });
  });

  describe('unitToDto', () => {
    it('sends the typed unit', () => {
      expect(unitToDto('STE 1200')).toBe('STE 1200');
    });

    it('trims surrounding whitespace', () => {
      expect(unitToDto('  STE 1200  ')).toBe('STE 1200');
    });

    it('omits a blank rather than sending an empty string', () => {
      // The update path treats an omitted field as "leave it alone". Sending "" would wipe a unit
      // the user never touched.
      expect(unitToDto('')).toBeUndefined();
      expect(unitToDto('   ')).toBeUndefined();
      expect(unitToDto(null)).toBeUndefined();
      expect(unitToDto(undefined)).toBeUndefined();
    });
  });
});
