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
