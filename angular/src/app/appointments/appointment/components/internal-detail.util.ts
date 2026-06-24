import type { AppointmentPillStatus } from '../../../shared/ui/status-pill/status-pill.component';

/**
 * Pure helpers for the redesigned internal appointment detail (Prompt 11).
 * Kept out of the component so the status-gated action set is unit-testable
 * without Angular's DI graph.
 */

/** Office actions available on the detail, keyed off the appointment's pill. */
export type DetailAction = 'approve' | 'reject' | 'reschedule' | 'cancel' | 'requestInfo';

/**
 * Which office actions the detail offers at a given pill. Mirrors the
 * prototype's ID_ACT (Pending = approve/reject/reschedule/cancel; Approved +
 * Rescheduled = reschedule/cancel; Rejected/Cancelled = none) and adds
 * request-info on Pending (the staff side of the send-back flow, which the
 * inherited engine fully supports). Server permissions remain authoritative.
 */
export function detailActions(pill: AppointmentPillStatus): DetailAction[] {
  switch (pill) {
    case 'Pending':
      return ['approve', 'reject', 'reschedule', 'cancel', 'requestInfo'];
    case 'Approved':
    case 'Rescheduled':
      return ['reschedule', 'cancel'];
    // Rejected, Cancelled, InfoRequested -> no office actions (terminal or
    // awaiting the requester); the banner + re-request handle those.
    default:
      return [];
  }
}

/** Banner theme variant for a pill (InfoRequested -> the hyphenated key). */
export function bannerVariant(pill: AppointmentPillStatus): string {
  return pill === 'InfoRequested' ? 'info-requested' : pill.toLowerCase();
}

/** Human label for a pill in the status chip ('Info requested' for InfoRequested). */
export function statusLabel(pill: AppointmentPillStatus): string {
  return pill === 'InfoRequested' ? 'Info requested' : pill;
}
