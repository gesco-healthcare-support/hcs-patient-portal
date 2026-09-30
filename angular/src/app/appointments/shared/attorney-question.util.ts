import type { AttorneySectionPrefix } from './attorney-section-validators';

/**
 * Item 5 (2026-08-18) -- the explicit attorney question that replaces the "Include" checkbox.
 *
 * "Include" conflated two different things: *show me this section* and *this appointment has an
 * applicant attorney*. The booker could not tell which they were answering, and the form could
 * not either -- a section left at its default quietly asserted an attorney existed, or quietly
 * dropped one. Asking the real question removes the ambiguity at the source.
 *
 * Kept as pure functions so they unit-test without a TestBed, mirroring wizard-copy.util.ts.
 */

/** Who is filling in the booking form. Drives wording, and whether the question is asked. */
export interface AttorneyQuestionViewer {
  isPatient: boolean;
  isApplicantAttorney: boolean;
  isDefenseAttorney: boolean;
}

/**
 * Should the question be asked at all?
 *
 * FALSE when the viewer IS the attorney type the section is about: an applicant attorney does
 * not need asking whether the applicant is represented -- they are the representation. The
 * answer is then forced to yes and the section stays required.
 *
 * This holds even when a paralegal books under an attorney's role account, because an attorney
 * is still on the claim: the question is about representation, not about who is typing
 * (Adrian, 2026-08-14). It is a DELIBERATE exception to the otherwise-universal rule that the
 * booker must answer explicitly -- do not file it as a missing validator.
 */
export function shouldAskAttorneyQuestion(
  prefix: AttorneySectionPrefix,
  viewer: AttorneyQuestionViewer,
): boolean {
  if (prefix === 'applicantAttorney') {
    return !viewer.isApplicantAttorney;
  }
  return !viewer.isDefenseAttorney;
}

/**
 * What the enabled-flag subscriber should do for a given value.
 *
 * Exists because item 5 gave that flag a THIRD state and a bare `!enabled` could not see it.
 * Before, the flag was only ever true or false, so `!enabled` meant "the booker turned this
 * off" and popping an "are you sure there is no attorney?" confirmation was right. Now null
 * means "not asked yet", null is falsy, and that same branch fired the confirmation over a
 * form the booker had not touched -- a modal demanding they confirm a decision they had never
 * made. Caught by a live check, not by the build.
 */
export type AttorneyToggleAction = 'unanswered' | 'confirm-off' | 'enable';

export function resolveAttorneyToggleAction(enabled: unknown): AttorneyToggleAction {
  if (enabled === null || enabled === undefined) {
    return 'unanswered';
  }
  return enabled ? 'enable' : 'confirm-off';
}

/**
 * The question text.
 *
 * Patients get jargon-free wording on the applicant section because they are the one audience
 * who would not necessarily know that "the applicant" means them. Everyone else gets
 * "applicant", which matches the section's own name and the language of the claim.
 */
export function attorneyQuestionText(
  prefix: AttorneySectionPrefix,
  viewer: AttorneyQuestionViewer,
): string {
  if (prefix === 'defenseAttorney') {
    return 'Is there a defense attorney?';
  }
  return viewer.isPatient
    ? 'Do you have an attorney representing you?'
    : 'Is the applicant represented?';
}
