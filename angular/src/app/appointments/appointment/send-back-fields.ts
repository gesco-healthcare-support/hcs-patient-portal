/**
 * One flaggable appointment field. `key` is the stable identifier shared by two
 * features so they can never drift apart:
 *  1. the staff "Request info" / Send Back modal (which fields a requester must
 *     review/fix and resubmit), and
 *  2. the per-appointment-type Field Configuration panel, which persists this
 *     `key` verbatim as the config row's FieldName.
 *
 * Most keys are live form-control names on the booking form's parent FormGroup
 * (appointment-add.component.ts), so a field's hidden / read-only / default-value
 * config takes effect on the real form. Two keys are section-level pseudo-fields
 * with no single control: `claimInformation` (the whole Claim Information section
 * is too dynamic to enumerate as discrete fields, so it is requested as a unit)
 * and `documents` (file uploads, stored-only).
 *
 * `sendBackFlaggable` separates the two concerns: every field is configurable in
 * the Field Configuration panel, but only requester-provided fields appear in the
 * staff send-back modal. The Schedule fields are staff/scheduling-controlled
 * (changing the date is a reschedule, not a fix), so they stay configurable but
 * never appear in the send-back modal.
 *
 * `group` is the wizard section name -- it MUST match the appointment wizard's
 * section headings (parity) so a non-technical user sees the same sections in the
 * send-back modal as in the form. The send-back modal renders these groups in
 * SEND_BACK_SECTION_ORDER as collapsible sections with a select-all-in-section
 * toggle (QA item L, 2026-06-30, per Adrian: 65 flaggable fields, Schedule and
 * Additional Accessor and Custom Fields excluded).
 */
export interface FlaggableField {
  key: string;
  label: string;
  group: string;
  /** Selectable in the staff send-back modal (false = Field-Config-only). */
  sendBackFlaggable: boolean;
}

/**
 * Wizard-parity section order for the send-back modal's collapsible groups. Only
 * groups that contain at least one `sendBackFlaggable` field are shown in the modal.
 */
export const SEND_BACK_SECTION_ORDER: string[] = [
  'Patient Demographics',
  'Employer Details',
  'Applicant Attorney',
  'Defense Attorney',
  'Insurance Carrier',
  'Claim Examiner',
  'Claim Information',
  'Documents',
];

