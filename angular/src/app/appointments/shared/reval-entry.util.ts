import { BookingMode } from './booking-mode';

/** How a re-evaluation identifies its first evaluation (the radio cards at the top of the form). */
export type RevalEntryChoice = 'number' | 'lookup' | 'before';

/** One radio card at the top of a re-evaluation. */
export interface RevalEntryCard {
  value: RevalEntryChoice;
  title: string;
  hint: string;
}

/** The three radio cards, in display order. */
export const REVAL_ENTRY_CARDS: readonly RevalEntryCard[] = [
  {
    value: 'number',
    title: 'I have the confirmation number',
    hint: 'Enter it and we fill in the form from the earlier evaluation.',
  },
  {
    value: 'lookup',
    title: "I don't know the confirmation number",
    hint: "Give the patient's name, date of birth and claim number, and we email the number to you.",
  },
  {
    value: 'before',
    title: 'The first exam was before this portal',
    hint: 'There is nothing to load. Fill in every section as for a new request.',
  },
];

/**
 * 2026-11-06 (R3): staff can open any case in their office, so the "I don't know it" card is for
 * outside bookers only; staff see the other two.
 */
export const STAFF_REVAL_ENTRY_CARDS: readonly RevalEntryCard[] = REVAL_ENTRY_CARDS.filter(
  (c) => c.value !== 'lookup',
);

/**
 * The choice is offered while a re-evaluation has no source loaded. Once a source is loaded
 * (typed in, or arriving as ?source= from the finished case) the choice is settled and hidden.
 */
export function showRevalEntryChoice(
  mode: BookingMode,
  sourceConfirmationNumber: string | null,
): boolean {
  return mode === 'reval' && !sourceConfirmationNumber;
}

/** The confirmation-number box: re-book always; re-evaluation once the number path is chosen or loaded. */
export function showSourceLookupFor(
  mode: BookingMode,
  choice: RevalEntryChoice | null,
  sourceConfirmationNumber: string | null,
): boolean {
  if (mode === 'reBook') return true;
  if (mode !== 'reval') return false;
  return choice === 'number' || !!sourceConfirmationNumber;
}

/** The prefill banner shows whenever the form holds data copied from an earlier case. */
export function showPrefillBanner(
  mode: BookingMode,
  sourceConfirmationNumber: string | null,
): boolean {
  return mode !== 'new' && !!sourceConfirmationNumber;
}

/** A re-evaluation launched from a finished case carries ?source= and loads without the choice. */
export function shouldAutoLoadSource(mode: BookingMode, source: string | null): boolean {
  return (mode === 'reval' || mode === 'reBook') && !!(source ?? '').trim();
}
