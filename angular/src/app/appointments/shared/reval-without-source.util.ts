import { BookingSubmitMode } from '../../proxy/enums/booking-submit-mode.enum';
import { BookingMode } from './booking-mode';

/**
 * Re-evaluation of a first evaluation that was NOT booked in this portal (it predates the portal),
 * so there is no prior confirmation number to load.
 *
 * <p>The server accepts it as submit mode 4 (`BookingSubmitMode.RevalWithoutSource`). The generated
 * proxy enum has no such member yet and `proxy/` must not be hand-edited, so the value is declared
 * here. A later `abp generate-proxy` adds the member; this constant can then be replaced by it.</p>
 */
export const REVAL_WITHOUT_SOURCE_SUBMIT_MODE = 4 as BookingSubmitMode;

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
