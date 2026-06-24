import { bannerVariant, detailActions, statusLabel } from './internal-detail.util';

describe('internal-detail.util', () => {
  describe('detailActions', () => {
    it('offers the full office set on Pending (incl. request-info)', () => {
      const a = detailActions('Pending');
      expect(a).toContain('approve');
      expect(a).toContain('reject');
      expect(a).toContain('reschedule');
      expect(a).toContain('cancel');
      expect(a).toContain('requestInfo');
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
});
