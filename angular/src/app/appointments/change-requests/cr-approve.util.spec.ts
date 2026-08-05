import {
  canApproveReschedule,
  canConfirmDate,
  canFinalizeReschedule,
  requiresAdminReason,
  rescheduleStage,
} from './cr-approve.util';

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

  /**
   * Phase 4c (2026-08-05): the approve modal became three steps -- pick, confirm (which opens a
   * consent round and emails both sides), finalize. These pin the stage derivation, which is
   * what decides which controls render and whether Finalize is live.
   */
  describe('rescheduleStage', () => {
    const NOT_REQUIRED = 0;
    const PENDING = 1;
    const APPROVED = 2;
    const REJECTED = 3;
    const EXPIRED = 4;

    it('reads a row with no round as still needing a date', () => {
      expect(rescheduleStage(null)).toBe('needs-date');
      expect(rescheduleStage({})).toBe('needs-date');
      expect(rescheduleStage({ currentConsentRoundNumber: null })).toBe('needs-date');
    });

    it('reads a round with a side still pending as awaiting consent', () => {
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: APPROVED,
          currentRoundSideBStatus: PENDING,
        }),
      ).toBe('awaiting-consent');
    });

    it('reads a round whose solicited sides all approved as granted', () => {
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 2,
          currentRoundSideAStatus: APPROVED,
          currentRoundSideBStatus: APPROVED,
        }),
      ).toBe('granted');
    });

    it('treats a side with no representative as satisfied, matching the server gate', () => {
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: APPROVED,
          currentRoundSideBStatus: NOT_REQUIRED,
        }),
      ).toBe('granted');
    });

    // A declined date is not a dead end: the way forward is to propose a different one, which
    // supersedes that round and opens the next.
    it('sends a rejected or expired round back to needing a date', () => {
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: APPROVED,
          currentRoundSideBStatus: REJECTED,
        }),
      ).toBe('needs-date');
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: EXPIRED,
          currentRoundSideBStatus: NOT_REQUIRED,
        }),
      ).toBe('needs-date');
    });
  });

  describe('canConfirmDate', () => {
    it('needs both a date and a time, because one date carries many slots', () => {
      expect(canConfirmDate({ slotId: CHOSEN, time: '09:30' })).toBe(true);
      expect(canConfirmDate({ slotId: CHOSEN, time: null })).toBe(false);
      expect(canConfirmDate({ slotId: null, time: '09:30' })).toBe(false);
    });
  });

  describe('canFinalizeReschedule', () => {
    it('stays blocked until the round is granted', () => {
      expect(canFinalizeReschedule({ stage: 'needs-date', outcome: 21 })).toBe(false);
      expect(canFinalizeReschedule({ stage: 'awaiting-consent', outcome: 21 })).toBe(false);
    });

    it('still needs a billing outcome once the round is granted', () => {
      expect(canFinalizeReschedule({ stage: 'granted', outcome: null })).toBe(false);
      expect(canFinalizeReschedule({ stage: 'granted', outcome: 21 })).toBe(true);
    });
  });
});
