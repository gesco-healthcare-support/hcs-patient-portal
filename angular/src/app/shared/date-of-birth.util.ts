/**
 * Date-of-birth conversion shared by the booking wizard, the appointment view
 * and the patient profile.
 *
 * These were three byte-identical private copies of `formatDateOfBirthForApi`
 * and two of `normalizePatientDateOfBirth` (#620, which recorded two of the
 * three). One home, so a correction lands everywhere at once -- which matters,
 * because the extracted version fixes a real defect. See below.
 */

/** The `{ year, month, day }` shape ngbDatepicker binds to. */
interface NgbDateLike {
  year?: number;
  month?: number;
  day?: number;
}

const pad = (value: number): string => String(value).padStart(2, '0');

/**
 * Convert a datepicker struct (or an already-formatted string) to the
 * `yyyy-MM-dd` the API expects. Returns null when there is nothing usable.
 *
 * FIXED WHILE EXTRACTING. All three copies built a `Date` and read the day back
 * out of `toISOString()`:
 *
 *     const d = new Date(obj.year, obj.month - 1, obj.day);
 *     return d.toISOString().split('T')[0];
 *
 * `new Date(y, m, d)` is LOCAL midnight and `toISOString()` renders UTC, so
 * anywhere east of Greenwich local midnight falls on the previous UTC day and
 * the date came back one day early. For a 1990-05-15 birth date:
 *
 *     UTC-08 Pacific      1990-05-15   correct
 *     UTC+00 London       1990-05-15   correct
 *     UTC+02 Berlin       1990-05-14   WRONG
 *     UTC+09 Tokyo        1990-05-14   WRONG
 *
 * Unnoticed because the users are Californian, but a date of birth is
 * medical-legal data and it travels to the Case Tracker on the intake payload,
 * so a silently shifted day is not cosmetic.
 *
 * Building the string from the parts removes the round trip, so the result no
 * longer depends on the runtime zone at all. Identical output to the old code
 * everywhere the old code was right.
 */
export function formatDateOfBirthForApi(value: unknown): string | null {
  if (!value) {
    return null;
  }
  if (typeof value === 'string') {
    return value;
  }
  const parts = value as NgbDateLike;
  if (parts?.year && parts?.month && parts?.day) {
    return `${parts.year}-${pad(parts.month)}-${pad(parts.day)}`;
  }
  return null;
}

/**
 * Reject a stored date of birth that cannot be a real one.
 *
 * Two rejections, both deliberate and carried over unchanged:
 * a year before 1900, and today's date -- the latter being the sentinel a
 * registration writes when no birth date was supplied, never a real entry.
 *
 * Returns the original string when it passes, so callers keep whatever
 * precision the API sent rather than a reformatted copy.
 */
export function normalizePatientDateOfBirth(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) {
    return null;
  }
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  if (year < 1900) {
    return null;
  }
  const today = new Date();
  if (year === today.getFullYear() && month === today.getMonth() + 1 && day === today.getDate()) {
    return null;
  }
  return value;
}
