/**
 * Wording for the booking-wizard failures that used to be swallowed (#604).
 *
 * <p>The wizard carried eight bare `catch {}` blocks. Five sat on the re-evaluation prefill
 * path, where they turned a failed GET into "the prior appointment had none of that". None of
 * those endpoints 404s for absent data: an appointment with no employer comes back as an empty
 * page, `by-appointment` injuries come back as an empty list, and the two attorney endpoints
 * return an explicit `null` with HTTP 200 (`AppointmentsAppService.cs:1700-1703`). So the catch
 * was only ever reachable on a REAL failure, and the wizard then let the booker submit a
 * re-evaluation missing sections the source appointment had, with nothing on screen to show
 * it.</p>
 *
 * <p>That is the BUG-045 failure mode. The Case Tracker stores the party groups as one opaque
 * JSON blob, so a section we dropped and a section that was legitimately empty are identical
 * downstream -- its silence can never confirm we sent everything.</p>
 *
 * <p>Pure functions over the error rather than component methods: the wizard is a
 * template-less base class with heavy DI and no spec file, so anything living inside it is
 * effectively untestable. Same reasoning as `auto-approve-outcome.ts`.</p>
 */

/** The shape we need from an Angular HttpErrorResponse, without depending on the class. */
interface HttpFailure {
  status?: number;
}

/** A source sub-resource that could not be read while prefilling from a prior appointment. */
export interface PrefillFailure {
  /** User-facing section name. Lower case -- it is rendered mid-sentence, inside a list. */
  section: string;
  /** HTTP status, when the failure carried one. Absent for a network or parse failure. */
  status?: number;
}

/** HTTP 403. The server knows who you are and refused anyway, so retrying cannot help. */
const FORBIDDEN = 403;

/** HTTP 404. */
const NOT_FOUND = 404;

function statusOf(err: unknown): number | undefined {
  return (err as HttpFailure | null)?.status;
}

/** Pairs a failed sub-resource GET with the section name the booker will recognise. */
export function toPrefillFailure(section: string, err: unknown): PrefillFailure {
  return { section, status: statusOf(err) };
}

/** Shared by the in-band "no appointment on the response" branch and the 404 catch. */
export const NO_SOURCE_FOUND_MESSAGE = 'No appointment was found for that confirmation number.';

/** The clean-prefill message. Only shown when every sub-resource loaded. */
export const SOURCE_LOADED_MESSAGE =
  'Prior appointment loaded. Review the details, choose a new date and time, then submit.';

/**
 * Address standardization is a convenience, and a failure must never block a booking -- but
 * the booker should know the address went in exactly as typed rather than assume it was
 * checked and correct.
 */
export const ADDRESS_CHECK_SKIPPED_MESSAGE =
  'We could not check your address formatting. Your addresses will be used exactly as typed.';

/** "a", "a and b", "a, b and c". */
function joinSections(sections: readonly string[]): string {
  if (sections.length <= 1) {
    return sections[0] ?? '';
  }
  return `${sections.slice(0, -1).join(', ')} and ${sections.at(-1)}`;
}

/**
 * Names the sections that did not copy across, or `null` when the prefill was clean.
 *
 * <p>Deliberately does NOT block the booking. The booker can still enter the missing details
 * by hand, and refusing to prefill anything because one sub-resource failed would be a worse
 * outcome than the one being fixed. What changes is that the gap is now visible and named.</p>
 */
export function sourcePrefillWarning(failures: readonly PrefillFailure[]): string | null {
  if (failures.length === 0) {
    return null;
  }

  const list = joinSections(failures.map((f) => f.section));

  if (failures.every((f) => f.status === FORBIDDEN)) {
    // Retrying is guaranteed to fail again, so the advice is "enter it", never "try again".
    return (
      `Prior appointment loaded, but not its ${list} -- you do not have permission to view ` +
      'that data. Enter those details manually before submitting.'
    );
  }

  return (
    `Prior appointment loaded, but not its ${list} -- that data could not be loaded. Enter ` +
    'those details manually, or load the prior appointment again to retry.'
  );
}

/**
 * Why the source lookup itself failed.
 *
 * <p>The old single message told the booker to check the confirmation number for every
 * failure, which is misleading advice for a 403 or a 500 -- the number is fine and re-reading
 * it wastes their time.</p>
 */
export function sourceLoadFailureMessage(err: unknown): string {
  const status = statusOf(err);

  if (status === FORBIDDEN) {
    return (
      'You do not have permission to view that appointment. Ask a supervisor to load it ' +
      'for you.'
    );
  }

  if (status === NOT_FOUND) {
    return NO_SOURCE_FOUND_MESSAGE;
  }

  return 'Unable to load that appointment. Please try again.';
}

/** Why the patient-by-email lookup failed. Both branches leave the manual path open. */
export function patientLoadFailureMessage(err: unknown): string {
  if (statusOf(err) === FORBIDDEN) {
    return (
      'You do not have permission to look up patients by email. Fill in the form below to ' +
      'enter the patient details.'
    );
  }

  return 'Unable to load patient. Please try again or fill in the form to create new.';
}
