import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import {
  AddressInput,
  AddressSuggestion,
  AddressValidationProvider,
  StandardizedAddress,
  ValidationResult,
} from './address-validation.provider';

/** Client config for the Smarty adapter (embedded "website" key + endpoints). */
export interface SmartyConfig {
  /** Embedded/website key (safe in the browser; gated by Smarty's host allow-list). */
  key: string;
  /** US Autocomplete Pro lookup endpoint. */
  autocompleteUrl: string;
  /** US Street Address verification endpoint. */
  verifyUrl: string;
}

/** Shape of a US Autocomplete Pro suggestion (only the fields we use). */
interface SmartyAutocompleteSuggestion {
  street_line?: string;
  secondary?: string;
  city?: string;
  state?: string;
  zipcode?: string;
}

/** Shape of a US Street Address candidate (only the fields we use). */
interface SmartyStreetCandidate {
  delivery_line_1?: string;
  components?: {
    primary_number?: string;
    street_predirection?: string;
    street_name?: string;
    street_suffix?: string;
    street_postdirection?: string;
    secondary_designator?: string;
    secondary_number?: string;
    city_name?: string;
    state_abbreviation?: string;
    zipcode?: string;
    plus4_code?: string;
  };
  analysis?: { dpv_match_code?: string };
}

/**
 * F2 / address validation (2026-05-29) -- Smarty adapter (Adrian decision
 * 2026-05-31). Implements the vendor-neutral provider against Smarty's US
 * Autocomplete Pro (type-ahead) + US Street Address (USPS CASS standardization)
 * APIs, using the embedded website key. Wired in only when a key is configured
 * (see app.config); otherwise the mock stays active, so this never breaks dev or
 * a key-less environment.
 *
 * Both methods swallow transport errors -- autocomplete yields [] and validate
 * yields `{ status: 'error', matchesInput: true }` -- so a Smarty outage or
 * quota exhaustion never blocks booking submission.
 *
 * Plain class (no Injectable decorator): the provider factory in app.config
 * constructs it manually via `new`, passing an injected HttpClient + the
 * resolved config, so it is never resolved through a DI token.
 */
export class SmartyAddressProvider extends AddressValidationProvider {
  constructor(
    private readonly http: HttpClient,
    private readonly config: SmartyConfig,
  ) {
    super();
  }

  autocomplete(query: string): Observable<AddressSuggestion[]> {
    const q = (query ?? '').trim();
    if (q.length < 3) {
      return of([]);
    }
    const params = new HttpParams().set('key', this.config.key).set('search', q);
    return this.http
      .get<{
        suggestions?: SmartyAutocompleteSuggestion[];
      }>(this.config.autocompleteUrl, { params })
      .pipe(
        map((res) => (res?.suggestions ?? []).map((s) => this.toSuggestion(s))),
        catchError(() => of([])),
      );
  }

  validate(input: AddressInput): Observable<ValidationResult> {
    const street = (input.street ?? '').trim();
    if (!street) {
      return of({ status: 'unverified', matchesInput: true });
    }
    let params = new HttpParams()
      .set('key', this.config.key)
      .set('street', street)
      .set('candidates', '1');
    if (input.suite) params = params.set('secondary', input.suite);
    if (input.city) params = params.set('city', input.city);
    if (input.state) params = params.set('state', input.state);
    if (input.zip) params = params.set('zipcode', input.zip);

    return this.http.get<SmartyStreetCandidate[]>(this.config.verifyUrl, { params }).pipe(
      map((candidates) => this.toResult(input, candidates)),
      catchError(() => of<ValidationResult>({ status: 'error', matchesInput: true })),
    );
  }

  private toSuggestion(s: SmartyAutocompleteSuggestion): AddressSuggestion {
    const street = s.street_line ?? '';
    const suite = s.secondary || undefined;
    const city = s.city ?? '';
    const state = s.state ?? '';
    const zip = s.zipcode ?? '';
    const text = `${[street, suite].filter(Boolean).join(' ')}, ${city}, ${state} ${zip}`.trim();
    return { text, street, suite, city, state, zip };
  }

  private toResult(
    input: AddressInput,
    candidates: SmartyStreetCandidate[] | null,
  ): ValidationResult {
    const candidate = (candidates ?? [])[0];
    if (!candidate?.components) {
      return { status: 'unverified', matchesInput: true };
    }
    const c = candidate.components;
    const street = [
      c.primary_number,
      c.street_predirection,
      c.street_name,
      c.street_suffix,
      c.street_postdirection,
    ]
      .filter(Boolean)
      .join(' ');
    const suite =
      [c.secondary_designator, c.secondary_number].filter(Boolean).join(' ') || undefined;
    const zip = c.plus4_code ? `${c.zipcode}-${c.plus4_code}` : (c.zipcode ?? '');
    const standardized: StandardizedAddress = {
      street,
      suite,
      city: c.city_name ?? '',
      state: c.state_abbreviation ?? '',
      zip,
    };
    const matchesInput = this.matchesInput(input, standardized);
    return {
      status: matchesInput ? 'verified' : 'corrected',
      standardized,
      matchesInput,
    };
  }

  private matchesInput(input: AddressInput, std: StandardizedAddress): boolean {
    const norm = (v: string | null | undefined) => (v ?? '').trim().toUpperCase();
    return (
      norm(input.street) === norm(std.street) &&
      norm(input.suite) === norm(std.suite) &&
      norm(input.city) === norm(std.city) &&
      norm(input.state) === norm(std.state) &&
      norm(input.zip) === norm(std.zip)
    );
  }
}
