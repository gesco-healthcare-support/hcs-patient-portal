import type { RescheduleChainDto } from '../../../proxy/appointments/models';

/**
 * Pure helpers for the "rescheduled from" block on the appointment detail (phase 4d, 2026-08-05).
 *
 * An appointment created by finalizing a reschedule has a source it replaced. The block names that
 * source and, behind a disclosure, shows WHEN each step happened.
 *
 * The agreed DATE is deliberately not among them: it is this appointment's own date, already at the
 * top of the page. What is not otherwise recoverable is the sequence -- each side agrees when it
 * answers its own consent email, and staff may not finalize until later, so the three moments are
 * genuinely distinct and usually not the same day as the appointment.
 *
 * Standalone functions rather than component methods so they unit-test without a TestBed, and so
 * the internal and external detail components share one derivation instead of two that drift.
 */

/** True when this appointment replaced another one. */
export function hasRescheduleSource(chain: RescheduleChainDto | null | undefined): boolean {
  return !!chain?.sourceAppointmentId;
}

/**
 * What to call the source appointment. Prefers its confirmation number, which is what staff and
 * parties actually recognise; returns null rather than falling back to the Guid, because a raw id
 * on screen is noise no reader can act on.
 */
export function rescheduleSourceLabel(chain: RescheduleChainDto | null | undefined): string | null {
  if (!hasRescheduleSource(chain)) {
    return null;
  }

  const number = chain?.sourceRequestConfirmationNumber?.trim();
  return number ? number : null;
}

/** Which step a disclosure row describes. The template maps this to a localized caption. */
export type RescheduleChainStepKind = 'side-a-agreed' | 'side-b-agreed' | 'decided';

export interface RescheduleChainStep {
  kind: RescheduleChainStepKind;
  /** ISO timestamp from the API. */
  at: string;
}

/**
 * The disclosure's rows, in the order the steps happen.
 *
 * A step that never happened contributes NO row rather than a row reading "not recorded": an
 * internal-staff reschedule solicits only the sides that exist, so a missing Side B is normal and
 * printing an empty slot for it would read as a fault.
 */
export function rescheduleChainSteps(
  chain: RescheduleChainDto | null | undefined,
): RescheduleChainStep[] {
  if (!hasRescheduleSource(chain)) {
    return [];
  }

  const steps: RescheduleChainStep[] = [];
  const push = (kind: RescheduleChainStepKind, at: string | null | undefined) => {
    if (at) {
      steps.push({ kind, at });
    }
  };

  push('side-a-agreed', chain?.sideAAgreedAt);
  push('side-b-agreed', chain?.sideBAgreedAt);
  push('decided', chain?.decidedAt);

  return steps;
}
