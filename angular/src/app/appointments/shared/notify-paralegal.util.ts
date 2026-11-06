import { paralegalRoleForSide, type LawOfficeSide } from '../../shared/auth/external-user-roles';
import type { StepErrorField } from '../wizard/step-errors.util';
import type { AppointmentAuthorizedUserDraft } from '../sections/appointment-add-authorized-users.component';

/**
 * "Send all notifications to my paralegal" -- the attorney booker's shortcut. Ticking it and
 * giving a name + email adds that person to the booking's authorized users (accessors) with the
 * booker's side's paralegal role. The lawyer keeps receiving every email: nothing is removed.
 *
 * Pure functions, so they unit-test without a TestBed.
 */
export interface NotifyParalegalState {
  enabled: boolean;
  firstName: string;
  lastName: string;
  email: string;
}

export const EMPTY_NOTIFY_PARALEGAL: NotifyParalegalState = {
  enabled: false,
  firstName: '',
  lastName: '',
  email: '',
};

/** Default access type of the authorized-user section: 23 = View (24 = Edit). */
export const NOTIFY_PARALEGAL_ACCESS_TYPE_ID = 23;

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type NotifyParalegalField = 'firstName' | 'lastName' | 'email';

/** Missing/invalid fields; empty when the box is unticked or everything is filled in. */
export function notifyParalegalErrors(state: NotifyParalegalState): NotifyParalegalField[] {
  if (!state.enabled) {
    return [];
  }
  const errors: NotifyParalegalField[] = [];
  if (!state.firstName.trim()) {
    errors.push('firstName');
  }
  if (!state.lastName.trim()) {
    errors.push('lastName');
  }
  const email = state.email.trim();
  if (!email || email.length > 256 || !EMAIL_PATTERN.test(email)) {
    errors.push('email');
  }
  return errors;
}

/**
 * The authorized-user draft the tick adds, or null when unticked/invalid. The server resolves
 * the email to a user (or invites one) on submit, exactly as for any typed authorized user.
 */
export function notifyParalegalDraft(
  state: NotifyParalegalState,
  side: LawOfficeSide,
): AppointmentAuthorizedUserDraft | null {
  if (!state.enabled || notifyParalegalErrors(state).length > 0) {
    return null;
  }
  return {
    firstName: state.firstName.trim(),
    lastName: state.lastName.trim(),
    email: state.email.trim(),
    userRole: paralegalRoleForSide(side),
    accessTypeId: NOTIFY_PARALEGAL_ACCESS_TYPE_ID,
  };
}

/** The users to submit: the typed list plus the ticked paralegal, never twice for one email. */
export function withNotifyParalegal(
  users: readonly AppointmentAuthorizedUserDraft[],
  state: NotifyParalegalState,
  side: LawOfficeSide | null,
): AppointmentAuthorizedUserDraft[] {
  const draft = side ? notifyParalegalDraft(state, side) : null;
  if (!draft) {
    return [...users];
  }
  const already = users.some((u) => u.email.trim().toLowerCase() === draft.email.toLowerCase());
  return already ? [...users] : [...users, draft];
}

const FIELD_LABELS: Record<NotifyParalegalField, string> = {
  firstName: 'Paralegal / case manager first name',
  lastName: 'Paralegal / case manager last name',
  email: 'Paralegal / case manager email',
};

/** Error-summary rows for the wizard's "Please fix the following" banner. */
export function notifyParalegalSummary(state: NotifyParalegalState): StepErrorField[] {
  return notifyParalegalErrors(state).map((field) => ({
    control: 'notifyParalegal.' + field,
    label: FIELD_LABELS[field],
    message: field === 'email' ? 'Enter a valid email address' : 'Required',
  }));
}

/** Shown when the typed paralegal email is already an Authorized User with another role. */
export const NOTIFY_PARALEGAL_CONFLICT_MESSAGE =
  'This email is already an Authorized User with another role. Change that row to Paralegal / Case Manager or use another email.';

/** Error-summary row for the conflict, for the wizard's "Please fix the following" banner. */
export function notifyParalegalConflictSummary(): StepErrorField {
  return {
    control: 'notifyParalegal.conflict',
    label: 'Paralegal / case manager email',
    message: NOTIFY_PARALEGAL_CONFLICT_MESSAGE,
  };
}
