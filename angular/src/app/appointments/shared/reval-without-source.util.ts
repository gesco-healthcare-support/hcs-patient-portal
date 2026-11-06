import { BookingMode } from './booking-mode';

/**
 * Re-evaluation of a first evaluation that was NOT booked in this portal (it predates the portal),
 * so there is no prior confirmation number to load. The server accepts it as submit mode 4
 * (`BookingSubmitMode.RevalWithoutSource`, from the generated proxy enum).
 */
/** True when the booker chose "not booked in this portal" while in re-evaluation mode. */
export function isRevalWithoutSource(mode: BookingMode, ticked: boolean): boolean {
  return mode === 'reval' && ticked;
}

/** The option is offered only for a re-evaluation that has not already loaded a source. */
export function canOfferRevalWithoutSource(
  mode: BookingMode,
  sourceConfirmationNumber: string | null,
): boolean {
  return mode === 'reval' && !sourceConfirmationNumber;
}
