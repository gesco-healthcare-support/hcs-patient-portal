/**
 * Wording for a save that got PART of the way (2026-08-06).
 *
 * The appointment edit form saves in stages: patient, then the appointment, then three child
 * upserts. When a child upsert fails the first two are already committed, so the message has to say
 * both things -- what was kept and what was not -- or the user cannot tell whether to retry the
 * whole form or just fix one section.
 *
 * <p>It also has to separate "you are not allowed to do this" from "it broke". Those need opposite
 * responses from the user: the first means ask someone with the permission, the second means try
 * again. The previous single message did neither, and named "employer / attorney" when only the
 * employer upsert can fail on permissions -- the two attorney upserts run under a bare
 * [Authorize].</p>
 *
 * A standalone function rather than a component method so the branch unit-tests without a TestBed,
 * matching the other *.util.ts files in this folder.
 */

/** The shape we need from an Angular HttpErrorResponse, without depending on the class. */
export interface SaveFailure {
  status?: number;
}

/** HTTP 403. The server understood who you are and refused anyway. */
const FORBIDDEN = 403;

export function downstreamSaveFailureMessage(error: SaveFailure | null | undefined): string | null {
  if (!error) {
    return null;
  }

  if (error.status === FORBIDDEN) {
    // Names the section AND the remedy. "Permission" rather than "error" because retrying will
    // never work -- the user needs someone else, and saying so saves them the attempts.
    return (
      'Your appointment and patient changes were saved, but you do not have permission to ' +
      'change employer details. Ask a supervisor to make that change.'
    );
  }

  return (
    'Your appointment and patient changes were saved, but the employer or attorney details ' +
    'could not be saved. Please try again.'
  );
}
