import { NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';

/**
 * Booking-availability rules, extracted verbatim from `AppointmentAddComponent` in phase 4a
 * (2026-08-03) so the reschedule flow can apply the SAME rules instead of duplicating them.
 *
 * <p>Pure and DI-free, which is the point: these are the decisions worth unit-testing, and the
 * component around them stays thin (same shape as phase 3's `schedule-calendar.util.ts`).</p>
 *
 * <p>Dates are compared at LOCAL midnight, never UTC. The original helpers were deliberate about
 * this because a slot's `AvailableDate` is a plain calendar date, and shifting it into UTC moves it
 * across a day boundary for half the world.</p>
 *
 * <p>The client rules are a UX MIRROR only. The server's `BookingPolicyValidator` is authoritative
 * and reads `SystemParameter.AppointmentLeadTime` per tenant, so nothing here may be treated as a
 * security or correctness boundary.</p>
 */

/** Local midnight today. Isolated so tests and callers share one definition. */
function localMidnightToday(): Date {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return today;
}

/** Parses a `YYYY-MM-DD` key at LOCAL midnight (month is 0-indexed in JS). */
function parseKeyToLocalDate(dateKey: string): Date {
  const [year, month, day] = dateKey.split('-').map(Number);
  const parsed = new Date(year, month - 1, day);
  parsed.setHours(0, 0, 0, 0);
  return parsed;
}

/** `YYYY-MM-DD`, zero-padded. The canonical key for every availability comparison. */
export function toDateKey(year: number, month: number, day: number): string {
  return `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day
    .toString()
    .padStart(2, '0')}`;
}

/**
 * The date part of an API value, which may be a plain date or an ISO timestamp. Returns null for
 * anything too short to be a date, so a malformed value cannot masquerade as availability.
 */
export function toDateKeyFromApi(value?: string | null): string | null {
  if (!value) return null;
  const parsed = value.includes('T') ? value.split('T')[0] : value;
  if (parsed.length < 10) return null;
  return parsed.slice(0, 10);
}

/** Whole days between local-midnight today and the given key. Negative for past dates. */
export function daysFromTodayKey(dateKey: string): number {
  const selected = parseKeyToLocalDate(dateKey);
  const msPerDay = 24 * 60 * 60 * 1000;
  return Math.round((selected.getTime() - localMidnightToday().getTime()) / msPerDay);
}

/** True when the date falls inside the lead-time window and so cannot be booked. */
export function isBeforeMinimumBookingDateKey(dateKey: string, leadDays: number): boolean {
  const threshold = localMidnightToday();
  threshold.setDate(threshold.getDate() + leadDays);
  return parseKeyToLocalDate(dateKey) < threshold;
}

/**
 * True beyond the absolute booking ceiling.
 *
 * <p>NOTE (2026-06-11, preserved): this ceiling applies to EVERY role -- nobody schedules past it.
 * The narrower 60-day external horizon is NOT enforced here; between 60 and 90 days external users
 * still SEE the dates and are intercepted with the contact-staff notice on SELECTION. Encoding that
 * horizon as a disable would silently remove dates external users can currently pick, so this
 * function takes one `ceilingDays` and knows nothing about roles.</p>
 */
export function isBeyondCeilingKey(dateKey: string, ceilingDays: number): boolean {
  return daysFromTodayKey(dateKey) > ceilingDays;
}

/** Distinct date keys from raw API date values, skipping anything unusable. */
export function buildAvailableDateKeys(
  rawDates: ReadonlyArray<string | null | undefined>,
): Set<string> {
  const keys = new Set<string>();
  for (const raw of rawDates) {
    const key = toDateKeyFromApi(raw);
    if (key) {
      keys.add(key);
    }
  }
  return keys;
}

/** Inputs to a selectability decision. */
export interface SelectabilityContext {
  /** False before an appointment type is chosen, when the picker greys nothing out. */
  typeChosen: boolean;
  leadDays: number;
  ceilingDays: number;
  /** Dates with remaining capacity. EMPTY means nothing is bookable, not everything. */
  availableKeys: ReadonlySet<string>;
}

/**
 * Whether a calendar day can be picked. Mirrors the original
 * `markAppointmentDateDisabled` inverted, including its guard order.
 */
export function isSelectableDate(date: NgbDateStruct, context: SelectabilityContext): boolean {
  if (!date) return false;

  // With no type chosen the picker is not yet meaningful, so nothing is disabled.
  if (!context.typeChosen) return true;

  const key = toDateKey(date.year, date.month, date.day);

  if (isBeforeMinimumBookingDateKey(key, context.leadDays)) return false;
  if (isBeyondCeilingKey(key, context.ceilingDays)) return false;

  // Nothing loaded yet -> nothing bookable. The inverse would offer slots that do not exist.
  if (context.availableKeys.size === 0) return false;

  return context.availableKeys.has(key);
}
