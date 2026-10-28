import { inviteEmailWasNotQueued } from './invite-email.util';

describe('inviteEmailWasNotQueued', () => {
  it('is true only when the server says the email was not queued', () => {
    expect(inviteEmailWasNotQueued({ emailQueued: false })).toBeTrue();
  });

  it('is false when the email was queued, or when the server does not say', () => {
    expect(inviteEmailWasNotQueued({ emailQueued: true })).toBeFalse();
    expect(inviteEmailWasNotQueued({})).toBeFalse();
    expect(inviteEmailWasNotQueued(null)).toBeFalse();
    expect(inviteEmailWasNotQueued(undefined)).toBeFalse();
  });
});
