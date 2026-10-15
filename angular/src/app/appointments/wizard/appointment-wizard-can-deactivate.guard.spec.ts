import { appointmentWizardCanDeactivateGuard } from './appointment-wizard-can-deactivate.guard';

import type { AppointmentWizardComponent } from './appointment-wizard.component';

/**
 * The booking wizard's CanDeactivate guard -- the only thing standing between an abandoned dirty
 * booking and a silent navigation away from it.
 *
 * <p>WHAT THIS OWNS, and it is deliberately narrow. The DECISION belongs to the component
 * (`canDeactivate()`), which weighs a dirty form against a reval session and a completed submit;
 * that logic is covered where it lives. What is only observable here is the DELEGATION: that the
 * guard asks the component rather than deciding for itself, and that it passes both answers
 * through unchanged. A guard that returned a constant would be indistinguishable from a working
 * one without the pair of specs below.</p>
 *
 * <p>The component is imported as a TYPE ONLY. `CanDeactivateFn<AppointmentWizardComponent>`
 * needs the type and nothing else, so `import type` keeps the real component -- a 900-line file
 * with a large dependency graph -- out of this test bundle entirely.</p>
 *
 * <p>No patient data is involved; the stub carries no appointment state at all.</p>
 */
describe('appointmentWizardCanDeactivateGuard', () => {
  /** Minimal stand-in exposing only the member the guard is allowed to touch. */
  function wizard(answer: boolean, calls: string[]): AppointmentWizardComponent {
    return {
      canDeactivate: () => {
        calls.push('canDeactivate');
        return answer;
      },
    } as unknown as AppointmentWizardComponent;
  }

  function run(component: AppointmentWizardComponent) {
    return appointmentWizardCanDeactivateGuard(component, {} as never, {} as never, {} as never);
  }

  it('allows the navigation when the wizard says it may leave', () => {
    const calls: string[] = [];

    expect(run(wizard(true, calls))).toBeTrue();
    expect(calls).toEqual(['canDeactivate']);
  });

  it('blocks the navigation when the wizard says it may not', () => {
    // The paired control. Without it, a guard hardcoded to `true` would satisfy the spec above.
    const calls: string[] = [];

    expect(run(wizard(false, calls))).toBeFalse();
    expect(calls).toEqual(['canDeactivate']);
  });
});
