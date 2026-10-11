import type { OfficeLetterheadDto, OfficeLetterheadFields } from './office-letterhead.service';

/** The text fields of the letterhead editor, in display order. */
export const LETTERHEAD_TEXT_KEYS = [
  'letterheadName',
  'letterheadTagline',
  'physicianName',
  'practiceName',
  'mailingStreet',
  'mailingCity',
  'mailingState',
  'mailingZip',
  'phone',
  'fax',
  'recordsDeliveryAddress',
  'recordsReleaseAddress',
] as const;

export type LetterheadTextKey = (typeof LETTERHEAD_TEXT_KEYS)[number];

/** Editor state: every text field as a string, the fee as the raw text typed. */
export interface LetterheadForm {
  text: Record<LetterheadTextKey, string>;
  fee: string;
}

export function emptyLetterheadForm(): LetterheadForm {
  const text = {} as Record<LetterheadTextKey, string>;
  for (const key of LETTERHEAD_TEXT_KEYS) {
    text[key] = '';
  }
  return { text, fee: '' };
}

export function letterheadFormFromDto(dto: OfficeLetterheadDto | null | undefined): LetterheadForm {
  const form = emptyLetterheadForm();
  for (const key of LETTERHEAD_TEXT_KEYS) {
    form.text[key] = dto?.[key] ?? '';
  }
  form.fee = dto?.missedAppointmentFee == null ? '' : dto.missedAppointmentFee.toFixed(2);
  return form;
}

/**
 * Parses the fee box: blank is "no fee" (null), otherwise a non-negative amount with at most
 * two decimals and an optional leading "$". Returns undefined when the text is not a valid
 * amount, so the caller can refuse to save rather than guess.
 */
export function parseFee(raw: string): number | null | undefined {
  const text = raw.trim().replace(/^\$/, '').replace(/,/g, '').trim();
  if (text.length === 0) {
    return null;
  }
  if (!/^\d+(\.\d{1,2})?$/.test(text)) {
    return undefined;
  }
  return Number(text);
}

/** Builds the save payload: blank text is sent as null so the server clears it to the default. */
export function letterheadInputFromForm(form: LetterheadForm): OfficeLetterheadFields | undefined {
  const fee = parseFee(form.fee);
  if (fee === undefined) {
    return undefined;
  }
  const input: OfficeLetterheadFields = { missedAppointmentFee: fee };
  for (const key of LETTERHEAD_TEXT_KEYS) {
    const value = form.text[key].trim();
    input[key] = value.length ? value : null;
  }
  return input;
}
