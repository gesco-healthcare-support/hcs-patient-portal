/**
 * Pure helpers for the external "fix-it" flow (status = Info requested). Kept
 * free of Angular/DI so the progress maths + the corrections-payload shaping are
 * unit-tested directly; the component owns the HTTP + form wiring.
 */

/** Where each flaggable key is corrected on the fix-it page. */
export type FixSource =
  | 'patient'
  | 'appointment-aa'
  | 'appointment-ce'
  | 'insurance'
  | 'defense'
  | 'document';

/** Maps each flaggable-field key (send-back-fields registry) to its fix source. */
export const FIX_SOURCE: Record<string, FixSource> = {
  dateOfBirth: 'patient',
  socialSecurityNumber: 'patient',
  street: 'patient',
  city: 'patient',
  stateId: 'patient',
  zipCode: 'patient',
  cellPhoneNumber: 'patient',
  appointmentLanguageId: 'patient',
  applicantAttorneyEmail: 'appointment-aa',
  appointmentClaimExaminerEmail: 'appointment-ce',
  appointmentInsuranceName: 'insurance',
  defenseAttorneyFirmName: 'defense',
  documents: 'document',
};

/** A flagged field is inline-editable unless it is a document (handled by upload). */
export function isInlineEditable(key: string): boolean {
  return FIX_SOURCE[key] !== undefined && FIX_SOURCE[key] !== 'document';
}

export interface FixItProgress {
  fixed: number;
  total: number;
}

/** Progress = how many flagged fields the requester has addressed. */
export function fixItProgress(
  flaggedKeys: readonly string[],
  touched: ReadonlySet<string>,
): FixItProgress {
  return {
    total: flaggedKeys.length,
    fixed: flaggedKeys.filter((k) => touched.has(k)).length,
  };
}

/** Resubmit is allowed only once every flagged field has been addressed. */
export function allFixed(flaggedKeys: readonly string[], touched: ReadonlySet<string>): boolean {
  return flaggedKeys.length > 0 && flaggedKeys.every((k) => touched.has(k));
}

/**
 * The corrections payload sent to the locked corrections endpoint. Mirrors the
 * backend SaveInfoRequestCorrectionsInput (camelCase). Document replacement is
 * NOT carried here -- it goes through the existing document upload.
 */
export interface CorrectionsPayload {
  dateOfBirth?: string;
  socialSecurityNumber?: string;
  street?: string;
  city?: string;
  /** State is a Guid; carried as its string id (mirrors appointmentLanguageId). */
  stateId?: string;
  zipCode?: string;
  cellPhoneNumber?: string;
  appointmentLanguageId?: string;
  applicantAttorneyEmail?: string;
  claimExaminerEmail?: string;
  insuranceName?: string;
  defenseAttorneyFirmName?: string;
}

/** Maps a flaggable key to its payload property (null = not carried in corrections). */
const PAYLOAD_KEY: Record<string, keyof CorrectionsPayload | null> = {
  dateOfBirth: 'dateOfBirth',
  socialSecurityNumber: 'socialSecurityNumber',
  street: 'street',
  city: 'city',
  stateId: 'stateId',
  zipCode: 'zipCode',
  cellPhoneNumber: 'cellPhoneNumber',
  appointmentLanguageId: 'appointmentLanguageId',
  applicantAttorneyEmail: 'applicantAttorneyEmail',
  appointmentClaimExaminerEmail: 'claimExaminerEmail',
  appointmentInsuranceName: 'insuranceName',
  defenseAttorneyFirmName: 'defenseAttorneyFirmName',
  documents: null,
};

/**
 * Build the corrections payload from edited values, including only fields that
 * are BOTH flagged and have a non-empty trimmed edit. The server re-locks to the
 * flagged set, so this is the UI half of the only-flagged-fields rule.
 */
export function buildCorrectionsPayload(
  flaggedKeys: readonly string[],
  edits: Readonly<Record<string, string>>,
): CorrectionsPayload {
  const flagged = new Set(flaggedKeys);
  const payload: CorrectionsPayload = {};
  for (const key of Object.keys(edits)) {
    if (!flagged.has(key)) {
      continue;
    }
    const target = PAYLOAD_KEY[key];
    const value = edits[key]?.trim();
    if (target && value) {
      payload[target] = value;
    }
  }
  return payload;
}
