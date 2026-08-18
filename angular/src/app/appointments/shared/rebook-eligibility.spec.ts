import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';
import { RE_BOOK_ELIGIBLE_STATUSES, isReBookEligibleStatus } from './rebook-eligibility';

/**
 * Item 4 T1 (2026-08-18) -- the four statuses a re-book may start from.
 *
 * Two callers depend on this list agreeing with itself: the "Book again" button gate on
 * both appointment detail pages, and the confirmation-number lookup's client-side status
 * gate. Hand-written lists in two places would drift, and the drift would be silent --
 * a button that offers a flow the lookup then refuses.
 *
 * The list is also a mirror of a server rule (`CanCreateReBook`). The server stays
 * authoritative; these tests pin the client half so a divergence shows up here rather
 * than as a refused POST after the user has filled in a whole form.
 */
describe('rebook-eligibility', () => {
  it('accepts an appointment that was cancelled, no-showed or not seen', () => {
    expect(isReBookEligibleStatus(AppointmentStatusType.NoShow)).toBe(true);
    expect(isReBookEligibleStatus(AppointmentStatusType.NotSeen)).toBe(true);
    expect(isReBookEligibleStatus(AppointmentStatusType.CancelledNoBill)).toBe(true);
    expect(isReBookEligibleStatus(AppointmentStatusType.CancelledLate)).toBe(true);
  });

  it('refuses an appointment that happened, or has not resolved yet', () => {
    // Approved is the one most likely to be wrongly admitted: it is the reval source
    // status, and re-book sits right next to reval in the UI.
    expect(isReBookEligibleStatus(AppointmentStatusType.Approved)).toBe(false);
    expect(isReBookEligibleStatus(AppointmentStatusType.Pending)).toBe(false);
    expect(isReBookEligibleStatus(AppointmentStatusType.Rejected)).toBe(false);
    expect(isReBookEligibleStatus(AppointmentStatusType.CheckedOut)).toBe(false);
    expect(isReBookEligibleStatus(AppointmentStatusType.Billed)).toBe(false);
  });

  it('refuses a rescheduled appointment', () => {
    // A rescheduled appointment already HAS a successor on the replacement chain.
    // Re-booking one would give it a second, and the Case Tracker payload resolves that
    // chain with a predicate query that assumes at most one.
    expect(isReBookEligibleStatus(AppointmentStatusType.RescheduledNoBill)).toBe(false);
    expect(isReBookEligibleStatus(AppointmentStatusType.RescheduledLate)).toBe(false);
  });

  it('refuses a missing status rather than defaulting to eligible', () => {
    expect(isReBookEligibleStatus(undefined)).toBe(false);
  });

  it('exposes exactly the four eligible statuses', () => {
    // Pinned as a count + membership rather than a deep-equal on order, so reordering
    // the constant is not a test failure but adding a fifth status is.
    expect(RE_BOOK_ELIGIBLE_STATUSES.length).toBe(4);
    expect(RE_BOOK_ELIGIBLE_STATUSES).toContain(AppointmentStatusType.NoShow);
    expect(RE_BOOK_ELIGIBLE_STATUSES).toContain(AppointmentStatusType.NotSeen);
    expect(RE_BOOK_ELIGIBLE_STATUSES).toContain(AppointmentStatusType.CancelledNoBill);
    expect(RE_BOOK_ELIGIBLE_STATUSES).toContain(AppointmentStatusType.CancelledLate);
  });
});
