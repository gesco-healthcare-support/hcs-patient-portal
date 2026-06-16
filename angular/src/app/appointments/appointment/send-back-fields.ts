/**
 * One flaggable booking field. `key` is the stable identifier shared by two
 * features so they can never drift apart:
 *  1. the staff "Request info" / Send Back modal (which fields a requester must
 *     fix and resubmit), and
 *  2. the per-appointment-type Field Configuration panel (Prompt 15), which
 *     persists this `key` verbatim as the config row's FieldName.
 *
 * The backend does NOT validate FieldName against a catalog, so this registry IS
 * the source of truth. 11 of the 12 keys are live booking-form control names in
 * `appointment-add.component.ts`, so a field's hidden / read-only / default-value
 * config takes effect on the real booking form; `documents` has no booking-form
 * control and is stored-only.
 */
export interface FlaggableField {
  key: string;
  label: string;
  group: string;
}

export const FLAGGABLE_FIELDS: FlaggableField[] = [
  { key: 'panelNumber', label: 'Panel number', group: 'Schedule' },
  { key: 'appointmentDate', label: 'Appointment date', group: 'Schedule' },
  { key: 'dateOfBirth', label: 'Date of birth', group: 'Patient' },
  { key: 'socialSecurityNumber', label: 'Social Security #', group: 'Patient' },
  { key: 'address', label: 'Address', group: 'Patient' },
  { key: 'cellPhoneNumber', label: 'Cell phone', group: 'Patient' },
  { key: 'appointmentLanguageId', label: 'Language', group: 'Patient' },
  { key: 'applicantAttorneyEmail', label: 'Applicant attorney email', group: 'Attorneys' },
  { key: 'defenseAttorneyFirmName', label: 'Defense attorney firm', group: 'Attorneys' },
  { key: 'appointmentInsuranceName', label: 'Insurance company', group: 'Insurance' },
  { key: 'appointmentClaimExaminerEmail', label: 'Claim examiner email', group: 'Examiner' },
  { key: 'documents', label: 'Documents', group: 'Documents' },
];
