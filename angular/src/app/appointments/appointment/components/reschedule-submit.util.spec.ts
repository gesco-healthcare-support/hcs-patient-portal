import { canSubmitReschedule, rescheduleModalOptions } from './reschedule-submit.util';

/**
 * Phase 4b (2026-08-04): the reschedule request modal is role-split. Internal staff filing a
 * reschedule pick a date with the availability calendar; external requestors submit a reason
 * only, because staff choose the date at approval. These pin both branches.
 */
describe('reschedule-submit.util', () => {
  const REASON = 'Patient is travelling that week';

  describe('external requestor (reason only)', () => {
    const external = { requesterIsStaff: false, slotId: null, time: null };

    it('submits on a reason alone -- no date is asked for', () => {
      expect(canSubmitReschedule({ ...external, reason: REASON })).toBe(true);
    });

    it('blocks on an empty or whitespace reason', () => {
      expect(canSubmitReschedule({ ...external, reason: '' })).toBe(false);
      expect(canSubmitReschedule({ ...external, reason: '   ' })).toBe(false);
    });

    it('blocks a reason longer than the limit', () => {
      expect(
        canSubmitReschedule({ ...external, reason: 'x'.repeat(501), maxReasonLength: 500 }),
      ).toBe(false);
    });
  });

  describe('rescheduleModalOptions', () => {
    // REGRESSION: a getter returning a fresh object literal here is bound to abp-modal's SIGNAL
    // input `options`. A new identity every change-detection pass re-dirties the view and
    // re-runs change detection forever, hanging the tab -- silently, because the served bundle
    // is a production build with Angular's infinite-CD guard compiled out.
    it('returns the SAME reference across calls for each role', () => {
      expect(rescheduleModalOptions(true)).toBe(rescheduleModalOptions(true));
      expect(rescheduleModalOptions(false)).toBe(rescheduleModalOptions(false));
    });

    it('widens the dialog for staff only', () => {
      // ABP's ModalComponent defaults to size 'md' (500px), which clips the two-month picker.
      expect(rescheduleModalOptions(true)).toEqual({ size: 'lg' });
      expect(rescheduleModalOptions(false)).toEqual({});
    });
  });

  describe('internal staff filer (calendar)', () => {
    const staff = { requesterIsStaff: true };

    it('blocks until both a date and a time are picked', () => {
      expect(canSubmitReschedule({ ...staff, slotId: null, time: null, reason: REASON })).toBe(
        false,
      );
      expect(canSubmitReschedule({ ...staff, slotId: 'slot-1', time: null, reason: REASON })).toBe(
        false,
      );
      expect(canSubmitReschedule({ ...staff, slotId: null, time: '09:30', reason: REASON })).toBe(
        false,
      );
    });

    it('submits once date, time and reason are all present', () => {
      expect(
        canSubmitReschedule({ ...staff, slotId: 'slot-1', time: '09:30', reason: REASON }),
      ).toBe(true);
    });

    it('still requires the reason', () => {
      expect(canSubmitReschedule({ ...staff, slotId: 'slot-1', time: '09:30', reason: '' })).toBe(
        false,
      );
    });
  });
});
