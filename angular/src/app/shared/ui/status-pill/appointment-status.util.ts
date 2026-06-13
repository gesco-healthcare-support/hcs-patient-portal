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
 * 6-status pills. The backend still carries deprecated values (NoShow,
 * CheckedIn, CheckedOut, Billed, and the Cancelled/Rescheduled bill variants);
 * the redesigned UI buckets every value into one of the six pills:
 *
 *   Approved   <- Approved, CheckedIn, CheckedOut, Billed (post-approval states)
 *   Rejected   <- Rejected
 *   Cancelled  <- CancelledNoBill, CancelledLate, CancellationRequested, NoShow
 *   Rescheduled<- RescheduledNoBill, RescheduledLate, RescheduleRequested
 *   InfoRequested <- InfoRequested
 *   Pending    <- Pending (and anything unknown)
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
    case AppointmentStatusType.CancellationRequested:
    case AppointmentStatusType.NoShow:
      return 'Cancelled';
    case AppointmentStatusType.RescheduledNoBill:
    case AppointmentStatusType.RescheduledLate:
    case AppointmentStatusType.RescheduleRequested:
      return 'Rescheduled';
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

const PILL_TO_SEGMENT: Record<AppointmentPillStatus, Exclude<ExternalStatusSegment, 'all'>> = {
  Pending: 'pending',
  InfoRequested: 'info',
  Approved: 'approved',
  Rescheduled: 'rescheduled',
  Cancelled: 'cancelled',
  Rejected: 'rejected',
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
