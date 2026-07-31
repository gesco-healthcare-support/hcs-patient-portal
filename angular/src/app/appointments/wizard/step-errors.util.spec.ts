import { FormBuilder, FormGroup, Validators } from '@angular/forms';

import {
  WIZARD_FIELD_LABELS,
  WIZARD_STEP_CONTROLS,
  collectStepErrors,
  describeControlError,
} from './step-errors.util';

describe('step-errors.util', () => {
  const fb = new FormBuilder();

  function build(): FormGroup {
    return fb.group({
      firstName: [null as string | null, [Validators.required]],
      lastName: [null as string | null, [Validators.required]],
      email: [null as string | null, [Validators.required, Validators.email]],
      middleName: [null as string | null],
    });
  }

  const labels = {
    firstName: 'First name',
    lastName: 'Last name',
    email: 'Email',
    middleName: 'Middle name',
  };

  describe('collectStepErrors', () => {
    it('returns an empty list when every named control is valid', () => {
      const form = build();
      form.patchValue({ firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com' });

      expect(collectStepErrors(form, ['firstName', 'lastName', 'email'], labels)).toEqual([]);
    });

    it('reports each invalid control mapped to its human label', () => {
      const form = build();

      const result = collectStepErrors(form, ['firstName', 'lastName'], labels);

      expect(result.map((e) => e.control)).toEqual(['firstName', 'lastName']);
      expect(result.map((e) => e.label)).toEqual(['First name', 'Last name']);
    });

    it('preserves the order of controlNames, not the form order', () => {
      const form = build();

      const result = collectStepErrors(form, ['email', 'firstName'], labels);

      expect(result.map((e) => e.control)).toEqual(['email', 'firstName']);
    });

    it('skips disabled controls even when invalid', () => {
      const form = build();
      form.get('firstName')?.disable();

      const result = collectStepErrors(form, ['firstName', 'lastName'], labels);

      expect(result.map((e) => e.control)).toEqual(['lastName']);
    });

    it('skips control names absent from the form', () => {
      const form = build();

      const result = collectStepErrors(form, ['firstName', 'notAControl'], labels);

      expect(result.map((e) => e.control)).toEqual(['firstName']);
    });

    it('falls back to the raw control name when a label is missing', () => {
      const form = build();

      const result = collectStepErrors(form, ['firstName'], {});

      expect(result[0].label).toBe('firstName');
    });

    it('does not require the control to be touched', () => {
      const form = build();
      // pristine + untouched, but invalid (required + empty)
      expect(collectStepErrors(form, ['firstName'], labels).length).toBe(1);
    });
  });

  describe('describeControlError', () => {
    it('returns empty string for no errors', () => {
      expect(describeControlError(null)).toBe('');
    });

    it('describes a required error', () => {
      expect(describeControlError({ required: true }).toLowerCase()).toContain('required');
    });

    it('describes an email error', () => {
      expect(describeControlError({ email: true }).toLowerCase()).toContain('email');
    });

    it('describes a date error', () => {
      const msg = describeControlError({ ngbDate: { invalid: true } }).toLowerCase();
      expect(msg).toContain('date');
    });

    it('describes a maxlength error as too long', () => {
      const msg = describeControlError({
        maxlength: { requiredLength: 50, actualLength: 80 },
      }).toLowerCase();
      expect(msg).toContain('long');
    });

    it('falls back to a generic reason for an unknown validator', () => {
      expect(describeControlError({ someCustomRule: true })).toBeTruthy();
    });
  });

  describe('field metadata', () => {
    it('has a label for every control named in WIZARD_STEP_CONTROLS', () => {
      const missing: string[] = [];
      for (const names of Object.values(WIZARD_STEP_CONTROLS)) {
        for (const name of names) {
          if (!WIZARD_FIELD_LABELS[name]) {
            missing.push(name);
          }
        }
      }
      expect(missing).toEqual([]);
    });

    it('collects an invalid control with a message derived from its errors', () => {
      const form = fb.group({
        email: [null as string | null, [Validators.required, Validators.email]],
      });
      form.patchValue({ email: 'not-an-email' });

      const [entry] = collectStepErrors(form, ['email'], WIZARD_FIELD_LABELS);

      expect(entry.label).toBe('Email');
      expect(entry.message.toLowerCase()).toContain('email');
    });
  });
});
