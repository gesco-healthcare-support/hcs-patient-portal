import { FormGroup, Validators } from '@angular/forms';

/**
 * BUG-012 follow-up (2026-05-22, Sub-bug 2 dedup) -- shared helper for
 * the Applicant Attorney / Defense Attorney section's conditional
 * `Validators.required` wiring. Previously inlined verbatim in BOTH
 * `appointment-add.component.ts` and `appointment-view.component.ts`;
 * extracted here so the suffixes list + the validator-application loop
 * live in one place, mirroring OLD's "Mandatory Fields" submit-modal
 * rationale once.
 */
export type AttorneySectionPrefix = 'applicantAttorney' | 'defenseAttorney';

/**
 * Field-suffix definitions for the AA/DA section. Each entry is the
 * control-name suffix appended to the section prefix + the existing
 * maxLength (preserved across required-toggling). Email is intentionally
 * NOT in this list -- email validators live on the initial form-control
 * declaration alongside `Validators.email` because the email check shape
 * differs (always carries .email validator; .required is conditional).
 */
export const ATTORNEY_SECTION_SUFFIXES: ReadonlyArray<{
  readonly name: string;
  readonly maxLength: number;
}> = [
  { name: 'FirstName', maxLength: 50 },
  { name: 'LastName', maxLength: 50 },
  { name: 'FirmName', maxLength: 50 },
  { name: 'PhoneNumber', maxLength: 20 },
  // 2026-06-01: Fax is optional. FaxNumber intentionally NOT in this list so
  // the section toggle never marks it required; its maxLength(19) lives on the
  // control declaration in appointment-add / appointment-view.
  { name: 'Street', maxLength: 255 },
  { name: 'City', maxLength: 50 },
  { name: 'StateId', maxLength: 0 },
  { name: 'ZipCode', maxLength: 10 },
];

/**
 * Conditionally applies `Validators.required` to the 9 AA/DA-section
 * fields keyed by the section's `prefix`. When `required` is true, each
 * field gets `[Validators.required, Validators.maxLength(N)]`. When
 * false, just `[Validators.maxLength(N)]` (or no validators if the field
 * is a select without a maxLength). Idempotent -- safe to call multiple
 * times. Bypasses event emission so the parent form's valueChanges
 * subscribers don't recurse.
 */
export function applyAttorneySectionValidators(
  form: FormGroup,
  prefix: AttorneySectionPrefix,
  required: boolean,
): void {
  for (const { name, maxLength } of ATTORNEY_SECTION_SUFFIXES) {
    const control = form.get(prefix + name);
    if (!control) continue;
    const validators = [];
    if (required) {
      validators.push(Validators.required);
    }
    if (maxLength > 0) {
      validators.push(Validators.maxLength(maxLength));
    }
    control.setValidators(validators);
    control.updateValueAndValidity({ emitEvent: false });
  }
}

/**
 * Wires the `Enabled`-toggle valueChanges subscription that re-runs
 * `applyAttorneySectionValidators` whenever the section's "Include"
 * checkbox flips. Also applies the initial pass at construction so the
 * required state is consistent before any user interaction.
 *
 * @param form    parent FormGroup carrying the `{prefix}Enabled` control + the 8 suffix fields.
 * @param prefix  'applicantAttorney' or 'defenseAttorney'.
 * @param onEnabledChange  optional hook for additional side-effects per toggle (e.g. clearing the email field). Called AFTER the validator update.
 */
export function wireAttorneySectionToggle(
  form: FormGroup,
  prefix: AttorneySectionPrefix,
  onEnabledChange?: (enabled: boolean) => void,
): void {
  const enabledControl = form.get(prefix + 'Enabled');
  if (!enabledControl) return;
  enabledControl.valueChanges.subscribe((enabled) => {
    const isEnabled = !!enabled;
    applyAttorneySectionValidators(form, prefix, isEnabled);
    onEnabledChange?.(isEnabled);
  });
  applyAttorneySectionValidators(form, prefix, !!enabledControl.value);
}
