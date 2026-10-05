/**
 * #1107 -- whether the booking form should tell the booker that an existing patient's stored
 * details are used as they are.
 *
 * Mirrors the server rule in `PatientBookingEditAccess.CanEdit` (#598): only staff and the
 * patient's own login have their edits to an existing patient applied. Everyone else still gets the
 * booking, but the edits are dropped. The server is the control; this only stops the form from
 * implying an edit is accepted.
 *
 * Staff are the roles the server calls internal. The Patient role never reaches this on another
 * person's record. An unknown (empty) role list shows nothing, because the form cannot yet say who
 * is booking.
 */
const EDITING_ROLES = ['patient', 'admin', 'intake staff', 'staff supervisor', 'it admin'];

export function storedPatientDetailsAreFinal(
  roles: readonly string[] | null | undefined,
  hasExistingPatientLoaded: boolean,
): boolean {
  if (!hasExistingPatientLoaded || !roles || roles.length === 0) {
    return false;
  }
  const lowered = roles.map((r) => r?.trim().toLowerCase());
  return !lowered.some((r) => EDITING_ROLES.includes(r));
}
