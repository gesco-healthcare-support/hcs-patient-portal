/**
 * Whether an invitation's email failed to go (2026-10-27).
 *
 * The server keeps the invitation and returns its link even when the email cannot be queued, so staff
 * can send the link themselves; `emailQueued` says which happened. Only an explicit `false` counts: a
 * server without the flag is taken as sent, as before.
 */
export function inviteEmailWasNotQueued(result: unknown): boolean {
  return (result as { emailQueued?: unknown } | null | undefined)?.emailQueued === false;
}

export const INVITE_EMAIL_NOT_SENT_MESSAGE =
  'The invitation was created, but its email could not be sent. Copy the link and send it to them yourself.';
