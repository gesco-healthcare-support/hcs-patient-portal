import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import {
  AddressInput,
  AddressSuggestion,
  AddressValidationProvider,
  StandardizedAddress,
  ValidationResult,
} from './address-validation.provider';
import { USPS_STATE_NAMES } from './state-resolver';

/**
 * F2 / address validation (2026-05-29) -- deterministic, offline mock used in
 * development and tests until the Smarty adapter ships (T4). It is also the
 * default provider when no vendor key is configured, so the booking form
 * degrades to "no suggestions, submit always allowed".
 *
 * Behaviour is intentionally simple + stable:
 *  - autocomplete: nothing under 3 chars; otherwise two synthetic candidates
 *    that echo the query (all data is fictional, never patient PHI).
 *  - validate: USPS-style uppercases street/city, normalizes state to a
 *    2-letter code, pads a bare 5-digit zip to ZIP+4. Reports `corrected` when
 *    that differs from the input (so the standardization dialog is exercised in
 *    dev), else `verified`. Empty street -> `unverified`. Never errors.
 */
@Injectable()
export class MockAddressProvider extends AddressValidationProvider {
  private readonly nameToCode: Readonly<Record<string, string>> = Object.entries(
    USPS_STATE_NAMES,
  ).reduce<Record<string, string>>((acc, [code, name]) => {
    acc[name.toLowerCase()] = code;
    return acc;
  }, {});

  autocomplete(query: string): Observable<AddressSuggestion[]> {
    const q = (query ?? '').trim();
    if (q.length < 3) {
      return of([]);
    }
    return of([
      {
        street: `${q} Main St`,
        city: 'Springfield',
        state: 'IL',
        zip: '62704',
        text: `${q} Main St, Springfield, IL 62704`,
      },
      {
        street: `${q} Oak Ave`,
        suite: 'Ste 200',
        city: 'Madison',
        state: 'WI',
        zip: '53703',
        text: `${q} Oak Ave Ste 200, Madison, WI 53703`,
      },
    ]);
  }

  validate(input: AddressInput): Observable<ValidationResult> {
    const street = (input.street ?? '').trim();
    if (!street) {
      return of({ status: 'unverified', matchesInput: true });
    }

    const standardized: StandardizedAddress = {
      street: street.toUpperCase(),
      suite: (input.suite ?? '').trim() || undefined,
      city: (input.city ?? '').trim().toUpperCase(),
      state: this.toCode(input.state),
      zip: this.toZipPlus4(input.zip),
    };

    const matches = this.matchesInput(input, standardized);
    return of({
      status: matches ? 'verified' : 'corrected',
      standardized,
      matchesInput: matches,
    });
  }

  /** Full name or code -> uppercase 2-letter code (best effort; passthrough otherwise). */
  private toCode(state: string | null | undefined): string {
    const s = (state ?? '').trim();
    if (!s) {
      return '';
    }
    if (s.length === 2) {
      return s.toUpperCase();
    }
    return this.nameToCode[s.toLowerCase()] ?? s.toUpperCase();
  }

  /** Pad a bare 5-digit zip to a deterministic ZIP+4; leave others as-is. */
  private toZipPlus4(zip: string | null | undefined): string {
    const z = (zip ?? '').trim();
    return /^\d{5}$/.test(z) ? `${z}-0000` : z;
  }

  private matchesInput(input: AddressInput, std: StandardizedAddress): boolean {
    const norm = (v: string | null | undefined) => (v ?? '').trim().toUpperCase();
    return (
      norm(input.street) === std.street &&
      norm(input.suite) === norm(std.suite) &&
      norm(input.city) === std.city &&
      this.toCode(input.state) === std.state &&
      norm(input.zip) === std.zip
    );
  }
}
