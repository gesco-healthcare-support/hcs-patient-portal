import { LEGACY_WIZARD_DRAFT_KEY, removeLegacyWizardDraftCache } from './legacy-draft-cache';

/**
 * Asserts what is left in the browser afterwards, not that removeItem was called. The value
 * planted below has the shape earlier builds wrote; every identifier in it is synthetic.
 */
describe('removeLegacyWizardDraftCache', () => {
  const LEFT_BY_AN_EARLIER_BUILD = JSON.stringify({
    v: { firstName: 'Testpatient', socialSecurityNumber: '000-12-3456', dateOfBirth: '1980-01-01' },
    step: 2,
  });

  afterEach(() => localStorage.removeItem(LEGACY_WIZARD_DRAFT_KEY));

  it('removes the booking form an earlier build left in localStorage', () => {
    localStorage.setItem(LEGACY_WIZARD_DRAFT_KEY, LEFT_BY_AN_EARLIER_BUILD);

    removeLegacyWizardDraftCache();

    expect(localStorage.getItem(LEGACY_WIZARD_DRAFT_KEY)).toBeNull();
  });

  it('leaves the rest of localStorage alone', () => {
    // The theme preference is a UI setting that sign-out deliberately keeps (full-logout.ts).
    const theme = localStorage.getItem('LPX_THEME');
    localStorage.setItem('LPX_THEME', 'dark');
    localStorage.setItem(LEGACY_WIZARD_DRAFT_KEY, LEFT_BY_AN_EARLIER_BUILD);
    try {
      removeLegacyWizardDraftCache();

      expect(localStorage.getItem('LPX_THEME')).toBe('dark');
    } finally {
      if (theme === null) {
        localStorage.removeItem('LPX_THEME');
      } else {
        localStorage.setItem('LPX_THEME', theme);
      }
    }
  });

  it('does nothing, and does not throw, when there is nothing to remove', () => {
    expect(() => removeLegacyWizardDraftCache()).not.toThrow();
    expect(localStorage.getItem(LEGACY_WIZARD_DRAFT_KEY)).toBeNull();
  });

  it('does not stop the app booting when storage is unavailable', () => {
    const removeItem = spyOn(Storage.prototype, 'removeItem').and.throwError('SecurityError');
    expect(() => removeLegacyWizardDraftCache()).not.toThrow();
    // The spy outlives the test body, and afterEach removes the key through the same method.
    removeItem.and.callThrough();
  });
});
