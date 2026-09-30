import {
  ADDRESS_CHECK_SKIPPED_MESSAGE,
  NO_SOURCE_FOUND_MESSAGE,
  patientLoadFailureMessage,
  SOURCE_LOADED_MESSAGE,
  sourceLoadFailureMessage,
  sourcePrefillWarning,
  toPrefillFailure,
} from './booking-failure-message.util';

/**
 * Pins the wording the wizard shows for failures it used to swallow (#604).
 *
 * The branch that matters is 403 vs everything else: a 403 can never succeed on a retry, so
 * telling the booker to try again wastes their time, and telling them to enter the data by
 * hand is the only advice that works.
 */
describe('booking-failure-message.util (#604)', () => {
  /** Minimal stand-in for HttpErrorResponse -- the util reads `status` and nothing else. */
  const httpError = (status: number) => ({ status });

  describe('toPrefillFailure', () => {
    it('carries the section name and the HTTP status', () => {
      expect(toPrefillFailure('injuries', httpError(403))).toEqual({
        section: 'injuries',
        status: 403,
      });
    });

    it('leaves status undefined when the failure carried none', () => {
      // A network drop or a parse error rejects with no status at all.
      expect(toPrefillFailure('injuries', new Error('offline'))).toEqual({
        section: 'injuries',
        status: undefined,
      });
    });

    it('does not throw on a null rejection', () => {
      expect(toPrefillFailure('injuries', null)).toEqual({
        section: 'injuries',
        status: undefined,
      });
    });
  });

  describe('sourcePrefillWarning', () => {
    it('returns null for a clean prefill, so the caller falls back to the success message', () => {
      expect(sourcePrefillWarning([])).toBeNull();
    });

    it('names the one section that failed', () => {
      const msg = sourcePrefillWarning([{ section: 'injuries', status: 500 }]);
      expect(msg).toContain('injuries');
      expect(msg).toContain('could not be loaded');
    });

    it('tells a booker who lacks permission to enter it manually, never to retry', () => {
      const msg = sourcePrefillWarning([{ section: 'injuries', status: 403 }])!;
      expect(msg).toContain('do not have permission');
      expect(msg).toContain('manually');
      // The whole point of the 403 branch: retrying a refusal cannot work.
      expect(msg).not.toContain('retry');
    });

    it('falls back to the retryable wording when the failures are mixed', () => {
      // One 403 among transient failures must not claim the whole thing was a permission
      // problem -- reloading genuinely might fix the 500.
      const msg = sourcePrefillWarning([
        { section: 'injuries', status: 403 },
        { section: 'employer details', status: 500 },
      ])!;
      expect(msg).toContain('could not be loaded');
      expect(msg).toContain('retry');
      expect(msg).not.toContain('do not have permission');
    });

    it('treats a status-less failure as retryable rather than as a refusal', () => {
      const msg = sourcePrefillWarning([{ section: 'injuries' }])!;
      expect(msg).toContain('could not be loaded');
    });

    it('joins two sections with "and"', () => {
      const msg = sourcePrefillWarning([
        { section: 'employer details', status: 500 },
        { section: 'injuries', status: 500 },
      ])!;
      expect(msg).toContain('employer details and injuries');
    });

    it('joins three or more sections with commas and a final "and"', () => {
      const msg = sourcePrefillWarning([
        { section: 'employer details', status: 500 },
        { section: 'applicant attorney', status: 500 },
        { section: 'injuries', status: 500 },
      ])!;
      expect(msg).toContain('employer details, applicant attorney and injuries');
    });
  });

  describe('sourceLoadFailureMessage', () => {
    it('sends a refused booker to a supervisor instead of back to the number', () => {
      const msg = sourceLoadFailureMessage(httpError(403));
      expect(msg).toContain('do not have permission');
      // The old single message said "check the confirmation number" for every failure. For a
      // 403 the number is correct and re-reading it is wasted effort.
      expect(msg).not.toContain('confirmation number');
    });

    it('reuses the not-found wording for a 404, matching the in-band empty-response branch', () => {
      expect(sourceLoadFailureMessage(httpError(404))).toBe(NO_SOURCE_FOUND_MESSAGE);
    });

    it('asks for a retry on a server error', () => {
      const msg = sourceLoadFailureMessage(httpError(500));
      expect(msg).toContain('try again');
      expect(msg).not.toContain('confirmation number');
    });

    it('asks for a retry when there is no status at all', () => {
      expect(sourceLoadFailureMessage(new Error('offline'))).toContain('try again');
    });
  });

  describe('patientLoadFailureMessage', () => {
    it('points a refused booker at the manual form', () => {
      const msg = patientLoadFailureMessage(httpError(403));
      expect(msg).toContain('do not have permission');
      expect(msg).toContain('form');
    });

    it('offers both a retry and the manual form for a transient failure', () => {
      const msg = patientLoadFailureMessage(httpError(500));
      expect(msg).toContain('try again');
      expect(msg).toContain('form');
    });
  });

  describe('exported copy', () => {
    it('is plain ASCII, which the repo requires of all source', () => {
      const all = [
        ADDRESS_CHECK_SKIPPED_MESSAGE,
        NO_SOURCE_FOUND_MESSAGE,
        SOURCE_LOADED_MESSAGE,
        sourceLoadFailureMessage(httpError(403)),
        sourcePrefillWarning([{ section: 'injuries', status: 403 }])!,
      ].join(' ');
      expect(all).toMatch(/^[\x20-\x7e]+$/);
    });

    it('tells the booker their address went in as typed, not that the booking failed', () => {
      expect(ADDRESS_CHECK_SKIPPED_MESSAGE).toContain('exactly as typed');
    });
  });
});
