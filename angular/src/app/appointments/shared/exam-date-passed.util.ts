/** IANA zone the whole business runs in. */
const PACIFIC_ZONE = 'America/Los_Angeles';

/** Today's calendar date (YYYY-MM-DD) in Pacific time at the given instant. */
export function pacificToday(now: Date = new Date()): string {
  // en-CA formats as YYYY-MM-DD, so the result sorts and compares as text.
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: PACIFIC_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/**
 * True when the exam's calendar day (Pacific) is strictly before today (Pacific).
 *
 * The API sends the exam date as a zoneless calendar value, so its first ten characters ARE the
 * Pacific day; it is never parsed into an instant, which is what keeps it on the right day in a
 * browser set to another zone. An absent or unreadable date is "not passed" so the button stays hidden.
 */
export function isExamDatePassed(
  appointmentDate: string | null | undefined,
  now: Date = new Date(),
): boolean {
  const day = /^\d{4}-\d{2}-\d{2}/.exec(appointmentDate ?? '')?.[0];
  return !!day && day < pacificToday(now);
}
