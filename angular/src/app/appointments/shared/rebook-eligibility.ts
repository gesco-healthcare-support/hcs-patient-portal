import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';

/**
 * Item 4 (2026-08-18) -- which appointments can be booked again.
 *
 * A re-book replaces an appointment that did NOT take place. That is the whole rule:
 * the patient cancelled it (either billing tier), did not show, or was not seen. An
 * appointment that happened is followed up with a re-evaluation instead, and a rejected
 * request is re-entered with a re-request -- three different flows with three different
 * server endpoints.
 *
 * Mirrors the server's `CanCreateReBook` (AppointmentLifecycleValidators.cs). The server
 * remains authoritative and re-checks on submit; this list exists so the UI does not
 * offer a "Book again" button whose POST would then be refused, and so the
 * confirmation-number lookup can say why before the booker fills in a form.
 */
export const RE_BOOK_ELIGIBLE_STATUSES: readonly AppointmentStatusType[] = [
  AppointmentStatusType.NoShow,
  AppointmentStatusType.NotSeen,
  AppointmentStatusType.CancelledNoBill,
  AppointmentStatusType.CancelledLate,
];

/** True when `status` is one an appointment can be re-booked from. Undefined is refused. */
export function isReBookEligibleStatus(status: AppointmentStatusType | undefined): boolean {
  return status !== undefined && RE_BOOK_ELIGIBLE_STATUSES.includes(status);
}
