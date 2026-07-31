import { Observable } from 'rxjs';

/**
 * F2 / address validation (2026-05-29) -- vendor-neutral contract for address
 * autocomplete + USPS standardization. The booking form's six address groups
 * talk only to this abstraction; the concrete vendor (Smarty -- Adrian decision
 * 2026-05-31) is a single adapter wired in at T4. A deterministic mock backs
 * all development + tests so the UI work lands before the vendor adapter.
 *
 * `state` is carried as the user typed it (a full name like "California" or a
 * USPS 2-letter code like "CA"); the booking form keys State by a `StateId`
 * GUID, so callers resolve `state` -> StateId via `state-resolver.ts`.
 */

/** An address as entered in one booking-form group. */
export interface AddressInput {
  street?: string | null;
  /** Secondary line: patient "Unit #" / insurance + CE "STE". */
  suite?: string | null;
  city?: string | null;
  /** Full state name or USPS 2-letter code, as entered. */
  state?: string | null;
  zip?: string | null;
}

/** One autocomplete candidate. `state` is the USPS 2-letter code. */
export interface AddressSuggestion {
  /** Single-line display text for the dropdown. */
  text: string;
  street: string;
  suite?: string;
  city: string;
  state: string;
  zip: string;
}

/** A provider-standardized (USPS-formatted) address. `state` is the 2-letter code. */
export interface StandardizedAddress {
  street: string;
  suite?: string;
  city: string;
  state: string;
  /** ZIP or ZIP+4 where the provider supplies it. */
  zip: string;
}

export type AddressValidationStatus = 'verified' | 'corrected' | 'unverified' | 'error';

/**
 * Outcome of a pre-submit standardization call.
 * - `verified`   : deliverable as entered (standardized == input modulo format).
 * - `corrected`  : deliverable but the provider changed something.
 * - `unverified` : provider could not confirm the address.
 * - `error`      : provider failed/unavailable (callers must degrade gracefully).
 * `matchesInput` is true when no "use suggested vs keep mine" prompt is needed.
 */
export interface ValidationResult {
  status: AddressValidationStatus;
  standardized?: StandardizedAddress;
  matchesInput: boolean;
}

/**
 * DI token + contract. Abstract class (not an interface) so Angular can inject
 * it directly; provided as the mock by default and swapped to the Smarty
 * adapter via config at T4.
 */
export abstract class AddressValidationProvider {
  /**
   * Type-ahead suggestions for a partial address. `sessionToken` is opaque:
   * a Google adapter uses it for session billing; the Smarty/mock adapters
   * ignore it. Returns [] (never throws) when unavailable.
   */
  abstract autocomplete(query: string, sessionToken?: string): Observable<AddressSuggestion[]>;

  /**
   * Validate + standardize a complete address. Must resolve to
   * `{ status: 'error', matchesInput: true }` (never throw) on provider
   * failure so the booking submit is never blocked.
   */
  abstract validate(input: AddressInput): Observable<ValidationResult>;
}
