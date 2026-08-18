import { BookingMode } from '../shared/booking-mode';

/**
 * Pure copy helpers for the appointment wizard. One component serves two
 * audiences -- external requesters and internal staff booking on a patient's
 * behalf -- so the visible header + review-step note branch on `isInternal`.
 * Kept as standalone functions (not component getters) so they unit-test
 * without a TestBed, mirroring internal-appointments.util.ts /
 * internal-detail.util.ts. External copy is byte-identical to the pre-redesign
 * wizard text so the shipped external flow reads unchanged.
 *
 * Item 4 (2026-08-18): these took an `isReevaluation` boolean until re-book made a third
 * flow visible in the header. A second boolean would give four states of which one
 * ('both a re-evaluation and a re-book') is meaningless, so they take the mode instead.
 */

/** Small eyebrow label above the wizard title. */
export function wizardEyebrow(isInternal: boolean, mode: BookingMode): string {
  if (mode === 'reval') {
    return 'Follow-up evaluation';
  }
  if (mode === 'reBook') {
    return 'Rebooking';
  }
  return isInternal ? 'Staff booking' : 'New evaluation';
}

/** Main wizard heading. Staff "book on behalf"; external users "request". */
export function wizardTitle(isInternal: boolean, mode: BookingMode): string {
  const subject =
    mode === 'reval'
      ? 'a Re-evaluation'
      : mode === 'reBook'
        ? 'a Replacement Appointment'
        : 'an Appointment';
  return isInternal ? `Book ${subject}` : `Request ${subject}`;
}

/** Sub-heading under the title. */
export function wizardSubtitle(isInternal: boolean, mode: BookingMode): string {
  if (mode === 'reval') {
    return isInternal
      ? 'Look up the prior appointment, then confirm the follow-up details on behalf of the patient.'
      : 'Look up the prior appointment, then confirm the details for the follow-up.';
  }
  if (mode === 'reBook') {
    return isInternal
      ? 'Look up the appointment that did not take place, then choose a new date and time on behalf of the patient.'
      : 'Look up the appointment that did not take place, then choose a new date and time.';
  }
  return isInternal
    ? 'Complete the steps below to book on behalf of the patient. Progress is saved automatically as a draft.'
    : 'Complete the steps below. Your progress is saved automatically as a draft.';
}

/**
 * Review-step warning shown when a flow needs a source appointment and none is loaded.
 * Replaces the nested ternaries this sentence used to be assembled from in the template,
 * which did not survive the arrival of a third flow.
 *
 * Returns '' for a plain booking, which has no source -- that branch is unreachable in
 * practice (the banner renders under `isSourceLoadRequired`, false for 'new'), but
 * returning empty is safer than asserting the caller got it right.
 */
export function sourceLookupBanner(mode: BookingMode): string {
  const step = 'by confirmation number on the Schedule step before submitting';
  if (mode === 'reval') {
    return `Load the prior approved appointment ${step} this re-evaluation.`;
  }
  if (mode === 'reBook') {
    return `Load the appointment you want to book again ${step} this booking.`;
  }
  if (mode === 'reRequest') {
    return `Load the prior appointment ${step} this re-request.`;
  }
  return '';
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
