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
 * Whether a re-evaluation load goes through the number + date-of-birth lookup. Only when the outside
 * booker typed a date: without one the load is the ordinary read, which works for a party to the
 * earlier appointment exactly as before.
 */
export function usesDateOfBirthLookup(
  flow: 'reval' | 'reRequest' | 'reBook',
  dateOfBirth: string | null | undefined,
  isInternalBooker: boolean,
): boolean {
  return flow === 'reval' && !isInternalBooker && !!(dateOfBirth ?? '').trim();
}

/** The lookup answered nothing: a wrong number, a wrong date, or a case that cannot be re-evaluated. */
export const NO_SOURCE_FOUND_FOR_DOB_MESSAGE =
  'No approved appointment matches that confirmation number and date of birth.';

/**
 * The ordinary read found nothing for an outside booker. They may simply not be a party to it, so
 * the message says what to add rather than only that nothing was found.
 */
export const NO_SOURCE_FOUND_ADD_DOB_MESSAGE =
  "No appointment was found for that confirmation number. If you were not part of the earlier appointment, also enter the patient's date of birth.";
