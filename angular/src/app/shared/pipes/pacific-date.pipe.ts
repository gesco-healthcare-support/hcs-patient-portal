import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

/** IANA zone the whole business runs in. */
const PACIFIC_ZONE = 'America/Los_Angeles';

/**
 * The offset Pacific time was actually on at a given instant, as the '+HHMM' string Angular's
 * DatePipe expects.
 *
 * Angular's DatePipe does NOT accept IANA zone names -- its timezone parameter takes an offset or
 * a US abbreviation -- so the zone has to be resolved to an offset first. Resolving it PER INSTANT
 * via Intl is what makes daylight saving correct: the same wall-clock hour repeats every November
 * and is skipped every March, and only the instant says which offset applied.
 */
function pacificOffset(instant: Date): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: PACIFIC_ZONE,
    timeZoneName: 'longOffset',
  }).formatToParts(instant);

  // 'GMT-07:00' in daylight time, 'GMT-08:00' in standard time, bare 'GMT' at zero offset.
  const label = parts.find((part) => part.type === 'timeZoneName')?.value ?? '';
  const match = /GMT([+-])(\d{2}):(\d{2})/.exec(label);

  return match ? `${match[1]}${match[2]}${match[3]}` : '+0000';
}

/**
 * Rewrites a date string so `new Date()` reads it as a wall-clock value in the viewer's own zone
 * rather than as an instant. Two separate hazards, both of which land a calendar date on the wrong
 * day:
 *
 * - A ZONE DESIGNATOR (`Z` or `+05:30`) makes it an instant, which then gets converted. The API
 *   deliberately sends calendar dates without one, but this is the value that must not move.
 * - A DATE-ONLY string is parsed as UTC MIDNIGHT by the ECMAScript spec, while a date-time string
 *   without an offset is parsed as local. So `new Date('1985-09-03')` is 1985-09-02 in Pacific,
 *   whereas `new Date('1985-09-03T00:00:00')` is the 3rd. Adding the time component removes that
 *   trap for anything that passes a bare date.
 */
function asLocalWallClock(value: string): string {
  const zoneless = value.replace(/(?:Z|[+-]\d{2}:?\d{2})$/, '');
  return /^\d{4}-\d{2}-\d{2}$/.test(zoneless) ? `${zoneless}T00:00:00` : zoneless;
}

/**
 * Marks an unzoned instant string as UTC so it is not read as the viewer's local time.
 *
 * Every instant in this system is stored UTC, but they do not all reach the client saying so. Our
 * own entities do, because the clock pin makes ABP kind them Utc. ABP's OWN module entities do NOT
 * -- `AuditLog.ExecutionTime` and `EntityChange.ChangeTime` carry no value converter (measured
 * 2026-08-27, ~55 such properties), and both are rendered in this app: the admin hub log table and
 * the appointment change-log timeline. Without this they would be parsed as local wall-clock time
 * and read 7 or 8 hours off.
 *
 * Safe because this pipe is only ever applied to instants; a calendar date goes through
 * `calendarDate`, which does the opposite.
 */
function asUtcInstant(value: string): string {
  return /(?:Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`;
}

function toDate(value: string | number | Date | null | undefined): Date | null {
  if (value === null || value === undefined || value === '') return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

/**
 * Renders an INSTANT -- a moment that happened -- in Pacific time.
 *
 * Use this for anything the server stamped: creation and modification times, approval times,
 * consent responses, packet generation, invite expiry. Those arrive as UTC with a trailing `Z`,
 * and the built-in `| date` pipe would render them in whatever zone the VIEWER's machine is set
 * to. That is wrong for a business that operates in one zone: two people looking at the same
 * appointment should read the same time, and the time they read should be the clinic's.
 *
 * DO NOT use it on a CALENDAR DATE -- a date of birth, a date of injury, the appointment date, a
 * slot date. Those carry no zone (the API deliberately sends them with no `Z`), so there is
 * nothing to convert and converting one shifts it into the previous day. Use `calendarDate` for
 * those. The split is the same one enforced on the server by
 * `CalendarDateNormalizationTests`; this pipe is the third layer where it can be got wrong.
 *
 * Format strings are passed straight through to Angular's DatePipe, so existing formats keep
 * their exact appearance and only the ZONE changes.
 */
@Pipe({ name: 'pacificDate', standalone: true, pure: true })
export class PacificDatePipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(
    value: string | number | Date | null | undefined,
    format = 'mediumDate',
  ): string | null {
    const date = toDate(typeof value === 'string' ? asUtcInstant(value) : value);
    if (!date) return null;

    return formatDate(date, format, this.locale, pacificOffset(date));
  }
}

/**
 * Renders a CALENDAR DATE verbatim -- the day somebody wrote down, with no zone conversion.
 *
 * A date of birth is not a moment in time. It has no zone, so shifting it by one is not a display
 * preference, it is a wrong value on a medical-legal record. This pipe exists so a template says
 * WHICH KIND of value it is rendering rather than leaving it to whoever reads the code next.
 *
 * It also defends the value: if a calendar date ever reaches the client carrying a `Z` -- which is
 * exactly what happened server-side on 2026-08-27, when pinning the clock made a date of birth
 * serialize as `1985-09-03T00:00:00Z` and render as 2 September -- the trailing designator is
 * stripped before parsing, so the written date still renders. The server-side exemption is the
 * real fix; this is a second line so a regression there is invisible to patients rather than
 * printed on their records.
 */
@Pipe({ name: 'calendarDate', standalone: true, pure: true })
export class CalendarDatePipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(
    value: string | number | Date | null | undefined,
    format = 'mediumDate',
  ): string | null {
    if (value === null || value === undefined || value === '') return null;

    const date = toDate(typeof value === 'string' ? asLocalWallClock(value) : value);
    if (!date) return null;

    return formatDate(date, format, this.locale);
  }
}
