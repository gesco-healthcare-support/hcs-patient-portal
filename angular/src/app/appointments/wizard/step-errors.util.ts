import { FormGroup, ValidationErrors } from '@angular/forms';

/**
 * Validation metadata + a pure error collector for the appointment wizard.
 *
 * The wizard blocks Continue when the current step has invalid controls, but
 * historically only reddened the stepper dot -- the booker was never told which
 * field was wrong or why. These helpers turn a step's invalid controls into a
 * labelled, human-readable list the shell renders as an error summary. Kept as
 * standalone functions + data (not component members) so they unit-test without
 * a TestBed, mirroring wizard-copy.util.ts.
 */

/**
 * Controls validated when leaving each wizard step (the Continue gate). Moved
 * out of the component so the label map below can be coverage-checked against it
 * and so the collector has a single source of truth for each step's fields.
 * Disabled or not-required controls pass automatically; the booking engine has
 * already applied the conditional validators (patient email, panel number,
 * AA/DA-when-enabled, required claim examiner + insurance name).
 */
export const WIZARD_STEP_CONTROLS: Readonly<Record<string, readonly string[]>> = {
  schedule: [
    'appointmentTypeId',
    'locationId',
    'appointmentDate',
    'appointmentTime',
    'panelNumber',
  ],
  patient: [
    'firstName',
    'lastName',
    'middleName',
    'email',
    'dateOfBirth',
    'cellPhoneNumber',
    'phoneNumber',
    'socialSecurityNumber',
    'street',
    'address',
    'city',
    'zipCode',
    'interpreterVendorName',
    'refferedBy',
    'employerName',
    'employerOccupation',
    'employerPhoneNumber',
    'employerStreet',
    'employerCity',
    'employerZipCode',
  ],
  applicant: [
    'applicantAttorneyFirstName',
    'applicantAttorneyLastName',
    'applicantAttorneyEmail',
    'applicantAttorneyFirmName',
    'applicantAttorneyWebAddress',
    'applicantAttorneyPhoneNumber',
    'applicantAttorneyFaxNumber',
    'applicantAttorneyStreet',
    'applicantAttorneyCity',
    'applicantAttorneyStateId',
    'applicantAttorneyZipCode',
  ],
  defense: [
    'defenseAttorneyFirstName',
    'defenseAttorneyLastName',
    'defenseAttorneyEmail',
    'defenseAttorneyFirmName',
    'defenseAttorneyWebAddress',
    'defenseAttorneyPhoneNumber',
    'defenseAttorneyFaxNumber',
    'defenseAttorneyStreet',
    'defenseAttorneyCity',
    'defenseAttorneyStateId',
    'defenseAttorneyZipCode',
  ],
  insurance: [
    'appointmentInsuranceName',
    'appointmentInsuranceSuite',
    'appointmentInsurancePhoneNumber',
    'appointmentInsuranceFaxNumber',
    'appointmentInsuranceStreet',
    'appointmentInsuranceCity',
    'appointmentInsuranceStateId',
    'appointmentInsuranceZip',
  ],
  examiner: [
    'appointmentClaimExaminerName',
    'appointmentClaimExaminerEmail',
    'appointmentClaimExaminerSuite',
    'appointmentClaimExaminerPhoneNumber',
    'appointmentClaimExaminerFax',
    'appointmentClaimExaminerStreet',
    'appointmentClaimExaminerCity',
    'appointmentClaimExaminerStateId',
    'appointmentClaimExaminerZip',
  ],
};

