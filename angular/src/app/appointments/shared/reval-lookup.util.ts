import type { AppointmentAccessorWithNavigationPropertiesDto } from '../../proxy/appointment-accessors/models';
import type { AppointmentEmployerDetailDto } from '../../proxy/appointment-employer-details/models';
import type { AppointmentInjuryDetailWithNavigationPropertiesDto } from '../../proxy/appointment-injury-details/models';
import type {
  ApplicantAttorneyDetailsDto,
  DefenseAttorneyDetailsDto,
  ReEvaluationSourceDto,
} from '../../proxy/appointments/models';
import type { BookingMode } from './booking-mode';

/**
 * The parts of an earlier appointment the booking form copies, besides the appointment itself and
 * its patient, claim examiner and insurance (which arrive with the appointment).
 */
export interface SourceSections {
  employer: AppointmentEmployerDetailDto | null;
  applicantAttorney: ApplicantAttorneyDetailsDto | null;
  defenseAttorney: DefenseAttorneyDetailsDto | null;
  injuries: AppointmentInjuryDetailWithNavigationPropertiesDto[];
  accessors: AppointmentAccessorWithNavigationPropertiesDto[];
}

/**
 * The sections, taken from the re-evaluation lookup's single answer (2026-11-06). A caller who is
 * not a party to the earlier appointment cannot make the per-appointment reads the form otherwise
 * makes, so everything comes from here; the Authorized Users are passed in, because only a party
 * may read them (the caller fetches them, or passes none).
 */
export function sectionsFromLookup(
  found: ReEvaluationSourceDto,
  accessors: AppointmentAccessorWithNavigationPropertiesDto[],
): SourceSections {
  return {
    employer: found.source?.appointmentEmployerDetail?.appointmentEmployerDetail ?? null,
    applicantAttorney: found.applicantAttorney ?? null,
    defenseAttorney: found.defenseAttorney ?? null,
    injuries: found.source?.appointmentInjuryDetails ?? [],
    accessors,
  };
}

/**
 * The patient date-of-birth box: re-evaluation mode, outside bookers only. Staff can load any case in
 * their office by its number, so they never need it.
 */
export function showsSourceDateOfBirth(mode: BookingMode, isInternalBooker: boolean): boolean {
  return mode === 'reval' && !isInternalBooker;
}

/**
 * Whether a load goes through the re-evaluation lookup. 2026-11-08 (W2): every re-evaluation does,
 * staff and parties included, because only the lookup walks to the NEWEST case in the chain the number
 * belongs to. A date of birth is sent only when one was typed; a party to the newest case needs none.
 * Re-request and re-book are about the exact case, so they keep the ordinary read.
 */
export function loadsThroughSourceLookup(flow: 'reval' | 'reRequest' | 'reBook'): boolean {
  return flow === 'reval';
}

/** A party's own case that cannot be re-evaluated (not approved, nor a re-evaluation the patient missed). */
export const REVAL_SOURCE_NOT_ELIGIBLE_MESSAGE =
  'You can re-evaluate only an approved appointment.';

/** The lookup answered nothing: a wrong number, a wrong date, or a case that cannot be re-evaluated. */
export const NO_SOURCE_FOUND_FOR_DOB_MESSAGE =
  'No approved appointment matches that confirmation number and date of birth.';

/**
 * Nothing found without a date, for an outside booker. They may simply not be a party to the case, or
 * (2026-11-08) not to a later evaluation of the same patient, which is the one the form would use; so
 * the message says what to add rather than only that nothing was found.
 */
export const NO_SOURCE_FOUND_ADD_DOB_MESSAGE =
  "No appointment was found for that confirmation number. If you were not part of the earlier appointment, or of a later evaluation of the same patient, also enter the patient's date of birth.";

/**
 * 2026-11-08 (W2): the note shown when the form was filled from a later case than the one whose number
 * was typed (a re-evaluation of it, or a rescheduled replacement). Null when it was not.
 */
export function newerSourceNote(found: {
  filledFromNewer?: boolean;
  source?: { appointment?: { requestConfirmationNumber?: string | null } | null } | null;
}): string | null {
  const newer = found.source?.appointment?.requestConfirmationNumber;
  return found.filledFromNewer && newer
    ? `A newer evaluation exists for this case (${newer}); we filled the form from that one.`
    : null;
}
