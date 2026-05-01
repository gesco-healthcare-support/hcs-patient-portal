/**
 * Single source of truth for the office's send-back-for-info modal field list.
 *
 * Field names mirror OLD's appointment form (camelCase). Renaming a key here
 * propagates to (a) the send-back modal checkbox list, (b) the booker's
 * AwaitingMoreInfo banner pill rendering, (c) the AppointmentSendBackInfo
 * row's FlaggedFieldsJson value. Backend stores the field key strings
 * verbatim -- no enum validation -- so renames are NEW-side cosmetic.
 *
 * Wave-by-wave editability:
 *   - W1: PATIENT_DEMOGRAPHICS, EMPLOYER_DETAIL, APPLICANT_ATTORNEY,
 *     AUTHORIZED_USERS, APPOINTMENT_DETAILS already render as editable
 *     fields on the booker's appointment-add / appointment-view pages.
 *   - W2 (`attorney-defense-patient-separation` cap [LANDED IN W2-7]):
 *     DEFENSE_ATTORNEY fields ship as editable on the booker form
 *     (appointment-add) parallel to APPLICANT_ATTORNEY.
 *   - W2 (`appointment-injury-workflow` cap [LANDED IN W2-8]):
 *     PATIENT_INJURY, INSURANCE_CARRIER, CLAIM_ADJUSTER fields ship as
 *     editable in the Claim Information modal on appointment-add. Multi-injury
 *     per appointment supported (table-of-injuries below the Claim Information
 *     header). Each injury row carries its own insurance + claim examiner
 *     sub-section with isActive toggle, mirroring OLD's UX.
 *
 * Until each cap lands, the corresponding section's checkboxes are still
 * visible in the send-back modal -- the office can pre-flag -- but the
 * booker won't see editable inputs for them on the resubmit screen.
 *
 * OLD-form citations: see `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.html`.
 */

export type FlaggableWaveTag = 'W1' | 'W2' | 'POST_MVP';

export interface FlaggableField {
  /** Unique key persisted to AppointmentSendBackInfo.FlaggedFieldsJson */
  key: string;
  /** Human-readable label rendered on the modal checkbox + booker pill */
  label: string;
  /** Wave the actual editable form field ships in */
  wave: FlaggableWaveTag;
}

export interface FlaggableSection {
  /** Section identifier; used in pill rendering on the booker banner */
  id: string;
  /** Section label rendered as the modal panel header */
  label: string;
  /** Wave when ALL fields in the section become editable on the booker form */
  wave: FlaggableWaveTag;
  /** Field list */
  fields: FlaggableField[];
}

