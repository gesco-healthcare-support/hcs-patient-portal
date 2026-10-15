import { FormGroup } from '@angular/forms';
import { WIZARD_STEP_CONTROLS } from '../wizard/step-errors.util';

/**
 * Item 5 (2026-08-18) -- the sections the prefill picker offers.
 *
 * When a booking is prefilled from an older appointment, every field arrives looking correct
 * whether or not it still is. A defense attorney who changed eight months ago passes straight
 * through unless the booker happens to notice. The picker turns that silent default into a
 * deliberate answer: the booker says which sections have changed, and those are cleared.
 *
 * SECTIONS ARE NOT STEPS. `WIZARD_STEP_CONTROLS` partitions the form by wizard STEP, and
 * Employer is rendered inside the Patient step rather than having one of its own. The picker
 * offers Employer separately because that is how a booker thinks about it -- so the two
 * partitions differ, and Patient here is the step's controls MINUS the employer ones. Deriving
 * Patient straight from the step map would let both sections own the same controls, and
 * clearing either would corrupt the other.
 *
 * Claim / Injuries is deliberately absent: an appointment can carry several injuries where only
 * one changed, and a section-level all-or-nothing would force re-entering the rest. Injuries
 * always copy and are edited or deleted individually in the existing table.
 */
export type PrefillSection =
  | 'patient'
  | 'applicantAttorney'
  | 'defenseAttorney'
  | 'employer'
  | 'insurance'
  | 'examiner';

/** The seven employer controls. `employerStateId` carries no validator, so it is absent from
 * `WIZARD_STEP_CONTROLS` -- taking the list from there alone would strand it. */
const EMPLOYER_CONTROLS = [
  'employerName',
  'employerOccupation',
  'employerPhoneNumber',
  'employerStreet',
  'employerCity',
  'employerStateId',
  'employerZipCode',
] as const;

/** Ordered for display; the order matches the wizard so the picker reads top-to-bottom. */
export const PREFILL_SECTIONS: readonly { key: PrefillSection; label: string }[] = [
  { key: 'patient', label: 'Patient details' },
  { key: 'employer', label: 'Employer' },
  { key: 'applicantAttorney', label: 'Applicant attorney' },
  { key: 'defenseAttorney', label: 'Defense attorney' },
  { key: 'insurance', label: 'Insurance' },
  { key: 'examiner', label: 'Claim examiner' },
];

/**
 * Controls each section owns, used to clear a section the booker marks as changed.
 *
 * The attorney lists deliberately EXCLUDE `applicantAttorneyEnabled` / `defenseAttorneyEnabled`.
 * Clearing an attorney's details must not also retract the claim that an attorney exists --
 * blank and absent are different statements, and the section stays enabled but empty.
 */
export const SECTION_CONTROLS: Readonly<Record<PrefillSection, readonly string[]>> = {
  patient: WIZARD_STEP_CONTROLS['patient'].filter(
    (control) => !(EMPLOYER_CONTROLS as readonly string[]).includes(control),
  ),
  employer: EMPLOYER_CONTROLS,
  applicantAttorney: WIZARD_STEP_CONTROLS['applicant'],
  defenseAttorney: WIZARD_STEP_CONTROLS['defense'],
  insurance: WIZARD_STEP_CONTROLS['insurance'],
  examiner: WIZARD_STEP_CONTROLS['examiner'],
};

/** True means "this section has changed since the source appointment", so it gets cleared. */
export type PrefillSelection = Record<PrefillSection, boolean>;

/**
 * Empty every control a section owns, because the booker said it has changed since the source
 * appointment.
 *
 * Clears with `emitEvent: false` and resets pristine/untouched, for two reasons: the cascade
 * subscribers on the booking form react to value changes (and none of these controls should
 * trigger a slot or field-config reload just for being emptied), and a section the booker has
 * only just been asked about should not open already painted red with required-field errors.
 * The validators stay attached -- the section is still required, it is simply blank until they
 * fill it in.
 *
 * Deliberately never touches `applicantAttorneyEnabled` / `defenseAttorneyEnabled`; see the
 * note on SECTION_CONTROLS.
 */
export function clearPrefillSection(form: FormGroup, section: PrefillSection): void {
  for (const controlName of SECTION_CONTROLS[section]) {
    const control = form.get(controlName);
    if (!control) {
      continue;
    }
    control.setValue(null, { emitEvent: false });
    control.markAsPristine();
    control.markAsUntouched();
  }
}

/** Every section unchanged -- the fast path for the common case. */
export function defaultPrefillSelection(): PrefillSelection {
  return {
    patient: false,
    employer: false,
    applicantAttorney: false,
    defenseAttorney: false,
    insurance: false,
    examiner: false,
  };
}
