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
 * 2026-07-16 triage (issue #2): Cancel is NOT offered on Pending -- it duplicated
 * Reject (both send a not-yet-approved appointment back), so it was dropped for
 * internal staff. This supersedes B1/C3 (2026-07-01), which had added Cancel on
 * Pending. Cancel stays on Approved/Rescheduled so the office can still cancel an
 * approved appointment via the change-request + consent flow. The backend
 * precondition relaxation + consent pipeline are unchanged; only the Pending
 * button is removed (F-M04 (2026-06-25) had also hidden Cancel on Pending).
 *
 * Phase 4c (2026-08-05): `RescheduleRequested` and `CancellationRequested` fall to the
 * default (no actions) because a request is already in flight -- stacking a second one on an
 * appointment awaiting consent has no coherent meaning. `CancellationRequested` already
 * behaved this way (it used to map to the `Cancelled` pill); `RescheduleRequested` did NOT,
 * so this REMOVES two buttons that render on a reschedule-requested appointment today. That is
 * an intentional behaviour change, not a regression.
 */
export function detailActions(pill: AppointmentPillStatus): DetailAction[] {
  switch (pill) {
    case 'Pending':
      return ['approve', 'reject', 'reschedule', 'requestInfo'];
    case 'Approved':
    case 'Rescheduled':
      return ['reschedule', 'cancel'];
    // Rejected, Cancelled, InfoRequested, and the two in-flight REQUESTED pills -> no office
    // actions (terminal, awaiting the requester, or awaiting consent); the banner handles those.
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

/**
 * Banner theme variant for a pill. Multi-word pills need an explicit kebab-case key --
 * `toLowerCase()` alone would yield `reschedulerequested`, which matches no CALLOUTS entry and
 * would silently fall back to the generic pending copy.
 */
const BANNER_VARIANTS: Partial<Record<AppointmentPillStatus, string>> = {
  InfoRequested: 'info-requested',
  RescheduleRequested: 'reschedule-requested',
  CancellationRequested: 'cancellation-requested',
  // Phase 5 (2026-08-07). REQUIRED, not cosmetic: without these the fallback yields
  // `noshow` / `notseen`, which match no CALLOUTS key, so the banner would silently
  // drop to the generic pending copy on a settled appointment -- exactly the failure
  // this map exists to prevent.
  NoShow: 'no-show',
  NotSeen: 'not-seen',
};

export function bannerVariant(pill: AppointmentPillStatus): string {
  return BANNER_VARIANTS[pill] ?? pill.toLowerCase();
}

/**
 * Human label for a pill in the status chip. Multi-word pills need an explicit entry --
 * without one the raw PascalCase key renders as a single run-on word.
 */
const STATUS_LABELS: Partial<Record<AppointmentPillStatus, string>> = {
  InfoRequested: 'Info requested',
  RescheduleRequested: 'Reschedule requested',
  CancellationRequested: 'Cancellation requested',
  // Long-standing business names, kept verbatim rather than sentence-cased to match
  // the neighbours above -- Adrian: "these are long used names throughout the
  // business". Without an entry the raw key renders as the run-on "NoShow".
  NoShow: 'No Show',
  NotSeen: 'Not Seen',
};

export function statusLabel(pill: AppointmentPillStatus): string {
  return STATUS_LABELS[pill] ?? pill;
}
