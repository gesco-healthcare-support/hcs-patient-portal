import { downstreamSaveFailureMessage } from './save-failure-message.util';

/**
 * 2026-08-06 -- the partial-save message on the appointment edit form.
 *
 * All fixture data is synthetic.
 */
describe('downstreamSaveFailureMessage', () => {
  it('returns nothing when there was no failure', () => {
    expect(downstreamSaveFailureMessage(null)).toBeNull();
    expect(downstreamSaveFailureMessage(undefined)).toBeNull();
  });

  describe('on a permission failure', () => {
    const message = downstreamSaveFailureMessage({ status: 403 })!;

    it('says it is a permission problem, not a fault', () => {
      // Retrying can never succeed, so the wording must not invite a retry.
      expect(message).toContain('do not have permission');
      expect(message).not.toContain('try again');
    });

    it('names the employer section specifically', () => {
      // The old message said "employer / attorney". The attorney upserts run under a bare
      // [Authorize] and cannot 403, so naming them here sent people looking in the wrong place.
      expect(message).toContain('employer');
      expect(message).not.toContain('attorney');
    });

    it('still confirms what WAS saved', () => {
      // The appointment and patient writes committed before the failure; omitting that would make
      // the user redo work that already persisted.
      expect(message).toContain('were saved');
    });
  });

  describe('on any other failure', () => {
    const message = downstreamSaveFailureMessage({ status: 500 })!;

    it('does not claim a permission problem', () => {
      expect(message).not.toContain('permission');
    });

    it('invites a retry, because one might work', () => {
      expect(message).toContain('try again');
    });

    it('still confirms what WAS saved', () => {
      expect(message).toContain('were saved');
    });
  });

  it('treats a missing status as a generic failure rather than a permission one', () => {
    // A network drop or a CORS failure arrives with no status; guessing "forbidden" there would
    // tell the user to go find a supervisor for a problem a retry would fix.
    const message = downstreamSaveFailureMessage({})!;

    expect(message).not.toContain('permission');
    expect(message).toContain('try again');
  });
});
