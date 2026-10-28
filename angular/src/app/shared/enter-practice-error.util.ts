/**
 * The message to show when switching into a practice fails (2026-10-27).
 *
 * Switching runs through the OAuth token grant, not RestService, so ABP's error dialog never fires and
 * the caller must say something itself. The grant explains a refusal in `error_description` (for example
 * "You are not assigned to this office."); that reason is shown when present, otherwise a general line.
 */
export const ENTER_PRACTICE_FALLBACK_MESSAGE =
  'Could not switch into that office. Please try again, or contact an administrator.';

export function enterPracticeErrorMessage(err: unknown): string {
  const body = (err as { error?: { error_description?: unknown } } | null | undefined)?.error;
  const reason = typeof body?.error_description === 'string' ? body.error_description.trim() : '';
  return reason || ENTER_PRACTICE_FALLBACK_MESSAGE;
}
