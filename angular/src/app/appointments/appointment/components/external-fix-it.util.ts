/**
 * Pure helpers for the external "fix-it" flow (status = Info requested). Kept free of
 * Angular/DI so the progress maths + the corrections-payload shaping are unit-tested
 * directly; the component owns the HTTP + form wiring.
 *
 * QA item L (2026-06-30): the flow now covers every flaggable scalar field (62), so
 * these helpers are registry-driven (keys from send-back-fields) rather than a fixed
 * 13-key map. The corrections payload is a generic field-key -> value map mirroring the
 * backend SaveInfoRequestCorrectionsInput.Corrections dictionary.
 */
import { FLAGGABLE_FIELDS } from '../send-back-fields';
import type { AppointmentInjuryDraft } from '../../sections/appointment-add-claim-information.component';

/** Editor widget a flagged field uses on the fix-it page. */
export type FieldKind = 'text' | 'date' | 'state' | 'language' | 'gender' | 'document';

/**
 * The editor kind for a flaggable key, derived from the key. State sub-fields exist on
 * several sections (patient, employer, both attorneys, insurance, claim examiner); they
 * all end in "StateId" (the patient's is the bare "stateId").
 */
export function fieldKind(key: string): FieldKind {
  if (key === 'documents') {
    return 'document';
  }
  if (key === 'genderId') {
    return 'gender';
  }
  if (key === 'appointmentLanguageId') {
    return 'language';
  }
  if (key === 'dateOfBirth') {
    return 'date';
  }
  if (key === 'stateId' || key.endsWith('StateId')) {
    return 'state';
  }
  return 'text';
}

const LABELS = new Map(FLAGGABLE_FIELDS.map((f) => [f.key, { label: f.label, group: f.group }]));

/**
 * A disambiguated display label for the fix-it list. Several sections share short field
 * names (Street, ZIP code, Phone), so the section is prefixed ("Employer Details: Street")
 * except where it would just repeat the label (Documents).
 */
export function fieldLabelOf(key: string): string {
  const meta = LABELS.get(key);
  if (!meta) {
    return key;
  }
  return meta.group === meta.label ? meta.label : `${meta.group}: ${meta.label}`;
}

/**
 * A flagged field is edited inline on the fix-it page unless it is the documents flag
 * (handled by re-upload) or the dropped claim-information section pseudo-key.
 */
export function isInlineEditable(key: string): boolean {
  return key !== 'documents' && key !== 'claimInformation';
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
 * The corrections payload sent to the locked corrections endpoint: a field-key -> value
 * map mirroring the backend SaveInfoRequestCorrectionsInput.Corrections. State / language
 * ids and gender are carried as their id/enum string. Document replacement is NOT carried
 * here -- it goes through the existing document upload.
 */
export type CorrectionsPayload = Record<string, string>;

/**
 * Build the corrections map from edited values, including only keys that are BOTH flagged
 * and inline-editable and have a non-empty trimmed edit. The server re-locks to the
 * flagged set, so this is the UI half of the only-flagged-fields rule.
 */
export function buildCorrectionsPayload(
  flaggedKeys: readonly string[],
  edits: Readonly<Record<string, string>>,
): CorrectionsPayload {
  const flagged = new Set(flaggedKeys);
  const payload: CorrectionsPayload = {};
  for (const key of Object.keys(edits)) {
    if (!flagged.has(key) || !isInlineEditable(key)) {
      continue;
    }
    const value = edits[key]?.trim();
    if (value) {
      payload[key] = value;
    }
  }
  return payload;
}

/**
 * One corrected Claim Information (injury-detail) row exchanged with the corrections
 * endpoint (QA item 11). Mirrors the backend InjuryDetailCorrectionDto; the appointment id
 * is the route param, so it is not carried here.
 */
export interface InjuryCorrectionRow {
  dateOfInjury: string | null;
  toDateOfInjury: string | null;
  claimNumber: string;
  isCumulativeInjury: boolean;
  wcabAdj: string | null;
  bodyPartsSummary: string;
  wcabOfficeId: string | null;
}

/**
 * Map a stored injury row into the booking-wizard section's transient draft shape so the
 * reused editor can prefill. The comma-joined summary is split back into structured body
 * parts; dates are trimmed to yyyy-MM-dd for the native date inputs. Null-safe on every
 * field so a partially-populated row never throws.
 */
export function injuryRowToDraft(r: InjuryCorrectionRow): AppointmentInjuryDraft {
  const summary = (r.bodyPartsSummary ?? '').trim();
  const bodyParts = summary
    ? summary
        .split(',')
        .map((s) => s.trim())
        .filter((s) => s.length > 0)
    : [];
  return {
    isCumulativeInjury: r.isCumulativeInjury === true,
    dateOfInjury: (r.dateOfInjury ?? '').slice(0, 10) || null,
    toDateOfInjury: (r.toDateOfInjury ?? '').slice(0, 10) || null,
    claimNumber: r.claimNumber ?? '',
    wcabOfficeId: r.wcabOfficeId ?? null,
    wcabAdj: r.wcabAdj ?? null,
    bodyParts,
    bodyPartsSummary: summary,
  };
}

/**
 * Map the editor's drafts into the corrections-endpoint replacement set. The wizard section
 * already derives bodyPartsSummary from the structured rows on edit, so it passes through.
 */
export function buildInjuryCorrections(
  drafts: readonly AppointmentInjuryDraft[],
): InjuryCorrectionRow[] {
  return drafts.map((d) => ({
    dateOfInjury: d.dateOfInjury,
    toDateOfInjury: d.toDateOfInjury,
    claimNumber: d.claimNumber,
    isCumulativeInjury: d.isCumulativeInjury,
    wcabAdj: d.wcabAdj,
    bodyPartsSummary: d.bodyPartsSummary,
    wcabOfficeId: d.wcabOfficeId,
  }));
}
