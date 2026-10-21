/**
 * Staff "Edit details" party changes (2026-10-21): builds the body for
 * PUT /api/app/appointments/{id}/parties from the edit form.
 *
 * Only a party staff actually changed in this edit session is sent. An untouched
 * party is left out (null), so a save that only fixes, say, the employer never
 * rewrites an attorney, never changes the appointment's party-email columns and
 * never mails anyone. That matters for the patient in particular: the form's
 * patient email is loaded from the Patient record, which can differ from the
 * appointment's own column, so sending it on every save would look like a new
 * patient address and mail it.
 */

export type AttorneyPrefix = 'applicantAttorney' | 'defenseAttorney';

export interface StaffAttorneyChange {
  remove: boolean;
  attorneyId?: string | null;
  firstName?: string;
  lastName?: string;
  email?: string;
  firmName?: string;
  webAddress?: string;
  phoneNumber?: string;
  faxNumber?: string;
  street?: string;
  city?: string;
  stateId?: string | null;
  zipCode?: string;
}

export interface StaffClaimExaminerChange {
  remove: boolean;
  name?: string;
  email?: string;
  phoneNumber?: string;
  fax?: string;
  suite?: string;
  street?: string;
  city?: string;
  stateId?: string | null;
  zip?: string;
}

export interface AppointmentPartiesUpdate {
  patientEmail?: string | null;
  applicantAttorney?: StaffAttorneyChange | null;
  defenseAttorney?: StaffAttorneyChange | null;
  claimExaminer?: StaffClaimExaminerChange | null;
}

/** The claim examiner editor's controls, as the internal page holds them. */
export interface ClaimExaminerFormValue {
  name?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  fax?: string | null;
  suite?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zip?: string | null;
}

type FormRecord = Record<string, unknown>;

const ATTORNEY_FIELDS = [
  'FirstName',
  'LastName',
  'Email',
  'FirmName',
  'WebAddress',
  'PhoneNumber',
  'FaxNumber',
  'Street',
  'City',
  'StateId',
  'ZipCode',
] as const;

const CLAIM_EXAMINER_FIELDS = [
  'name',
  'email',
  'phoneNumber',
  'fax',
  'suite',
  'street',
  'city',
  'stateId',
  'zip',
] as const;

