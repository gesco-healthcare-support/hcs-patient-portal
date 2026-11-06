import { BookingMode } from './booking-mode';

/**
 * True when the booker chose "not booked in this portal" while in re-evaluation mode: a
 * re-evaluation of a first evaluation that predates the portal, so there is no prior confirmation
 * number to load. The server accepts it as submit mode 4 (`BookingSubmitMode.RevalWithoutSource`,
 * from the generated proxy enum). Whether the choice is offered at all is `showRevalEntryChoice`.
 */
export function isRevalWithoutSource(mode: BookingMode, ticked: boolean): boolean {
  return mode === 'reval' && ticked;
}
