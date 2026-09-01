/**
 * Pure helpers for the change-request inbox's APPROVE modal (phase 4b, 2026-08-04).
 *
 * Staff now choose the reschedule date at approval rather than accepting one the requestor
 * picked, so the modal has to distinguish two situations that look alike but are not:
 *
 * - Nothing was proposed at submit (the normal external case since 4b). The staff choice is the
 *   only choice, and Approve must stay blocked until date AND time are set.
 * - A slot WAS proposed (internal staff filing a reschedule). Staff may accept it -- in which
 *   case Approve is immediately live -- or replace it, which is a genuine override and owes an
 *   admin reason the requestor will read.
 *
 * Standalone functions rather than component methods so they unit-test without a TestBed,
 * mirroring cr-inbox.util and the other *-inbox / *-detail utils. The server enforces the same
 * rules (ChangeRequestApprovalValidator); these only drive the button state.
 */

/** True only when staff replaced a slot the requestor actually proposed. */
export function requiresAdminReason(
  proposedSlotId: string | null | undefined,
  chosenSlotId: string | null | undefined,
): boolean {
  return !!proposedSlotId && !!chosenSlotId && proposedSlotId !== chosenSlotId;
}

export interface RescheduleApprovalState {
  /** Slot the requestor proposed at submit, or null -- the normal case since 4b. */
  proposedSlotId: string | null | undefined;
  /** Slot staff picked in this modal, or null when they have not picked. */
  chosenSlotId: string | null | undefined;
  /** Time staff picked alongside the date. Both are needed to identify a slot. */
  chosenTime: string | null | undefined;
  adminReason?: string | null;
}

/**
 * Whether Approve may fire. Mirrors the server's resolve-then-gate order: there must be a slot
 * to schedule onto, and an override must carry its reason.
 */
export function canApproveReschedule(state: RescheduleApprovalState): boolean {
  const { proposedSlotId, chosenSlotId, chosenTime, adminReason } = state;

  // Staff picked in this modal: a slot is only fully identified once a time is chosen too,
  // because one date carries many slots.
  const staffPickComplete = !!chosenSlotId && !!chosenTime;

  if (!proposedSlotId && !staffPickComplete) {
    // Nothing proposed and nothing (fully) chosen -- no date exists to approve onto.
    return false;
  }

  if (requiresAdminReason(proposedSlotId, chosenSlotId)) {
    return staffPickComplete && !!adminReason && adminReason.trim().length > 0;
  }

  return true;
}

/**
 * Phase 4c (2026-08-05) -- which of the three stages the reschedule approve modal is in.
 *
 * Staff now PICK a date, CONFIRM it (which opens a consent round and emails both sides), and
 * only then FINALIZE. The stage is derived from the row's current-round fields rather than
 * held as component state, so a reload lands on the same stage the server believes it is in.
 */
export type ConsentRoundStage = 'needs-date' | 'awaiting-consent' | 'granted';

/** The current-round fields the stage is derived from (a subset of AppointmentChangeRequestDto). */
export interface ConsentRoundRow {
  currentConsentRoundNumber?: number | null;
  currentRoundSideAStatus?: number | null;
  currentRoundSideBStatus?: number | null;
}

/**
 * Mirrors the backend `ChangeRequestConsentStatus` enum. Declared locally rather than imported
 * from the proxy so this file stays a pure, TestBed-free unit -- the values are a persisted
 * contract and do not drift.
 */
const CONSENT_NOT_REQUIRED = 0;
const CONSENT_PENDING = 1;
const CONSENT_APPROVED = 2;
const CONSENT_REJECTED = 3;
const CONSENT_EXPIRED = 4;

/**
 * Which stage the modal should render.
 *
 * A round whose side was REJECTED or EXPIRED goes back to `needs-date`, not to a dead end:
 * the way forward from a declined date is to propose a different one, which supersedes that
 * round and opens the next. That is the whole reason rounds exist as separate rows.
 */
