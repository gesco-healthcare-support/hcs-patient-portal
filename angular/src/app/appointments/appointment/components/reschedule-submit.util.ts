/**
 * Pure submit-eligibility rule for the reschedule request modal (phase 4b, 2026-08-04).
 *
 * The modal is role-split. Internal staff filing a reschedule pick the new date with the shared
 * availability calendar, so they must supply date + time + reason. External requestors get NO
 * date control at all -- staff choose the date at approval -- so a reason alone is a complete
 * request. Extracted as a standalone function so both branches unit-test without a TestBed,
 * matching the *.util.spec.ts pattern used across this folder.
 */

/** Mirrors AppointmentChangeRequestConsts.ReasonMaxLength. */
export const RESCHEDULE_REASON_MAX_LENGTH = 500;

export interface RescheduleSubmitState {
  /** True when the filer is internal staff, who pick the date themselves. */
  requesterIsStaff: boolean;
  /** doctorAvailabilityId the calendar resolved, or null. Ignored for external filers. */
  slotId: string | null | undefined;
  /** Time chosen alongside the date; a date alone does not identify a slot. */
  time: string | null | undefined;
  reason: string | null | undefined;
  maxReasonLength?: number;
}

/**
 * `abp-modal` options for the reschedule modal. Staff get the wide dialog because the two-month
 * datepicker popup overflows ABP's default `size: 'md'` (Bootstrap's 500px); external requestors
 * see only a reason box and keep the narrow one.
 *
 * THE RETURN VALUE MUST BE REFERENTIALLY STABLE. `ModalComponent.options` is a SIGNAL input, so a
 * binding that yields a fresh object literal each time sets the signal to a new identity on every
 * change-detection pass, re-dirtying the view and re-running change detection forever -- which
 * hung the browser tab outright. It was silent because the served bundle is a PRODUCTION build,
 * where Angular's dev-mode infinite-CD guard is compiled out. Hence the frozen module constants.
 */
const WIDE_MODAL_OPTIONS = Object.freeze({ size: 'lg' as const });
const DEFAULT_MODAL_OPTIONS = Object.freeze({});

export function rescheduleModalOptions(requesterIsStaff: boolean): object {
  return requesterIsStaff ? WIDE_MODAL_OPTIONS : DEFAULT_MODAL_OPTIONS;
}

export function canSubmitReschedule(state: RescheduleSubmitState): boolean {
  const { requesterIsStaff, slotId, time, reason } = state;
  const maxReasonLength = state.maxReasonLength ?? RESCHEDULE_REASON_MAX_LENGTH;

  const reasonText = reason ?? '';
  const reasonOk = reasonText.trim().length > 0 && reasonText.length <= maxReasonLength;
  if (!reasonOk) {
    return false;
  }

  // External requestors propose no date, so there is nothing further to require.
  return requesterIsStaff ? !!slotId && !!time : true;
}
