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

    // F-M04 (2026-06-25): Cancel is NOT offered on Pending -- the domain rejects
    // cancelling a Pending appointment (Reject is the terminal action), so the
    // action previously produced a 403 + a stuck dialog.
    it('does NOT offer cancel on Pending (reject is the valid action)', () => {
      expect(detailActions('Pending')).not.toContain('cancel');
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

    it('never offers approve/reject outside Pending', () => {
      for (const pill of [
        'Approved',
        'Rescheduled',
        'Rejected',
        'Cancelled',
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
  });

  describe('statusLabel', () => {
    it('humanizes InfoRequested and passes the rest through', () => {
      expect(statusLabel('InfoRequested')).toBe('Info requested');
      expect(statusLabel('Approved')).toBe('Approved');
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
