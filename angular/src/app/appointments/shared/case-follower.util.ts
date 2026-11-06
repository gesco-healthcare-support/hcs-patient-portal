/**
 * Case-follower choices on the booking submit (Patrick's answers, 2026-11-04).
 *
 * <p>The two submit fields are not on the generated proxy type yet and `proxy/` must not be
 * hand-edited, so they are declared here and spread onto the payload. A later
 * `abp generate-proxy` adds them; this extension can then be dropped.</p>
 *
 * <ul>
 *   <li>`bookerFollowsCase` (default true): the external booker is an Authorized User and gets
 *       every case email. Unticked, they still see the case but only get the request receipt.</li>
 *   <li>`sendAttorneyMailToParalegal` (default false): attorney bookers only; the lawyer gets NO
 *       email for this case and the side's paralegal accessor gets it instead. Implies the booker
 *       does not follow.</li>
 * </ul>
 */
export interface FollowerSubmitFields {
  bookerFollowsCase?: boolean;
  sendAttorneyMailToParalegal?: boolean;
}

export interface FollowerChoice {
  /** The "Add me as an Authorized User" box is offered (external non-patient booker). */
  showBookerOption: boolean;
  /** The booker's own choice for that box. */
  bookerFollows: boolean;
  /** "Send all notifications to my paralegal instead of me" is ticked (complete or not). */
  insteadTicked: boolean;
  /** ...and the merged accessor list holds the side's paralegal, so the redirect can apply. */
  insteadValid: boolean;
}

/** The fields to put on the submit body. Staff / patient bookers send neither (server default). */
export function resolveFollowerFields(choice: FollowerChoice): FollowerSubmitFields {
  if (choice.insteadTicked) {
    // The lawyer chose "instead of me", so the booker never follows, whatever the stored box says.
    return choice.insteadValid
      ? { bookerFollowsCase: false, sendAttorneyMailToParalegal: true }
      : { bookerFollowsCase: false };
  }
  return choice.showBookerOption ? { bookerFollowsCase: choice.bookerFollows } : {};
}

/** What the box shows: forced off (and disabled) while the lawyer's "instead" tick is on. */
export function bookerFollowsDisplayed(bookerFollows: boolean, insteadTicked: boolean): boolean {
  return bookerFollows && !insteadTicked;
}

/**
 * One-line note for the appointment views, from the detail DTO's read-only booleans. Both are
 * optional until the proxy is regenerated, so the DTO is read loosely.
 */
export function followerNote(dto: unknown): string | null {
  const d = (dto ?? {}) as { bookerFollowsCase?: boolean; attorneyMailToParalegal?: boolean };
  if (d.attorneyMailToParalegal === true) {
    return "The attorney's case emails go to the paralegal / case manager instead; the attorney can still sign in to see this case.";
  }
  if (d.bookerFollowsCase === false) {
    return 'The person who booked this case gets only the request receipt, not later case emails.';
  }
  return null;
}
