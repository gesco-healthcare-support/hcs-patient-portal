import {
  ENTER_PRACTICE_FALLBACK_MESSAGE,
  enterPracticeErrorMessage,
} from './enter-practice-error.util';

describe('enterPracticeErrorMessage', () => {
  it("shows the grant's own reason", () => {
    const err = {
      status: 400,
      error: { error: 'invalid_grant', error_description: 'You are not assigned to this office.' },
    };
    expect(enterPracticeErrorMessage(err)).toBe('You are not assigned to this office.');
  });

  it('falls back to a general line when there is no reason', () => {
    expect(enterPracticeErrorMessage({ status: 0 })).toBe(ENTER_PRACTICE_FALLBACK_MESSAGE);
    expect(enterPracticeErrorMessage({ error: { error_description: '   ' } })).toBe(
      ENTER_PRACTICE_FALLBACK_MESSAGE,
    );
    expect(enterPracticeErrorMessage({ error: { error_description: 42 } })).toBe(
      ENTER_PRACTICE_FALLBACK_MESSAGE,
    );
    expect(enterPracticeErrorMessage(null)).toBe(ENTER_PRACTICE_FALLBACK_MESSAGE);
    expect(enterPracticeErrorMessage(undefined)).toBe(ENTER_PRACTICE_FALLBACK_MESSAGE);
  });
});
