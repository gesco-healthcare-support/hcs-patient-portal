import type { AppointmentPillStatus } from '../../../shared/ui/status-pill/status-pill.component';

/**
 * Pure helpers for the redesigned internal appointment detail (Prompt 11).
 * Kept out of the component so the status-gated action set is unit-testable
 * without Angular's DI graph.
 */

/** Office actions available on the detail, keyed off the appointment's pill. */
export type DetailAction = 'approve' | 'reject' | 'reschedule' | 'cancel' | 'requestInfo';

/**
 * Which office actions the detail offers at a given pill. Approved +
 * Rescheduled = reschedule/cancel; Rejected/Cancelled = none. Pending offers
 * approve/reject/reschedule + request-info (the staff side of the send-back
 * flow). Server permissions remain authoritative.
 *
 * F-M04 (2026-06-25): Cancel is NOT offered on Pending. The domain
 * (SubmitCancellationAsync) rejects cancelling a Pending appointment -- Reject
 * is the correct terminal action there -- so showing Cancel only produced a 403.
 */
export function detailActions(pill: AppointmentPillStatus): DetailAction[] {
  switch (pill) {
    case 'Pending':
      return ['approve', 'reject', 'reschedule', 'requestInfo'];
    case 'Approved':
    case 'Rescheduled':
      return ['reschedule', 'cancel'];
    // Rejected, Cancelled, InfoRequested -> no office actions (terminal or
    // awaiting the requester); the banner + re-request handle those.
    default:
      return [];
  }
}

/** Minimal shape {@link resolveBookerEmail} reads off the detail's appointment. */
export interface BookerEmailSource {
  bookedByUser?: { email?: string | null; userName?: string | null } | null;
  identityUser?: { email?: string | null; userName?: string | null } | null;
}

/**
 * The "Booker (identity)" value for the internal detail. QA F-011: prefer the
 * ACTUAL booker (BookedByUserId, resolved server-side) over the identity user
 * (patient/owner); fall back to the identity only for legacy rows booked before
 * BookedByUserId existed. Pulled out of the component so it is unit-testable
 * without the DI graph.
 */
export function resolveBookerEmail(appointment: BookerEmailSource | null | undefined): string {
  const booker = appointment?.bookedByUser;
  return (
    booker?.email ??
    booker?.userName ??
    appointment?.identityUser?.email ??
    appointment?.identityUser?.userName ??
    ''
  );
}

/** Banner theme variant for a pill (InfoRequested -> the hyphenated key). */
export function bannerVariant(pill: AppointmentPillStatus): string {
  return pill === 'InfoRequested' ? 'info-requested' : pill.toLowerCase();
}

/** Human label for a pill in the status chip ('Info requested' for InfoRequested). */
export function statusLabel(pill: AppointmentPillStatus): string {
  return pill === 'InfoRequested' ? 'Info requested' : pill;
}
