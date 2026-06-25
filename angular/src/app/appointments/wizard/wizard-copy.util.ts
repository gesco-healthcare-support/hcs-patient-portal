/**
 * Pure copy helpers for the appointment wizard. One component serves two
 * audiences -- external requesters and internal staff booking on a patient's
 * behalf -- so the visible header + review-step note branch on `isInternal`.
 * Kept as standalone functions (not component getters) so they unit-test
 * without a TestBed, mirroring internal-appointments.util.ts /
 * internal-detail.util.ts. External copy is byte-identical to the pre-redesign
 * wizard text so the shipped external flow reads unchanged.
 */

/** Small eyebrow label above the wizard title. */
export function wizardEyebrow(isInternal: boolean, isReevaluation: boolean): string {
  if (isReevaluation) {
    return 'Follow-up evaluation';
  }
  return isInternal ? 'Staff booking' : 'New evaluation';
}

/** Main wizard heading. Staff "book on behalf"; external users "request". */
export function wizardTitle(isInternal: boolean, isReevaluation: boolean): string {
  if (isInternal) {
    return isReevaluation ? 'Book a Re-evaluation' : 'Book an Appointment';
  }
  return isReevaluation ? 'Request a Re-evaluation' : 'Request an Appointment';
}

/** Sub-heading under the title. */
export function wizardSubtitle(isInternal: boolean, isReevaluation: boolean): string {
  if (isInternal) {
    return isReevaluation
      ? 'Look up the prior appointment, then confirm the follow-up details on behalf of the patient.'
      : 'Complete the steps below to book on behalf of the patient. Progress is saved automatically as a draft.';
  }
  return isReevaluation
    ? 'Look up the prior appointment, then confirm the details for the follow-up.'
    : 'Complete the steps below. Your progress is saved automatically as a draft.';
}

/**
 * Review-step note shown above the submit button. The external copy warns the
 * requester they cannot self-edit after submit; staff CAN edit afterward from
 * the appointment record, so the patient-voiced copy is replaced for them.
 */
export function reviewSubmitNote(isInternal: boolean): string {
  return isInternal
    ? 'Review every step above, then submit to book on behalf of the patient. Staff can edit the appointment afterward from the appointment record.'
    : 'Review every step above, then submit. Once submitted you cannot edit the request yourself -- contact staff to make changes.';
}
