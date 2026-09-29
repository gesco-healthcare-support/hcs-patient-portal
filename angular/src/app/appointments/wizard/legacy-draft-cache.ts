/**
 * Removes the booking form that earlier builds of the booking wizard left in the browser.
 *
 * <p>Those builds autosaved the whole form -- the patient's SSN, date of birth, name and
 * address included -- to localStorage under {@link LEGACY_WIZARD_DRAFT_KEY}, in plain text.
 * The key was the same for every user, and sign-out did not remove it, so the next person to
 * book on a shared computer could have the previous patient pre-filled. The wizard no longer
 * writes or reads it; its only store is the per-user server draft.</p>
 *
 * <p>Removing the write does not remove what is already there, so this runs at every app
 * start (registered in app.config.ts). Nothing has to remember to call it. Keep it: a browser
 * that last ran an older build can still hold the key, however long ago that was.</p>
 */
export const LEGACY_WIZARD_DRAFT_KEY = 'ra-wizard-draft';

export function removeLegacyWizardDraftCache(): void {
  if (typeof window === 'undefined') {
    return;
  }
  try {
    window.localStorage.removeItem(LEGACY_WIZARD_DRAFT_KEY);
  } catch {
    // Storage is unavailable (private mode, blocked site data). Then it cannot hold the key
    // either, so there is nothing to remove, and a failure here must not stop the app booting.
  }
}
