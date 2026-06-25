import { CanDeactivateFn } from '@angular/router';
import { AppointmentWizardComponent } from './appointment-wizard.component';

/**
 * #15 (2026-06-22): first CanDeactivate guard in the app. Prompts the booker to
 * Save / Discard / Stay when they abandon a dirty 'new' booking wizard. The
 * component owns the decision (canDeactivate); a clean form, a reval/re-request
 * session, or a successful submit leaves without prompting -- so it never blocks
 * the post-booking navigation.
 */
export const appointmentWizardCanDeactivateGuard: CanDeactivateFn<AppointmentWizardComponent> = (
  component,
) => component.canDeactivate();
