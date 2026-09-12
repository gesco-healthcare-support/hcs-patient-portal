/**
 * The four flows the booking form serves. Resolved from the route query params in
 * `AppointmentAddComponent`'s constructor and read by everything downstream that has to
 * branch: heading copy, the source-lookup panel, the appointment-type filter, and which
 * create endpoint submit calls.
 *
 * Extracted to its own file (2026-08-18, item 4) so the wizard's pure copy helpers can
 * take the mode without importing the 3,000-line component that owns the field.
 *
 *   'new'       -- `?type=1` or no type. Plain create.
 *   'reval'     -- `?type=2`. Follow-up to an appointment that DID happen; the booker
 *                  enters a prior APPROVED confirmation number and the form prefills.
 *   'reRequest' -- `?mode=rerequest&source=<conf#>`, launched from a REJECTED appointment.
 *                  Auto-loads, and submits under the SOURCE's confirmation number.
 *   'reBook'    -- `?type=3`, optionally `&source=<conf#>`. Replaces an appointment that
 *                  did NOT happen (cancelled, no-showed, not-seen) with a new one under a
 *                  fresh confirmation number.
 */
export type BookingMode = 'new' | 'reval' | 'reRequest' | 'reBook';

/**
 * Resolve the mode from the `type` query param. Re-request is NOT decided here -- it
 * arrives as `?mode=rerequest` and is matched before this is reached.
 *
 * Anything unrecognised falls back to 'new' rather than throwing: a stale bookmark or a
 * typo should open the ordinary booking form, not a broken page.
 */
export function resolveBookingModeFromType(type: string | null): BookingMode {
  if (type === '2') return 'reval';
  if (type === '3') return 'reBook';
  return 'new';
}