/** Human labels for every control in {@link WIZARD_STEP_CONTROLS}. */
export const WIZARD_FIELD_LABELS: Readonly<Record<string, string>> = {
  appointmentTypeId: 'Appointment type',
  locationId: 'Location',
  appointmentDate: 'Appointment date',
  appointmentTime: 'Appointment time',
  panelNumber: 'Panel number',
  firstName: 'First name',
  lastName: 'Last name',
  middleName: 'Middle name',
  email: 'Email',
  dateOfBirth: 'Date of birth',
  cellPhoneNumber: 'Cell phone number',
  phoneNumber: 'Phone number',
  socialSecurityNumber: 'Social Security number',
  street: 'Street',
  address: 'Unit #',
  city: 'City',
  zipCode: 'ZIP code',
  interpreterVendorName: 'Interpreter vendor',
  refferedBy: 'Referred by',
  employerName: 'Employer name',
  employerOccupation: 'Employer occupation',
  employerPhoneNumber: 'Employer phone number',
  employerStreet: 'Employer street',
  employerCity: 'Employer city',
  employerZipCode: 'Employer ZIP code',
  applicantAttorneyFirstName: 'Applicant attorney first name',
  applicantAttorneyLastName: 'Applicant attorney last name',
  applicantAttorneyEmail: 'Applicant attorney email',
  applicantAttorneyFirmName: 'Applicant attorney firm name',
  applicantAttorneyWebAddress: 'Applicant attorney website',
  applicantAttorneyPhoneNumber: 'Applicant attorney phone number',
  applicantAttorneyFaxNumber: 'Applicant attorney fax number',
  applicantAttorneyStreet: 'Applicant attorney street',
  applicantAttorneyCity: 'Applicant attorney city',
  applicantAttorneyStateId: 'Applicant attorney state',
  applicantAttorneyZipCode: 'Applicant attorney ZIP code',
  defenseAttorneyFirstName: 'Defense attorney first name',
  defenseAttorneyLastName: 'Defense attorney last name',
  defenseAttorneyEmail: 'Defense attorney email',
  defenseAttorneyFirmName: 'Defense attorney firm name',
  defenseAttorneyWebAddress: 'Defense attorney website',
  defenseAttorneyPhoneNumber: 'Defense attorney phone number',
  defenseAttorneyFaxNumber: 'Defense attorney fax number',
  defenseAttorneyStreet: 'Defense attorney street',
  defenseAttorneyCity: 'Defense attorney city',
  defenseAttorneyStateId: 'Defense attorney state',
  defenseAttorneyZipCode: 'Defense attorney ZIP code',
  appointmentInsuranceName: 'Insurance company',
  appointmentInsuranceSuite: 'Insurance suite',
  appointmentInsurancePhoneNumber: 'Insurance phone number',
  appointmentInsuranceFaxNumber: 'Insurance fax number',
  appointmentInsuranceStreet: 'Insurance street',
  appointmentInsuranceCity: 'Insurance city',
  appointmentInsuranceStateId: 'Insurance state',
  appointmentInsuranceZip: 'Insurance ZIP code',
  appointmentClaimExaminerName: 'Claim examiner name',
  appointmentClaimExaminerEmail: 'Claim examiner email',
  appointmentClaimExaminerSuite: 'Claim examiner suite',
  appointmentClaimExaminerPhoneNumber: 'Claim examiner phone number',
  appointmentClaimExaminerFax: 'Claim examiner fax number',
  appointmentClaimExaminerStreet: 'Claim examiner street',
  appointmentClaimExaminerCity: 'Claim examiner city',
  appointmentClaimExaminerStateId: 'Claim examiner state',
  appointmentClaimExaminerZip: 'Claim examiner ZIP code',
};

/** One invalid field on the current step, ready to render in the summary. */
export interface StepErrorField {
  /** The reactive-form control name. */
  readonly control: string;
  /** Human label shown to the booker. */
  readonly label: string;
  /** Short reason + how to fix (e.g. "Required", "Enter a valid email"). */
  readonly message: string;
}

/**
 * Maps a control's first validation error to a short, plain-language reason so
 * the booker knows how to fix it. Falls back to a generic prompt for any
 * validator not enumerated here.
 */
export function describeControlError(errors: ValidationErrors | null): string {
  if (!errors) {
    return '';
  }
  if (errors['required']) {
    return 'Required';
  }
  if (errors['email']) {
    return 'Enter a valid email address';
  }
  // ngbDatepicker reports a malformed / out-of-range typed date via ngbDate;
  // the shared US-date parser is the source (see UsDateParserFormatter).
  if (errors['ngbDate']) {
    return 'Enter a valid date (MM/DD/YYYY)';
  }
  if (errors['pattern']) {
    return 'Invalid format';
  }
  if (errors['maxlength']) {
    return 'Too long';
  }
  if (errors['minlength']) {
    return 'Too short';
  }
  if (errors['min'] || errors['max']) {
    return 'Out of the allowed range';
  }
  return 'Please review this field';
}

/**
 * Collects the enabled, invalid controls named in <paramref>controlNames</param>
 * into a labelled, ordered list for the step error summary. Order follows
 * <paramref>controlNames</param>; disabled controls and controls absent from the
 * form are skipped. Labels come from <paramref>labelMap</param>, falling back to
 * the raw control name so a missing label never hides an error.
 */
export function collectStepErrors(
  form: FormGroup,
  controlNames: readonly string[],
  labelMap: Readonly<Record<string, string>>,
): StepErrorField[] {
  const errors: StepErrorField[] = [];
  for (const control of controlNames) {
    const field = form.get(control);
    if (field && field.enabled && field.invalid) {
      errors.push({
        control,
        label: labelMap[control] ?? control,
        message: describeControlError(field.errors),
      });
    }
  }
  return errors;
}
