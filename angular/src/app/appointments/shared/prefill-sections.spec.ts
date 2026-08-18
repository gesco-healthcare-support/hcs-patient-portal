import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { WIZARD_STEP_CONTROLS } from '../wizard/step-errors.util';
import {
  PREFILL_SECTIONS,
  SECTION_CONTROLS,
  clearPrefillSection,
  defaultPrefillSelection,
  type PrefillSection,
} from './prefill-sections';

/**
 * Item 5 T1 (2026-08-18) -- the six sections the prefill picker offers.
 *
 * The load-bearing test here is the Patient/Employer partition. `WIZARD_STEP_CONTROLS.patient`
 * contains the six `employer*` controls, because Employer is not its own wizard STEP -- it is
 * rendered inside the Patient step. But the picker treats Employer as its own SECTION, so a
 * naive `patient: WIZARD_STEP_CONTROLS.patient` would let both sections own the same controls:
 * marking Patient changed would silently wipe the employer, and marking Employer changed would
 * be undone by re-applying Patient. Steps and sections are different partitions of the form.
 */
describe('prefill-sections', () => {
  it('offers six sections and defaults every one to unchanged', () => {
    expect(PREFILL_SECTIONS.length).toBe(6);
    const selection = defaultPrefillSelection();
    for (const section of PREFILL_SECTIONS) {
      expect(selection[section.key]).toBe(false);
    }
  });

  it('does not offer Claim, Schedule, Docs or Review', () => {
    // Injuries always copy and are edited individually -- an appointment can carry several
    // where only one changed, so section-level all-or-nothing would force re-entering all.
    const keys = PREFILL_SECTIONS.map((s) => s.key as string);
    expect(keys).not.toContain('claim');
    expect(keys).not.toContain('schedule');
    expect(keys).not.toContain('docs');
    expect(keys).not.toContain('review');
  });

  it('gives every section a non-empty control list', () => {
    for (const section of PREFILL_SECTIONS) {
      expect(SECTION_CONTROLS[section.key].length).toBeGreaterThan(0);
    }
  });

  it('partitions Patient and Employer -- no control belongs to both', () => {
    const patient = SECTION_CONTROLS.patient;
    const employer = SECTION_CONTROLS.employer;

    expect(employer.length).toBe(7);
    for (const control of employer) {
      expect(control.startsWith('employer')).toBe(true);
      expect(patient).not.toContain(control);
    }
    // employerStateId is a real control that WIZARD_STEP_CONTROLS omits (it has no validator),
    // so deriving the employer list from the step map alone would silently leave it behind.
    expect(employer).toContain('employerStateId');
  });

  it('never lets two sections claim the same control', () => {
    const seen = new Set<string>();
    for (const section of PREFILL_SECTIONS) {
      for (const control of SECTION_CONTROLS[section.key]) {
        expect(seen.has(control)).toBe(false);
        seen.add(control);
      }
    }
  });

  it('keeps the attorney enabled flags OUT of the attorney sections', () => {
    // Clearing an attorney section must leave "is there an attorney?" answered. Blank and
    // absent are different claims -- an empty section still asserts one exists.
    expect(SECTION_CONTROLS.applicantAttorney).not.toContain('applicantAttorneyEnabled');
    expect(SECTION_CONTROLS.defenseAttorney).not.toContain('defenseAttorneyEnabled');
  });

  it('covers the attorney, insurance and examiner controls the step map validates', () => {
    // These four sections ARE whole steps, so the step map is the right source and the two
    // should not drift apart.
    const pairs: [PrefillSection, string][] = [
      ['applicantAttorney', 'applicant'],
      ['defenseAttorney', 'defense'],
      ['insurance', 'insurance'],
      ['examiner', 'examiner'],
    ];
    for (const [section, step] of pairs) {
      for (const control of WIZARD_STEP_CONTROLS[step]) {
        expect(SECTION_CONTROLS[section]).toContain(control);
      }
    }
  });

  describe('clearPrefillSection', () => {
    let fb: FormBuilder;
    let form: FormGroup;

    beforeEach(() => {
      fb = new FormBuilder();
      form = fb.group({
        firstName: ['Prefilled'],
        lastName: ['Prefilled'],
        employerName: ['Old Employer Inc'],
        employerStateId: ['some-guid'],
        applicantAttorneyFirstName: ['Old'],
        applicantAttorneyEmail: ['old@example.test', [Validators.email]],
        applicantAttorneyEnabled: [true],
        defenseAttorneyEnabled: [true],
        appointmentTypeId: ['type-guid'],
      });
    });

    it('clears the controls the section owns', () => {
      clearPrefillSection(form, 'employer');

      expect(form.get('employerName')!.value).toBeNull();
      expect(form.get('employerStateId')!.value).toBeNull();
    });

    it('leaves other sections and the schedule untouched', () => {
      clearPrefillSection(form, 'employer');

      expect(form.get('firstName')!.value).toBe('Prefilled');
      expect(form.get('appointmentTypeId')!.value).toBe('type-guid');
    });

    it('clears an attorney WITHOUT retracting that the attorney exists', () => {
      // The whole point of decision 7: an empty applicant-attorney section still asserts the
      // applicant IS represented. Flipping the enabled flag here would record the opposite --
      // that the appointment has no applicant attorney -- which is a different claim entirely.
      clearPrefillSection(form, 'applicantAttorney');

      expect(form.get('applicantAttorneyFirstName')!.value).toBeNull();
      expect(form.get('applicantAttorneyEnabled')!.value).toBe(true);
      expect(form.get('defenseAttorneyEnabled')!.value).toBe(true);
    });

    it('resets validation state so a cleared section does not open covered in errors', () => {
      form.get('applicantAttorneyFirstName')!.markAsDirty();
      form.get('applicantAttorneyFirstName')!.markAsTouched();

      clearPrefillSection(form, 'applicantAttorney');

      expect(form.get('applicantAttorneyFirstName')!.dirty).toBe(false);
      expect(form.get('applicantAttorneyFirstName')!.touched).toBe(false);
    });

    it('ignores controls the form does not have', () => {
      const sparse = new FormBuilder().group({ firstName: ['x'] });
      expect(() => clearPrefillSection(sparse, 'insurance')).not.toThrow();
    });
  });
});
