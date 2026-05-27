import { FormBuilder, FormGroup } from '@angular/forms';
import {
  ATTORNEY_SECTION_SUFFIXES,
  applyAttorneySectionValidators,
  wireAttorneySectionToggle,
} from './attorney-section-validators';

/**
 * BUG-012 Sub-bug 1 (2026-05-22) -- the FIRST Karma spec in this repo.
 * The shared `attorney-section-validators.ts` helper was extracted in
 * Sub-bug 2 to dedupe the AA/DA-section conditional Validators.required
 * wiring previously inlined in both `appointment-add` and
 * `appointment-view`. These tests cover the pure-function helpers
 * directly via a `FormBuilder` instance -- no TestBed boot needed.
 */
describe('attorney-section-validators', () => {
  let fb: FormBuilder;
  let form: FormGroup;

  /**
   * Build a fresh form with all 9 suffix-fields + the Enabled toggle
   * for both prefixes. Mirrors the per-prefix shape that the production
   * components declare.
   */
  function buildForm(): FormGroup {
    const controls: { [key: string]: unknown[] } = {
      applicantAttorneyEnabled: [true],
      defenseAttorneyEnabled: [true],
    };
    for (const prefix of ['applicantAttorney', 'defenseAttorney']) {
      for (const { name } of ATTORNEY_SECTION_SUFFIXES) {
        // Use [null] so .hasError('required') fires on empty value when
        // required is wired.
        controls[prefix + name] = [null];
      }
    }
    return fb.group(controls);
  }

  beforeEach(() => {
    fb = new FormBuilder();
    form = buildForm();
  });

  describe('ATTORNEY_SECTION_SUFFIXES', () => {
    it('declares 9 entries -- OLD Mandatory Fields modal + split Last Name (BUG-042)', () => {
      expect(ATTORNEY_SECTION_SUFFIXES.length).toBe(9);
    });

    it('includes FirmName with maxLength 50', () => {
      const firm = ATTORNEY_SECTION_SUFFIXES.find((s) => s.name === 'FirmName');
      expect(firm).toBeDefined();
      expect(firm!.maxLength).toBe(50);
    });

    it('includes split FirstName + LastName, each maxLength 50 (BUG-042)', () => {
      const first = ATTORNEY_SECTION_SUFFIXES.find((s) => s.name === 'FirstName');
      const last = ATTORNEY_SECTION_SUFFIXES.find((s) => s.name === 'LastName');
      expect(first).toBeDefined();
      expect(first!.maxLength).toBe(50);
      expect(last).toBeDefined();
      expect(last!.maxLength).toBe(50);
    });

    it('declares StateId with maxLength 0 (select, no length check)', () => {
      const state = ATTORNEY_SECTION_SUFFIXES.find((s) => s.name === 'StateId');
      expect(state).toBeDefined();
      expect(state!.maxLength).toBe(0);
    });
  });

  describe('applyAttorneySectionValidators', () => {
    it('adds Validators.required to all suffix-fields when required=true', () => {
      applyAttorneySectionValidators(form, 'applicantAttorney', true);

      for (const { name } of ATTORNEY_SECTION_SUFFIXES) {
        const control = form.get('applicantAttorney' + name);
        expect(control!.hasError('required'))
          .withContext(`applicantAttorney${name} should have required error on null`)
          .toBe(true);
      }
    });

    it('clears Validators.required from all suffix-fields when required=false', () => {
      // First apply, then revoke.
      applyAttorneySectionValidators(form, 'applicantAttorney', true);
      applyAttorneySectionValidators(form, 'applicantAttorney', false);

      for (const { name } of ATTORNEY_SECTION_SUFFIXES) {
        const control = form.get('applicantAttorney' + name);
        expect(control!.hasError('required'))
          .withContext(`applicantAttorney${name} required should be cleared`)
          .toBe(false);
      }
    });

    it('preserves Validators.maxLength on fields with maxLength > 0', () => {
      applyAttorneySectionValidators(form, 'applicantAttorney', false);

      // FirmName has maxLength 50 -- a 51-char string should fail.
      const firmControl = form.get('applicantAttorneyFirmName');
      firmControl!.setValue('x'.repeat(51));
      expect(firmControl!.hasError('maxlength')).toBe(true);
    });

    it('does not apply maxLength to StateId (maxLength=0 is the "select" sentinel)', () => {
      applyAttorneySectionValidators(form, 'applicantAttorney', true);

      const stateControl = form.get('applicantAttorneyStateId');
      stateControl!.setValue('any-very-long-state-id-value');
      // maxLength validator NOT present; required IS present (empty -> required error).
      expect(stateControl!.hasError('maxlength')).toBe(false);
    });

    it('isolates AA vs DA -- updating one prefix does not touch the other', () => {
      applyAttorneySectionValidators(form, 'applicantAttorney', true);
      applyAttorneySectionValidators(form, 'defenseAttorney', false);

      expect(form.get('applicantAttorneyFirmName')!.hasError('required')).toBe(true);
      expect(form.get('defenseAttorneyFirmName')!.hasError('required')).toBe(false);
    });

    it('is a no-op when a suffix field is missing from the form', () => {
      const sparseForm = fb.group({
        applicantAttorneyEnabled: [true],
        applicantAttorneyFirmName: [null],
        // Other suffix fields intentionally absent.
      });

      expect(() =>
        applyAttorneySectionValidators(sparseForm, 'applicantAttorney', true),
      ).not.toThrow();

      // The one declared control DID get required.
      expect(sparseForm.get('applicantAttorneyFirmName')!.hasError('required')).toBe(true);
    });
  });

  describe('wireAttorneySectionToggle', () => {
    it('applies initial validators based on the current Enabled value', () => {
      form.get('applicantAttorneyEnabled')!.setValue(true, { emitEvent: false });

      wireAttorneySectionToggle(form, 'applicantAttorney');

      expect(form.get('applicantAttorneyFirmName')!.hasError('required')).toBe(true);
    });

    it('re-applies validators when Enabled.valueChanges emits', () => {
      wireAttorneySectionToggle(form, 'applicantAttorney');
      // Initial state: required (Enabled=true).
      expect(form.get('applicantAttorneyFirmName')!.hasError('required')).toBe(true);

      // Toggle off -> validators cleared.
      form.get('applicantAttorneyEnabled')!.setValue(false);

      expect(form.get('applicantAttorneyFirmName')!.hasError('required')).toBe(false);
    });

    it('fires the optional onEnabledChange callback after re-applying validators', () => {
      const seen: boolean[] = [];

      wireAttorneySectionToggle(form, 'applicantAttorney', (enabled) => seen.push(enabled));

      form.get('applicantAttorneyEnabled')!.setValue(false);
      form.get('applicantAttorneyEnabled')!.setValue(true);

      // Two transitions; initial-apply does NOT fire the callback.
      expect(seen).toEqual([false, true]);
    });

    it('handles a missing Enabled control gracefully', () => {
      const sparseForm = fb.group({ applicantAttorneyFirmName: [null] });

      expect(() => wireAttorneySectionToggle(sparseForm, 'applicantAttorney')).not.toThrow();
    });
  });
});
