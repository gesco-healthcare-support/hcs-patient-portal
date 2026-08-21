/**
 * The patient "Unit #", in the one place it is decided (2026-08-13).
 *
 * The unit used to live in TWO columns. The booking wizard and the send-back flow wrote
 * `Patient.Address`; every staff-facing screen wrote `Patient.ApptNumber`. Both boxes were labelled
 * "Unit #", so nobody using the system could tell them apart, and the Case Tracker payload read only
 * `Address` -- which meant a staff correction never reached them while the stale booking-time value
 * kept going out.
 *
 * Everything now writes `apptNumber`. `address` survives as a READ-ONLY fallback for patients booked
 * before the change, which were deliberately not backfilled: this data ends up in proof-of-service
 * documents and guessing whether an old value is a genuine unit or a mis-keyed street line is not
 * something to automate.
 *
 * The wizard's form control keeps the name `address` on purpose -- it is threaded through the address
 * autocomplete (`suite: 'address'`), the review step, validation and specs, and renaming it would be a
 * wide change to the highest-traffic flow for no behaviour gain. The translation happens here instead.
 */
export interface PatientUnitSource {
  apptNumber?: string | null;
  address?: string | null;
}

/**
 * The unit to PREFILL into the form, preferring the column staff write.
 *
 * Returns null rather than undefined because it feeds a reactive control, and `patchValue(undefined)`
 * leaves the previous value in place instead of clearing it.
 */
export function unitForForm(patient: PatientUnitSource | null | undefined): string | null {
  return patient?.apptNumber ?? patient?.address ?? null;
}

/**
 * The unit to SEND to the server, from the wizard's `address` control.
 *
 * Returns undefined for a blank so the field is omitted rather than sent as an empty string: the
 * update path treats an omitted field as "leave it alone", and clearing a unit the user never touched
 * would be a silent data loss.
 */
export function unitToDto(controlValue: string | null | undefined): string | undefined {
  const trimmed = controlValue?.trim();
  return trimmed ? trimmed : undefined;
}
