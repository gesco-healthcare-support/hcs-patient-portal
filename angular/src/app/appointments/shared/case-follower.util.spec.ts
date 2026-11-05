import { bookerFollowsDisplayed, followerNote, resolveFollowerFields } from './case-follower.util';

describe('case-follower.util', () => {
  describe('resolveFollowerFields', () => {
    it('sends the booker choice when the option is offered', () => {
      expect(
        resolveFollowerFields({
          showBookerOption: true,
          bookerFollows: true,
          insteadTicked: false,
          insteadValid: false,
        }),
      ).toEqual({ bookerFollowsCase: true });
      expect(
        resolveFollowerFields({
          showBookerOption: true,
          bookerFollows: false,
          insteadTicked: false,
          insteadValid: false,
        }),
      ).toEqual({ bookerFollowsCase: false });
    });

    it('sends nothing for a booker who is not offered the option (staff, patient)', () => {
      expect(
        resolveFollowerFields({
          showBookerOption: false,
          bookerFollows: true,
          insteadTicked: false,
          insteadValid: false,
        }),
      ).toEqual({});
    });

    it('"instead" sends the redirect and forces the booker off, whatever the box says', () => {
      for (const showBookerOption of [true, false]) {
        expect(
          resolveFollowerFields({
            showBookerOption,
            bookerFollows: true,
            insteadTicked: true,
            insteadValid: true,
          }),
        ).toEqual({ bookerFollowsCase: false, sendAttorneyMailToParalegal: true });
      }
    });

    it('a ticked "instead" that cannot apply still never sends the stored booker choice', () => {
      expect(
        resolveFollowerFields({
          showBookerOption: true,
          bookerFollows: true,
          insteadTicked: true,
          insteadValid: false,
        }),
      ).toEqual({ bookerFollowsCase: false });
    });
  });

  it('shows the booker box unticked while "instead" is on, else as chosen', () => {
    expect(bookerFollowsDisplayed(true, true)).toBeFalse();
    expect(bookerFollowsDisplayed(true, false)).toBeTrue();
    expect(bookerFollowsDisplayed(false, false)).toBeFalse();
  });

  describe('followerNote', () => {
    it('is null for a normal case or a DTO without the fields', () => {
      expect(followerNote(null)).toBeNull();
      expect(followerNote({})).toBeNull();
      expect(followerNote({ bookerFollowsCase: true, attorneyMailToParalegal: false })).toBeNull();
    });

    it('explains an opted-out booker', () => {
      expect(followerNote({ bookerFollowsCase: false })).toContain('only the request receipt');
    });

    it('explains the redirect, which wins over the booker note', () => {
      const note = followerNote({ bookerFollowsCase: false, attorneyMailToParalegal: true });
      expect(note).toContain('go to the paralegal instead');
    });
  });
});