/** Trimmed text, or undefined for empty, so an empty field is not sent as "". */
function text(value: unknown): string | undefined {
  if (value === null || value === undefined) {
    return undefined;
  }
  const trimmed = String(value).trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function sameText(a: unknown, b: unknown): boolean {
  return (text(a) ?? '') === (text(b) ?? '');
}

function sameEmail(a: unknown, b: unknown): boolean {
  return (text(a) ?? '').toLowerCase() === (text(b) ?? '').toLowerCase();
}

/** True when any of the attorney's fields differ between the two form values. */
export function attorneyChanged(
  raw: FormRecord,
  initial: FormRecord,
  prefix: AttorneyPrefix,
): boolean {
  return ATTORNEY_FIELDS.some((f) => !sameText(raw[prefix + f], initial[prefix + f]));
}

/** True when any field typed into the attorney section is non-empty. */
function attorneyHasContent(raw: FormRecord, prefix: AttorneyPrefix): boolean {
  return ATTORNEY_FIELDS.some((f) => text(raw[prefix + f]) !== undefined);
}

/**
 * The attorney's change, or null when staff left the section as it was.
 *
 * `loadedAttorneyId` (the master record the page loaded) is sent only while the
 * email still matches the one it was loaded with. Replacing attorney A with B
 * must not write B into A's record; the server applies the same rule.
 */
export function attorneyChange(
  raw: FormRecord,
  initial: FormRecord,
  prefix: AttorneyPrefix,
  removed: boolean,
  loadedAttorneyId: string | null | undefined,
): StaffAttorneyChange | null {
  if (removed) {
    return attorneyHasContent(initial, prefix) ? { remove: true } : null;
  }
  if (!attorneyChanged(raw, initial, prefix) || !attorneyHasContent(raw, prefix)) {
    return null;
  }
  const email = text(raw[prefix + 'Email']);
  return {
    remove: false,
    attorneyId: sameEmail(email, initial[prefix + 'Email']) ? (loadedAttorneyId ?? null) : null,
    firstName: text(raw[prefix + 'FirstName']),
    lastName: text(raw[prefix + 'LastName']),
    email,
    firmName: text(raw[prefix + 'FirmName']),
    webAddress: text(raw[prefix + 'WebAddress']),
    phoneNumber: text(raw[prefix + 'PhoneNumber']),
    faxNumber: text(raw[prefix + 'FaxNumber']),
    street: text(raw[prefix + 'Street']),
    city: text(raw[prefix + 'City']),
    stateId: (raw[prefix + 'StateId'] as string | null | undefined) ?? null,
    zipCode: text(raw[prefix + 'ZipCode']),
  };
}

/** The claim examiner's change, or null when staff left it as it was. */
export function claimExaminerChange(
  value: ClaimExaminerFormValue,
  initial: ClaimExaminerFormValue,
  removed: boolean,
): StaffClaimExaminerChange | null {
  const hasContent = (v: ClaimExaminerFormValue) =>
    CLAIM_EXAMINER_FIELDS.some((f) => text(v[f]) !== undefined);
  if (removed) {
    return hasContent(initial) ? { remove: true } : null;
  }
  const changed = CLAIM_EXAMINER_FIELDS.some((f) => !sameText(value[f], initial[f]));
  if (!changed || !hasContent(value)) {
    return null;
  }
  return {
    remove: false,
    name: text(value.name),
    email: text(value.email),
    phoneNumber: text(value.phoneNumber),
    fax: text(value.fax),
    suite: text(value.suite),
    street: text(value.street),
    city: text(value.city),
    stateId: value.stateId ?? null,
    zip: text(value.zip),
  };
}

export interface StaffPartiesInput {
  raw: FormRecord;
  initial: FormRecord;
  claimExaminer: ClaimExaminerFormValue;
  initialClaimExaminer: ClaimExaminerFormValue;
  removed: { applicantAttorney: boolean; defenseAttorney: boolean; claimExaminer: boolean };
  applicantAttorneyId?: string | null;
  defenseAttorneyId?: string | null;
}

/** The whole body; `hasChanges` is false when there is nothing to send. */
export function buildStaffPartiesPayload(input: StaffPartiesInput): {
  payload: AppointmentPartiesUpdate;
  hasChanges: boolean;
} {
  const patientEmail = sameEmail(input.raw['patientEmail'], input.initial['patientEmail'])
    ? null
    : (text(input.raw['patientEmail']) ?? null);
  const payload: AppointmentPartiesUpdate = {
    patientEmail,
    applicantAttorney: attorneyChange(
      input.raw,
      input.initial,
      'applicantAttorney',
      input.removed.applicantAttorney,
      input.applicantAttorneyId,
    ),
    defenseAttorney: attorneyChange(
      input.raw,
      input.initial,
      'defenseAttorney',
      input.removed.defenseAttorney,
      input.defenseAttorneyId,
    ),
    claimExaminer: claimExaminerChange(
      input.claimExaminer,
      input.initialClaimExaminer,
      input.removed.claimExaminer,
    ),
  };
  const hasChanges =
    payload.patientEmail !== null ||
    payload.applicantAttorney !== null ||
    payload.defenseAttorney !== null ||
    payload.claimExaminer !== null;
  return { payload, hasChanges };
}

/**
 * A plain-English problem to show before anything is saved, or null. Every
 * party being added or changed needs an email: it is what lets them open the
 * appointment. Checked up front so a save never stops half-way through.
 */
export function partiesPayloadProblem(payload: AppointmentPartiesUpdate): string | null {
  const kept = (change: { remove: boolean } | null | undefined) => !!change && !change.remove;
  const parties: Array<[string, { email?: string } | null | undefined]> = [
    ['applicant attorney', payload.applicantAttorney],
    ['defense attorney', payload.defenseAttorney],
    ['claim examiner', payload.claimExaminer],
  ];
  const missing = parties
    .filter(([, change]) => kept(change as { remove: boolean }) && !change?.email)
    .map(([label]) => label);
  if (missing.length > 0) {
    return `Enter an email address for the ${missing.join(' and ')}. It is what lets them open this appointment.`;
  }
  const attorneys = [payload.applicantAttorney, payload.defenseAttorney];
  if (attorneys.some((a) => kept(a) && !a?.firmName)) {
    return 'Enter a firm name for each attorney you add or change.';
  }
  return null;
}