export const FLAGGABLE_FIELDS: FlaggableField[] = [
  // --- Patient Demographics (17) ---
  { key: 'firstName', label: 'First name', group: 'Patient Demographics', sendBackFlaggable: true },
  {
    key: 'middleName',
    label: 'Middle name',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  { key: 'lastName', label: 'Last name', group: 'Patient Demographics', sendBackFlaggable: true },
  { key: 'genderId', label: 'Gender', group: 'Patient Demographics', sendBackFlaggable: true },
  {
    key: 'dateOfBirth',
    label: 'Date of birth',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  { key: 'email', label: 'Email', group: 'Patient Demographics', sendBackFlaggable: true },
  {
    key: 'cellPhoneNumber',
    label: 'Cell phone',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  { key: 'phoneNumber', label: 'Phone', group: 'Patient Demographics', sendBackFlaggable: true },
  {
    key: 'socialSecurityNumber',
    label: 'Social Security #',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  { key: 'street', label: 'Street', group: 'Patient Demographics', sendBackFlaggable: true },
  { key: 'city', label: 'City', group: 'Patient Demographics', sendBackFlaggable: true },
  { key: 'stateId', label: 'State', group: 'Patient Demographics', sendBackFlaggable: true },
  { key: 'zipCode', label: 'ZIP code', group: 'Patient Demographics', sendBackFlaggable: true },
  {
    key: 'appointmentLanguageId',
    label: 'Language',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  {
    key: 'needsInterpreter',
    label: 'Needs interpreter',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  {
    key: 'interpreterVendorName',
    label: 'Interpreter vendor',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },
  {
    key: 'refferedBy',
    label: 'Referred by',
    group: 'Patient Demographics',
    sendBackFlaggable: true,
  },

  // --- Employer Details (7) ---
  {
    key: 'employerName',
    label: 'Employer name',
    group: 'Employer Details',
    sendBackFlaggable: true,
  },
  {
    key: 'employerOccupation',
    label: 'Occupation',
    group: 'Employer Details',
    sendBackFlaggable: true,
  },
  {
    key: 'employerPhoneNumber',
    label: 'Phone',
    group: 'Employer Details',
    sendBackFlaggable: true,
  },
  { key: 'employerStreet', label: 'Street', group: 'Employer Details', sendBackFlaggable: true },
  { key: 'employerCity', label: 'City', group: 'Employer Details', sendBackFlaggable: true },
  { key: 'employerStateId', label: 'State', group: 'Employer Details', sendBackFlaggable: true },
  { key: 'employerZipCode', label: 'ZIP code', group: 'Employer Details', sendBackFlaggable: true },

  // --- Applicant Attorney (11) ---
  {
    key: 'applicantAttorneyFirstName',
    label: 'First name',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyLastName',
    label: 'Last name',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyEmail',
    label: 'Email',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyFirmName',
    label: 'Firm name',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyWebAddress',
    label: 'Website',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyPhoneNumber',
    label: 'Phone',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyFaxNumber',
    label: 'Fax',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyStreet',
    label: 'Street',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyCity',
    label: 'City',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyStateId',
    label: 'State',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'applicantAttorneyZipCode',
    label: 'ZIP code',
    group: 'Applicant Attorney',
    sendBackFlaggable: true,
  },

  // --- Defense Attorney (11) ---
  {
    key: 'defenseAttorneyFirstName',
    label: 'First name',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyLastName',
    label: 'Last name',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyEmail',
    label: 'Email',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyFirmName',
    label: 'Firm name',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyWebAddress',
    label: 'Website',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyPhoneNumber',
    label: 'Phone',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyFaxNumber',
    label: 'Fax',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyStreet',
    label: 'Street',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  { key: 'defenseAttorneyCity', label: 'City', group: 'Defense Attorney', sendBackFlaggable: true },
  {
    key: 'defenseAttorneyStateId',
    label: 'State',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },
  {
    key: 'defenseAttorneyZipCode',
    label: 'ZIP code',
    group: 'Defense Attorney',
    sendBackFlaggable: true,
  },

  // --- Insurance Carrier (8) ---
  {
    key: 'appointmentInsuranceName',
    label: 'Insurance company',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceStreet',
    label: 'Street',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceSuite',
    label: 'Suite',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsurancePhoneNumber',
    label: 'Phone',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceFaxNumber',
    label: 'Fax',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceCity',
    label: 'City',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceStateId',
    label: 'State',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentInsuranceZip',
    label: 'ZIP code',
    group: 'Insurance Carrier',
    sendBackFlaggable: true,
  },

  // --- Claim Examiner (9) ---
  {
    key: 'appointmentClaimExaminerName',
    label: 'Name',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerEmail',
    label: 'Email',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerStreet',
    label: 'Street',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerSuite',
    label: 'Suite',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerPhoneNumber',
    label: 'Phone',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerFax',
    label: 'Fax',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerCity',
    label: 'City',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerStateId',
    label: 'State',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },
  {
    key: 'appointmentClaimExaminerZip',
    label: 'ZIP code',
    group: 'Claim Examiner',
    sendBackFlaggable: true,
  },

  // --- Claim Information (1, consolidated section-level flag) ---
  {
    key: 'claimInformation',
    label: 'Claim information',
    group: 'Claim Information',
    sendBackFlaggable: true,
  },

  // --- Documents (1) ---
  { key: 'documents', label: 'Documents', group: 'Documents', sendBackFlaggable: true },

  // --- Schedule (Field-Config only; never in the send-back modal) ---
  {
    key: 'appointmentTypeId',
    label: 'Appointment type',
    group: 'Schedule',
    sendBackFlaggable: false,
  },
  { key: 'panelNumber', label: 'Panel number', group: 'Schedule', sendBackFlaggable: false },
  { key: 'locationId', label: 'Location', group: 'Schedule', sendBackFlaggable: false },
  {
    key: 'appointmentDate',
    label: 'Appointment date',
    group: 'Schedule',
    sendBackFlaggable: false,
  },
  {
    key: 'appointmentTime',
    label: 'Appointment time',
    group: 'Schedule',
    sendBackFlaggable: false,
  },
];

/** The send-back-flaggable fields grouped by wizard section, in wizard order.
 *  Drives the collapsible modal: each entry is one collapsible section with its
 *  selectable fields. Sections with no flaggable fields are omitted. */
export const SEND_BACK_GROUPS: { group: string; fields: FlaggableField[] }[] =
  SEND_BACK_SECTION_ORDER.map((group) => ({
    group,
    fields: FLAGGABLE_FIELDS.filter((f) => f.group === group && f.sendBackFlaggable),
  })).filter((g) => g.fields.length > 0);
