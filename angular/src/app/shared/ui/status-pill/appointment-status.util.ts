import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { AppointmentPillStatus } from './status-pill.component';

// InfoRequested = 14 exists in the backend enum (Domain.Shared) but the Angular
// proxy has not been regenerated yet. Reference the numeric value until the next
// `abp generate-proxy` adds the member, then switch to
// AppointmentStatusType.InfoRequested. (Mirrors the local-type pattern in
// appointments/appointment/appointment-add.component.ts.)
const INFO_REQUESTED_STATUS = 14 as AppointmentStatusType;

/**
 * Maps the (legacy-inclusive) AppointmentStatusType enum onto the redesign's
 * status pills. The backend still carries deprecated values (NoShow,
 * CheckedIn, CheckedOut, Billed, and the Cancelled/Rescheduled bill variants);
 * the redesigned UI buckets every value into one pill:
 *
 *   Approved   <- Approved, CheckedIn, CheckedOut, Billed (post-approval states)
 *   Rejected   <- Rejected
 *   Cancelled  <- CancelledNoBill, CancelledLate
 *   CancellationRequested <- CancellationRequested
 *   Rescheduled<- RescheduledNoBill, RescheduledLate
 *   RescheduleRequested <- RescheduleRequested
 *   InfoRequested <- InfoRequested
 *   NoShow     <- NoShow
 *   NotSeen    <- NotSeen
 *   Pending    <- Pending (and anything unknown)
 *
 * Phase 5 (2026-08-07) took NoShow OUT of the Cancelled bucket. That mapping was a
 * MISLABELLING, not a simplification: the appointment was not cancelled, the patient
 * did not arrive -- and the backend StatusPillPolicy meanwhile returned null for it,
 * so the two sides of this "mirror" disagreed. NotSeen (the patient arrived but was
 * not evaluated) joins it. Both use the business's own long-standing names.
 *
 * Phase 4c (2026-08-05) split the two REQUESTED states out of their terminal pills. They used
 * to bucket into `Rescheduled` / `Cancelled`, so an in-flight request rendered as though it had
 * already happened -- and the external detail banner went further and asserted "This
 * appointment has been rescheduled" while nothing had moved. Both are pre-existing defects on
 * `main`; 4c fixes them because it lengthens the in-flight window considerably.
 *
 * The new pills still map to the EXISTING filter segments (see `PILL_TO_SEGMENT`), so no
 * seventh chip appears and chip counts do not move -- only the pill text becomes honest.
 */
export function appointmentStatusToPill(status: AppointmentStatusType): AppointmentPillStatus {
  switch (status) {
    case AppointmentStatusType.Approved:
    case AppointmentStatusType.CheckedIn:
    case AppointmentStatusType.CheckedOut:
    case AppointmentStatusType.Billed:
      return 'Approved';
    case AppointmentStatusType.Rejected:
      return 'Rejected';
    case AppointmentStatusType.CancelledNoBill:
    case AppointmentStatusType.CancelledLate:
      return 'Cancelled';
    case AppointmentStatusType.NoShow:
      return 'NoShow';
    case AppointmentStatusType.NotSeen:
      return 'NotSeen';
    case AppointmentStatusType.CancellationRequested:
      return 'CancellationRequested';
    case AppointmentStatusType.RescheduledNoBill:
    case AppointmentStatusType.RescheduledLate:
      return 'Rescheduled';
    case AppointmentStatusType.RescheduleRequested:
      return 'RescheduleRequested';
    case INFO_REQUESTED_STATUS:
      return 'InfoRequested';
    case AppointmentStatusType.Pending:
    default:
      return 'Pending';
  }
}

/** The status-segment keys shown as filter chips on the external home + lists. */
export type ExternalStatusSegment =
  | 'all'
  | 'pending'
  | 'info'
  | 'approved'
  | 'rescheduled'
  | 'cancelled'
  | 'rejected';

/**
 * The two REQUESTED pills deliberately share their terminal pill's segment: an in-flight
 * reschedule still belongs under the "Rescheduled" chip, so no seventh chip is needed and every
 * existing chip count stays exactly as it was.
 */
const PILL_TO_SEGMENT: Record<AppointmentPillStatus, Exclude<ExternalStatusSegment, 'all'>> = {
  Pending: 'pending',
  InfoRequested: 'info',
  Approved: 'approved',
  Rescheduled: 'rescheduled',
  RescheduleRequested: 'rescheduled',
  Cancelled: 'cancelled',
  CancellationRequested: 'cancelled',
  Rejected: 'rejected',
  // Phase 5 (2026-08-07): the two attendance outcomes get their own honest PILL but
  // keep filtering under the chip NoShow already filtered under, so no seventh chip
  // appears and no external chip count moves -- the same trade 4c made for the
  // REQUESTED states. The internal dashboard donut DOES get a slice each; that is a
  // separate surface (StatusPillPolicy.DonutOrder) and staff want the volume.
  NoShow: 'cancelled',
  NotSeen: 'cancelled',
};

export function appointmentStatusToSegment(
  status: AppointmentStatusType,
): Exclude<ExternalStatusSegment, 'all'> {
  return PILL_TO_SEGMENT[appointmentStatusToPill(status)];
}

/** Segment chip definitions in display order (label + the alert highlight flag). */
export const EXTERNAL_STATUS_SEGMENTS: ReadonlyArray<{
  key: ExternalStatusSegment;
  label: string;
  alert?: boolean;
}> = [
  { key: 'all', label: 'All' },
  { key: 'pending', label: 'Pending' },
  { key: 'info', label: 'Info Requested' },
  { key: 'approved', label: 'Approved' },
  { key: 'rescheduled', label: 'Rescheduled' },
  { key: 'cancelled', label: 'Cancelled' },
  { key: 'rejected', label: 'Rejected', alert: true },
];
