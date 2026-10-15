/**
 * F2 / address validation (2026-05-29) -- pure resolver from a state string
 * (full name OR USPS 2-letter code) to the booking form's `StateId` GUID.
 *
 * Address providers return a state name ("California") or a USPS code ("CA"),
 * but every booking-form State control is a `<select>` keyed by the `StateId`
 * of the host `State` lookup. After an autocomplete pick or a standardization
 * result, callers run the returned state through this resolver to find the
 * matching lookup id; if no match, they leave the State select untouched.
 *
 * Pure (no DI) so the mapping is unit-tested directly.
 */

/** USPS 2-letter code -> full state name (50 states + DC). */
export const USPS_STATE_NAMES: Readonly<Record<string, string>> = {
  AL: 'Alabama',
  AK: 'Alaska',
  AZ: 'Arizona',
  AR: 'Arkansas',
  CA: 'California',
  CO: 'Colorado',
  CT: 'Connecticut',
  DE: 'Delaware',
  DC: 'District of Columbia',
  FL: 'Florida',
  GA: 'Georgia',
  HI: 'Hawaii',
  ID: 'Idaho',
  IL: 'Illinois',
  IN: 'Indiana',
  IA: 'Iowa',
  KS: 'Kansas',
  KY: 'Kentucky',
  LA: 'Louisiana',
  ME: 'Maine',
  MD: 'Maryland',
  MA: 'Massachusetts',
  MI: 'Michigan',
  MN: 'Minnesota',
  MS: 'Mississippi',
  MO: 'Missouri',
  MT: 'Montana',
  NE: 'Nebraska',
  NV: 'Nevada',
  NH: 'New Hampshire',
  NJ: 'New Jersey',
  NM: 'New Mexico',
  NY: 'New York',
  NC: 'North Carolina',
  ND: 'North Dakota',
  OH: 'Ohio',
  OK: 'Oklahoma',
  OR: 'Oregon',
  PA: 'Pennsylvania',
  RI: 'Rhode Island',
  SC: 'South Carolina',
  SD: 'South Dakota',
  TN: 'Tennessee',
  TX: 'Texas',
  UT: 'Utah',
  VT: 'Vermont',
  VA: 'Virginia',
  WA: 'Washington',
  WV: 'West Virginia',
  WI: 'Wisconsin',
  WY: 'Wyoming',
};

/** A state lookup option: its GUID id and display name (e.g. "California"). */
export interface StateLookupOption {
  id: string;
  name: string;
}

/**
 * Resolve a state string to the matching lookup id, or null if no match.
 * Accepts a USPS 2-letter code (case-insensitive) or a full name
 * (case-insensitive, trimmed).
 */
export function resolveStateId(
  state: string | null | undefined,
  lookup: ReadonlyArray<StateLookupOption>,
): string | null {
  if (!state) {
    return null;
  }
  const trimmed = state.trim();
  if (!trimmed) {
    return null;
  }

  // A 2-letter token is treated as a USPS code; expand to the full name.
  const fullName =
    trimmed.length === 2 ? (USPS_STATE_NAMES[trimmed.toUpperCase()] ?? trimmed) : trimmed;

  const target = fullName.toLowerCase();
  const match = lookup.find((option) => (option.name ?? '').trim().toLowerCase() === target);
  return match ? match.id : null;
}
