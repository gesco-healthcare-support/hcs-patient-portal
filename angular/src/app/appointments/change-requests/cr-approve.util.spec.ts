import { canApproveReschedule, requiresAdminReason } from './cr-approve.util';

/**
 * Phase 4b (2026-08-04): staff choose the reschedule date at approval, so the approve modal
 * gained a calendar. These pin the two rules that decide when Approve is live and when an
 * admin reason is owed -- the difference between a first-and-only choice and a real override.
 */
describe('cr-approve.util', () => {
  const PROPOSED = 'slot-proposed';
  const CHOSEN = 'slot-chosen';

  describe('requiresAdminReason', () => {
    it('is false when the requestor proposed nothing (the normal external case)', () => {
      // Nobody is being overruled, so demanding a justification would be nonsense.
      expect(requiresAdminReason(null, CHOSEN)).toBe(false);
    });

    it('is false when staff accept the proposed slot', () => {
      expect(requiresAdminReason(PROPOSED, PROPOSED)).toBe(false);
    });

    it('is true when staff replace a slot the requestor proposed', () => {
      expect(requiresAdminReason(PROPOSED, CHOSEN)).toBe(true);
    });

    it('is false before staff have chosen anything', () => {
      expect(requiresAdminReason(PROPOSED, null)).toBe(false);
    });
  });

  describe('canApproveReschedule', () => {
    it('blocks until a date AND a time are chosen when nothing was proposed', () => {
      expect(
        canApproveReschedule({ proposedSlotId: null, chosenSlotId: null, chosenTime: null }),
      ).toBe(false);
      expect(
        canApproveReschedule({ proposedSlotId: null, chosenSlotId: CHOSEN, chosenTime: null }),
      ).toBe(false);
      expect(
        canApproveReschedule({ proposedSlotId: null, chosenSlotId: null, chosenTime: '09:30' }),
      ).toBe(false);
    });

    it('allows approval once a date and time are chosen, with no reason needed', () => {
      expect(
        canApproveReschedule({ proposedSlotId: null, chosenSlotId: CHOSEN, chosenTime: '09:30' }),
      ).toBe(true);
    });

    it('allows approving the proposed slot untouched', () => {
      // Staff open the modal, accept what was asked for, pick an outcome and approve.
      expect(
        canApproveReschedule({ proposedSlotId: PROPOSED, chosenSlotId: null, chosenTime: null }),
      ).toBe(true);
    });

    it('demands a non-empty admin reason when staff override a proposed slot', () => {
      expect(
        canApproveReschedule({
          proposedSlotId: PROPOSED,
          chosenSlotId: CHOSEN,
          chosenTime: '09:30',
          adminReason: '   ',
        }),
      ).toBe(false);

      expect(
        canApproveReschedule({
          proposedSlotId: PROPOSED,
          chosenSlotId: CHOSEN,
          chosenTime: '09:30',
          adminReason: 'Doctor unavailable that morning',
        }),
      ).toBe(true);
    });

    it('does not demand an admin reason for a first-and-only staff choice', () => {
      expect(
        canApproveReschedule({
          proposedSlotId: null,
          chosenSlotId: CHOSEN,
          chosenTime: '09:30',
          adminReason: '',
        }),
      ).toBe(true);
    });
  });
});
