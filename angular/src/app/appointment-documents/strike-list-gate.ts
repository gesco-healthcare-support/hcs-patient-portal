/**
 * AF6 (2026-06-05): the booking-form submit gate for the PQME panel strike list.
 * Blocks submission when the appointment type is PQME AND the booker opted in
 * (checked "I have the panel strike list") AND no staged document has been
 * marked as the strike list. Pure function (structural param, no component or
 * DI dependency) so it is unit-testable in isolation, like
 * appointment-documents/document-upload.validation.ts.
 */
export function isStrikeListGateBlocked(
  isPqme: boolean,
  hasPanelStrikeList: boolean,
  stagedDocuments: readonly { isStrikeList: boolean }[],
): boolean {
  return isPqme && hasPanelStrikeList && !stagedDocuments.some((d) => d.isStrikeList);
}