export const FLAGGABLE_SECTIONS: readonly FlaggableSection[] = [
  // Section 1: Patient Demographics (W1 -- editable on appointment-add today)
  {
    id: 'patientDemographics',
    label: 'Patient Demographics',
    wave: 'W1',
    fields: [
      { key: 'firstName', label: 'First Name', wave: 'W1' },
      { key: 'lastName', label: 'Last Name', wave: 'W1' },
      { key: 'middleName', label: 'Middle Name', wave: 'W1' },
      { key: 'genderId', label: 'Gender', wave: 'W1' },
      { key: 'dateOfBirth', label: 'Date of Birth', wave: 'W1' },
      { key: 'email', label: 'Email', wave: 'W1' },
      { key: 'cellPhoneNumber', label: 'Cell Phone Number', wave: 'W1' },
      { key: 'phoneNumber', label: 'Phone Number', wave: 'W1' },
      { key: 'phoneNumberType', label: 'Phone Number Type', wave: 'W1' },
      { key: 'socialSecurityNumber', label: 'Social Security #', wave: 'W1' },
      { key: 'street', label: 'Street Address', wave: 'W1' },
      { key: 'apptNumber', label: 'Unit / Apartment', wave: 'W1' },
      { key: 'city', label: 'City', wave: 'W1' },
      { key: 'stateId', label: 'State', wave: 'W1' },
      { key: 'zipCode', label: 'Zip Code', wave: 'W1' },
      { key: 'languageId', label: 'Language', wave: 'W1' },
      { key: 'othersLanguageName', label: 'Other Language Name', wave: 'W1' },
      { key: 'isInterpreter', label: 'Interpreter Needed', wave: 'W1' },
      { key: 'interpreterVendorName', label: 'Interpreter Vendor', wave: 'W1' },
      { key: 'referredBy', label: 'Referred By', wave: 'W1' },
    ],
  },

  // Section 2: Employer Detail (W1 -- editable today)
  {
    id: 'employerDetail',
    label: 'Employer Detail',
    wave: 'W1',
    fields: [
      { key: 'employerName', label: 'Employer Name', wave: 'W1' },
      { key: 'occupation', label: 'Occupation', wave: 'W1' },
      { key: 'employerPhoneNumber', label: 'Employer Phone Number', wave: 'W1' },
      { key: 'employerStreet', label: 'Employer Street', wave: 'W1' },
      { key: 'employerCity', label: 'Employer City', wave: 'W1' },
      { key: 'employerStateId', label: 'Employer State', wave: 'W1' },
      { key: 'employerZip', label: 'Employer Zip', wave: 'W1' },
    ],
  },

  // Section 3: Applicant Attorney (W1 -- editable today)
  {
    id: 'applicantAttorney',
    label: 'Applicant Attorney',
    wave: 'W1',
    fields: [
      { key: 'applicantAttorneyIsActive', label: 'Active', wave: 'W1' },
      { key: 'applicantAttorneyName', label: 'Attorney Name', wave: 'W1' },
      { key: 'applicantAttorneyEmail', label: 'Email', wave: 'W1' },
      { key: 'applicantAttorneyFirmName', label: 'Firm Name', wave: 'W1' },
      { key: 'applicantAttorneyWebAddress', label: 'Web Address', wave: 'W1' },
      { key: 'applicantAttorneyPhoneNumber', label: 'Phone Number', wave: 'W1' },
      { key: 'applicantAttorneyFaxNumber', label: 'Fax Number', wave: 'W1' },
      { key: 'applicantAttorneyStreet', label: 'Street', wave: 'W1' },
      { key: 'applicantAttorneyCity', label: 'City', wave: 'W1' },
      { key: 'applicantAttorneyStateId', label: 'State', wave: 'W1' },
      { key: 'applicantAttorneyZip', label: 'Zip', wave: 'W1' },
    ],
  },

  // Section 4: Authorized Users (W1 -- editable today)
  {
    id: 'authorizedUsers',
    label: 'Authorized Users',
    wave: 'W1',
    fields: [
      { key: 'authorizedUserEmail', label: 'Email', wave: 'W1' },
      { key: 'authorizedUserFirstName', label: 'First Name', wave: 'W1' },
      { key: 'authorizedUserLastName', label: 'Last Name', wave: 'W1' },
      { key: 'authorizedUserRoleId', label: 'User Role', wave: 'W1' },
      { key: 'authorizedUserAccessTypeId', label: 'Access Rights Type', wave: 'W1' },
    ],
  },

  // Section 5: Appointment Details (W1 -- editable today)
  {
    id: 'appointmentDetails',
    label: 'Appointment Details',
    wave: 'W1',
    fields: [
      { key: 'appointmentTypeId', label: 'Appointment Type', wave: 'W1' },
      { key: 'requestConfirmationNumber', label: 'Confirmation Number', wave: 'W1' },
      { key: 'panelNumber', label: 'Panel Number', wave: 'W1' },
      { key: 'locationId', label: 'Location', wave: 'W1' },
      { key: 'primaryResponsibleUserId', label: 'Responsible User', wave: 'W1' },
      { key: 'availableDate', label: 'Appointment Date', wave: 'W1' },
      { key: 'doctorAvailabilityId', label: 'Appointment Time', wave: 'W1' },
    ],
  },

  // Section 6: Defense Attorney (W2 -- attorney-defense-patient-separation cap; deferred per ledger)
  {
    id: 'defenseAttorney',
    label: 'Defense Attorney',
    wave: 'W2',
    fields: [
      { key: 'defenseAttorneyIsActive', label: 'Active', wave: 'W2' },
      { key: 'defenseAttorneyName', label: 'Attorney Name', wave: 'W2' },
      { key: 'defenseAttorneyEmail', label: 'Email', wave: 'W2' },
      { key: 'defenseAttorneyFirmName', label: 'Firm Name', wave: 'W2' },
      { key: 'defenseAttorneyWebAddress', label: 'Web Address', wave: 'W2' },
      { key: 'defenseAttorneyPhoneNumber', label: 'Phone Number', wave: 'W2' },
      { key: 'defenseAttorneyFaxNumber', label: 'Fax Number', wave: 'W2' },
      { key: 'defenseAttorneyStreet', label: 'Street', wave: 'W2' },
      { key: 'defenseAttorneyCity', label: 'City', wave: 'W2' },
      { key: 'defenseAttorneyStateId', label: 'State', wave: 'W2' },
      { key: 'defenseAttorneyZip', label: 'Zip', wave: 'W2' },
    ],
  },

  // Section 7: Patient Injury / Claim Details (W2 -- appointment-injury-workflow cap)
  {
    id: 'patientInjury',
    label: 'Patient Injury / Claim Details',
    wave: 'W2',
    fields: [
      { key: 'isCumulativeInjury', label: 'Cumulative Trauma Injury', wave: 'W2' },
      { key: 'dateOfInjury', label: 'Date of Injury / From Date', wave: 'W2' },
      { key: 'toDateOfInjury', label: 'To Date of Injury', wave: 'W2' },
      { key: 'claimNumber', label: 'Claim Number', wave: 'W2' },
      { key: 'wcabOfficeId', label: 'WCAB Office / Venue', wave: 'W2' },
      { key: 'wcabAdj', label: 'WCAB ADJ #', wave: 'W2' },
      { key: 'bodyParts', label: 'Body Parts', wave: 'W2' },
    ],
  },

  // Section 8: Insurance Carrier (W2 -- appointment-injury-workflow cap covers AppointmentPrimaryInsurance)
  {
    id: 'insuranceCarrier',
    label: 'Insurance Carrier',
    wave: 'W2',
    fields: [
      { key: 'insuranceCarrierIsActive', label: 'Active', wave: 'W2' },
      { key: 'insuranceCarrierName', label: 'Company Name', wave: 'W2' },
      { key: 'insuranceCarrierAttention', label: 'Attention To', wave: 'W2' },
      { key: 'insuranceCarrierPhoneNumber', label: 'Phone Number', wave: 'W2' },
      { key: 'insuranceCarrierFaxNumber', label: 'Fax Number', wave: 'W2' },
      { key: 'insuranceCarrierStreet', label: 'Street', wave: 'W2' },
      { key: 'insuranceCarrierSuite', label: 'STE / Suite', wave: 'W2' },
      { key: 'insuranceCarrierCity', label: 'City', wave: 'W2' },
      { key: 'insuranceCarrierStateId', label: 'State', wave: 'W2' },
      { key: 'insuranceCarrierZip', label: 'Zip', wave: 'W2' },
    ],
  },

  // Section 9: Claim Adjuster / Claim Examiner (W2 -- appointment-injury-workflow cap covers AppointmentClaimExaminer)
  {
    id: 'claimAdjuster',
    label: 'Claim Adjuster',
    wave: 'W2',
    fields: [
      { key: 'claimAdjusterIsActive', label: 'Active', wave: 'W2' },
      { key: 'claimAdjusterName', label: 'Name', wave: 'W2' },
      { key: 'claimAdjusterEmail', label: 'Email', wave: 'W2' },
      { key: 'claimAdjusterPhoneNumber', label: 'Phone Number', wave: 'W2' },
      { key: 'claimAdjusterFax', label: 'Fax', wave: 'W2' },
      { key: 'claimAdjusterStreet', label: 'Street', wave: 'W2' },
      { key: 'claimAdjusterSuite', label: 'STE / Suite', wave: 'W2' },
      { key: 'claimAdjusterCity', label: 'City', wave: 'W2' },
      { key: 'claimAdjusterStateId', label: 'State', wave: 'W2' },
      { key: 'claimAdjusterZip', label: 'Zip', wave: 'W2' },
    ],
  },
];

/** Total flaggable field count -- 92 across 9 sections. Matches OLD's appointment form. */
export const TOTAL_FLAGGABLE_FIELD_COUNT: number = FLAGGABLE_SECTIONS.reduce(
  (sum, section) => sum + section.fields.length,
  0,
);

/**
 * Build a map of `key -> { sectionId, label }` for fast lookup when rendering
 * the booker's banner pills (we know flagged keys, need section + label).
 */
export function buildFlaggedFieldLookup(): Map<
  string,
  { sectionId: string; sectionLabel: string; fieldLabel: string; wave: FlaggableWaveTag }
> {
  const lookup = new Map<
    string,
    { sectionId: string; sectionLabel: string; fieldLabel: string; wave: FlaggableWaveTag }
  >();
  for (const section of FLAGGABLE_SECTIONS) {
    for (const field of section.fields) {
      lookup.set(field.key, {
        sectionId: section.id,
        sectionLabel: section.label,
        fieldLabel: field.label,
        wave: field.wave,
      });
    }
  }
  return lookup;
}
