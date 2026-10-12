import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import type { PartyConsentRequestDto } from '../../proxy/appointment-change-requests/models';

/** The card's one-line headline: what the party is being asked to agree to. */
export function partyConsentHeadline(item: PartyConsentRequestDto): string {
  if (item.changeRequestType === ChangeRequestType.Reschedule) {
    return item.requestedNewDateTime
      ? `Move this appointment to ${item.requestedNewDateTime}?`
      : 'Move this appointment to a new date?';
  }
  return 'Cancel this appointment?';
}

/** Which appointment it is, in the words a party tracks cases by. */
export function partyConsentSubject(item: PartyConsentRequestDto): string {
  return [item.patientName, item.confirmationNumber].filter((part) => !!part?.trim()).join(' - ');
}

/**
 * The confirmation text before an answer is sent. Answering is final -- the server refuses a
 * second answer -- so both choices are confirmed, and the text says what each one does.
 */
export function partyConsentConfirmText(item: PartyConsentRequestDto, approved: boolean): string {
  const isReschedule = item.changeRequestType === ChangeRequestType.Reschedule;
  if (approved) {
    return isReschedule
      ? 'Agree to the new date? Once both sides agree, the appointment is moved.'
      : 'Agree to cancel this appointment? Once both sides agree, it is cancelled.';
  }
  return isReschedule
    ? 'Decline the new date? The office will be told and may propose another.'
    : 'Decline the cancellation? The office will be told and the appointment stays booked.';
}
