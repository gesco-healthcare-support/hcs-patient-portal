import {
  bannerVariant,
  detailActions,
  resolveBookerEmail,
  statusLabel,
} from './internal-detail.util';

describe('internal-detail.util', () => {
  describe('detailActions', () => {
    it('offers approve/reject/reschedule/request-info on Pending', () => {
      const a = detailActions('Pending');
      expect(a).toContain('approve');
      expect(a).toContain('reject');
      expect(a).toContain('reschedule');
      expect(a).toContain('requestInfo');
    });

    // 2026-07-16 triage (issue #2): Cancel is NO LONGER offered on Pending -- it
    // duplicated Reject (both send a not-yet-approved appointment back), so it was
    // dropped for internal staff. Supersedes B1/C3 (2026-07-01), which had added
    // Cancel on Pending. The backend precondition + consent flow are unchanged;
    // only the Pending button is removed. Cancel stays on Approved/Rescheduled.
    it('does NOT offer cancel on Pending (Reject already covers it)', () => {
      const a = detailActions('Pending');
      expect(a).not.toContain('cancel');
      expect(a).toContain('reject');
    });

    it('offers only reschedule + cancel on Approved and Rescheduled', () => {
      expect(detailActions('Approved')).toEqual(['reschedule', 'cancel']);
      expect(detailActions('Rescheduled')).toEqual(['reschedule', 'cancel']);
    });

    it('offers no office actions on terminal / awaiting pills', () => {
      expect(detailActions('Rejected')).toEqual([]);
      expect(detailActions('Cancelled')).toEqual([]);
      expect(detailActions('InfoRequested')).toEqual([]);
    });

    // Phase 4c (2026-08-05): stacking a second change request on one already awaiting consent
    // has no coherent meaning. This REMOVES two buttons that render on a reschedule-requested
    // appointment today -- intentional, and the change most likely to read as "something
    // disappeared".
    it('offers no office actions while a change request is in flight', () => {
      expect(detailActions('RescheduleRequested')).toEqual([]);
      expect(detailActions('CancellationRequested')).toEqual([]);
    });

    it('never offers approve/reject outside Pending', () => {
      for (const pill of [
        'Approved',
        'Rescheduled',
        'RescheduleRequested',
        'Rejected',
        'Cancelled',
        'CancellationRequested',
        'InfoRequested',
      ] as const) {
        expect(detailActions(pill)).not.toContain('approve');
        expect(detailActions(pill)).not.toContain('reject');
      }
    });
  });

  describe('bannerVariant', () => {
    it('lowercases the pill and hyphenates InfoRequested', () => {
      expect(bannerVariant('Pending')).toBe('pending');
      expect(bannerVariant('Approved')).toBe('approved');
      expect(bannerVariant('InfoRequested')).toBe('info-requested');
    });

    // A bare toLowerCase() yields 'reschedulerequested', which matches no CALLOUTS key and
    // silently falls back to the generic pending copy -- the failure mode is invisible.
    it('hyphenates the multi-word in-flight pills', () => {
      expect(bannerVariant('RescheduleRequested')).toBe('reschedule-requested');
      expect(bannerVariant('CancellationRequested')).toBe('cancellation-requested');
    });
  });

  describe('statusLabel', () => {
    it('humanizes InfoRequested and passes the rest through', () => {
      expect(statusLabel('InfoRequested')).toBe('Info requested');
      expect(statusLabel('Approved')).toBe('Approved');
    });

    it('humanizes the multi-word in-flight pills instead of running the words together', () => {
      expect(statusLabel('RescheduleRequested')).toBe('Reschedule requested');
      expect(statusLabel('CancellationRequested')).toBe('Cancellation requested');
    });
  });

  // F-011 regression: "Booker (identity)" shows the ACTUAL booker, not the
  // responsible user/approver (which the identity user resolves to once set).
  describe('resolveBookerEmail (F-011)', () => {
    it('prefers the actual booker over the identity user', () => {
      const email = resolveBookerEmail({
        bookedByUser: { email: 'booker@example.test', userName: 'booker' },
        identityUser: { email: 'patient@example.test', userName: 'patient' },
      });
      expect(email).toBe('booker@example.test');
    });

    it('falls back to the identity user only when there is no booker (legacy rows)', () => {
      expect(
        resolveBookerEmail({
          bookedByUser: null,
          identityUser: { email: 'patient@example.test', userName: 'patient' },
        }),
      ).toBe('patient@example.test');
    });

    it('falls back to userName, then empty string', () => {
      expect(resolveBookerEmail({ bookedByUser: { userName: 'booker' } })).toBe('booker');
      expect(resolveBookerEmail(null)).toBe('');
    });
  });
});
