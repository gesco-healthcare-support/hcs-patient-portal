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
 *
 * `sendBackFlaggable` separates the two concerns: every field is configurable in
 * the Field Configuration panel, but only requester-provided fields can be sent
 * back for the EXTERNAL user to fix. Panel number and appointment date/time are
 * staff/scheduling-controlled (changing the date is a reschedule, not a fix), so
 * they stay configurable but never appear in the send-back modal.
 */
export interface FlaggableField {
  key: string;
  label: string;
  group: string;
  /** Selectable in the staff send-back modal (false = Field-Config-only). */
  sendBackFlaggable: boolean;
}

export const FLAGGABLE_FIELDS: FlaggableField[] = [
  { key: 'panelNumber', label: 'Panel number', group: 'Schedule', sendBackFlaggable: false },
  {
    key: 'appointmentDate',
    label: 'Appointment date',
    group: 'Schedule',
    sendBackFlaggable: false,
  },
  { key: 'dateOfBirth', label: 'Date of birth', group: 'Patient', sendBackFlaggable: true },
  {
    key: 'socialSecurityNumber',
    label: 'Social Security #',
    group: 'Patient',
    sendBackFlaggable: true,
  },
  { key: 'address', label: 'Address', group: 'Patient', sendBackFlaggable: true },
  { key: 'cellPhoneNumber', label: 'Cell phone', group: 'Patient', sendBackFlaggable: true },
  {
    key: 'appointmentLanguageId',
    label: 'Language',
    group: 'Patient',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyEmail',
    label: 'Applicant attorney email',
    group: 'Attorneys',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyFirmName',
    label: 'Defense attorney firm',
    group: 'Attorneys',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceName',
    label: 'Insurance company',
    group: 'Insurance',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerEmail',
    label: 'Claim examiner email',
    group: 'Examiner',
    sendBackFlaggable: true,
  },
  { key: 'documents', label: 'Documents', group: 'Documents', sendBackFlaggable: true },
];