export function rescheduleStage(row: ConsentRoundRow | null | undefined): ConsentRoundStage {
  if (!row?.currentConsentRoundNumber) {
    return 'needs-date';
  }

  const sides = [row.currentRoundSideAStatus, row.currentRoundSideBStatus];

  // A declined or expired side kills the WHOLE round: it can never satisfy the finalize gate,
  // so there is nothing left to wait for even if the other side has not answered. Offering
  // "resend" here would be a dead end -- it only re-asks the still-pending side, and the round
  // would stay unfinalizable however they answer. The only way forward is a new date.
  if (sides.some((s) => s === CONSENT_REJECTED || s === CONSENT_EXPIRED)) {
    return 'needs-date';
  }

  // A side that was never solicited (no representative) is auto-satisfied, matching the
  // server's RescheduleConsentGate.
  const blocked = sides.some((s) => s !== CONSENT_NOT_REQUIRED && s !== CONSENT_APPROVED);
  return blocked ? 'awaiting-consent' : 'granted';
}

/**
 * "Aug 27, 2026 at 10:30", or null when there is no date to show. Shared by the requestor's
 * proposal and the confirmed round's date so the two read identically in the modal.
 */
export function formatSlotLabel(
  isoDate: string | null | undefined,
  fromTime: string | null | undefined,
): string | null {
  if (!isoDate) {
    return null;
  }
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) {
    return null;
  }
  const day = date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
  return fromTime ? `${day} at ${fromTime}` : day;
}

/**
 * Staff-facing wording for one side's consent state. "Not needed" rather than "Not required"
 * because the reason is always that the side has no representative to ask -- staff read it as
 * "nobody to chase", not as "the rule was waived".
 */
export function consentStatusLabel(status: number | null | undefined): string {
  switch (status) {
    case CONSENT_NOT_REQUIRED:
      return 'Not needed';
    case CONSENT_PENDING:
      return 'Awaiting reply';
    case CONSENT_APPROVED:
      return 'Agreed';
    case 3:
      return 'Declined';
    case 4:
      return 'Expired';
    default:
      return 'Not asked';
  }
}

/**
 * Whether "Confirm date & request consent" may fire: a date AND a time identify one slot, and
 * REPLACING a slot the requestor proposed owes the explanation they will read.
 *
 * The admin reason belongs in this gate, not only in the click handler: confirming is what
 * emails both sides, so the button must be visibly unavailable rather than enabled-then-refusing.
 */
export function canConfirmDate(state: {
  slotId: string | null | undefined;
  time: string | null | undefined;
  proposedSlotId?: string | null;
  adminReason?: string | null;
}): boolean {
  if (!state.slotId || !state.time) {
    return false;
  }
  if (requiresAdminReason(state.proposedSlotId, state.slotId)) {
    return !!state.adminReason && state.adminReason.trim().length > 0;
  }
  return true;
}

/**
 * Whether "Finalize reschedule" may fire. Finalize is the step that picks the billing outcome,
 * so it needs one -- and it is only reachable once the current round is fully granted. The
 * server re-checks both (`RescheduleConsentGate`, `EnsureRescheduleOutcome`).
 */
export function canFinalizeReschedule(state: {
  stage: ConsentRoundStage;
  outcome: number | null | undefined;
}): boolean {
  return state.stage === 'granted' && !!state.outcome;
}

/**
 * Item K (2026-08-22) -- what the inbox ROW button should say.
 *
 * <p>It read "Approve" at every stage. On a reschedule that is wrong twice over: the first click
 * opens a modal whose real job is to pick a date and email both sides for consent, and approving is
 * step 3 of 3. Staff reasonably read the label as "this approves the request".</p>
 *
 * <p>A pure function of the two things that decide it, so the wording is testable without standing
 * up the component. Cancellations have no consent round, so "Approve" is accurate for them and is
 * returned unchanged.</p>
 */
export function rowActionLabel(isReschedule: boolean, stage: ConsentRoundStage): string {
  if (!isReschedule) {
    return 'Approve';
  }
  switch (stage) {
    case 'needs-date':
      return 'Set date';
    case 'awaiting-consent':
      return 'Awaiting consent';
    default:
      return 'Approve';
  }
}

/**
 * Item K -- whether this click is the one that genuinely approves. Drives the green, final-looking
 * treatment, so the earlier stages stop looking like an approval they are not.
 */
export function rowActionIsFinal(isReschedule: boolean, stage: ConsentRoundStage): boolean {
  return !isReschedule || stage === 'granted';
}
