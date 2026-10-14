import {
  canApproveReschedule,
  canConfirmDate,
  canFinalizeReschedule,
  consentStatusLabel,
  effectiveRescheduleOutcome,
  formatSlotLabel,
  requiresAdminReason,
  rescheduleModalTitle,
  rescheduleStage,
  rowActionIsFinal,
  rowActionLabel,
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

    // A declined side kills the round even while the OTHER side is still thinking: resending
    // would only re-ask the pending side, and the round stays unfinalizable however they answer.
    it('sends a declined round back to needing a date even if the other side has not answered', () => {
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: PENDING,
          currentRoundSideBStatus: REJECTED,
        }),
      ).toBe('needs-date');
      expect(
        rescheduleStage({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: PENDING,
          currentRoundSideBStatus: EXPIRED,
        }),
      ).toBe('needs-date');
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
      expect(canConfirmDate({ slotId: CHOSEN, time: '09:30', outcome: 7 })).toBe(true);
      expect(canConfirmDate({ slotId: CHOSEN, time: null, outcome: 7 })).toBe(false);
      expect(canConfirmDate({ slotId: null, time: '09:30', outcome: 7 })).toBe(false);
    });

    // Confirming is what emails both sides, so a missing explanation must make the button
    // unavailable rather than let it fire and refuse.
    it('also needs the admin reason when staff replace a slot the requestor proposed', () => {
      expect(
        canConfirmDate({
          slotId: CHOSEN,
          time: '09:30',
          outcome: 7,
          proposedSlotId: PROPOSED,
          adminReason: '',
        }),
      ).toBe(false);
      expect(
        canConfirmDate({
          slotId: CHOSEN,
          time: '09:30',
          outcome: 7,
          proposedSlotId: PROPOSED,
          adminReason: '   ',
        }),
      ).toBe(false);
      expect(
        canConfirmDate({
          slotId: CHOSEN,
          time: '09:30',
          outcome: 7,
          proposedSlotId: PROPOSED,
          adminReason: 'Doctor unavailable',
        }),
      ).toBe(true);
    });

    it('does not demand a reason for a first-and-only staff choice', () => {
      expect(
        canConfirmDate({
          slotId: CHOSEN,
          time: '09:30',
          outcome: 7,
          proposedSlotId: null,
          adminReason: '',
        }),
      ).toBe(true);
    });
  });

  describe('canConfirmDate outcome', () => {
    // 2026-10-09: the reschedule completes on its own once both sides agree, so the No bill / Late
    // choice is made when staff SET the date and the button must not fire without it.
    it('stays blocked until a No bill / Late outcome is chosen', () => {
      expect(canConfirmDate({ slotId: CHOSEN, time: '09:30', outcome: null })).toBe(false);
      expect(canConfirmDate({ slotId: CHOSEN, time: '09:30', outcome: undefined })).toBe(false);
      expect(canConfirmDate({ slotId: CHOSEN, time: '09:30', outcome: 8 })).toBe(true);
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

  /**
   * Item K (2026-08-22) -- the inbox row button said "Approve" at every stage, but on a reschedule
   * the first click only sets a date and asks both parties for consent. These pin the wording so it
   * cannot silently revert to describing step 3 of 3 at step 1 of 3.
   */
  describe('rowActionLabel', () => {
    it('says Set date before a date is chosen -- that is all the click does', () => {
      expect(rowActionLabel(true, 'needs-date')).toBe('Set date');
    });

    it('says Awaiting consent while both sides are being asked, because staff cannot approve yet', () => {
      expect(rowActionLabel(true, 'awaiting-consent')).toBe('Awaiting consent');
    });

    it('says Approve only once consent is granted, which is the click that reschedules', () => {
      expect(rowActionLabel(true, 'granted')).toBe('Approve');
    });

    it('says Approve for a cancellation whose consent is not outstanding', () => {
      expect(rowActionLabel(false, 'needs-date')).toBe('Approve');
      expect(rowActionLabel(false, 'awaiting-consent')).toBe('Approve');
      expect(rowActionLabel(false, 'granted')).toBe('Approve');
      expect(rowActionLabel(false, 'needs-date', false)).toBe('Approve');
    });

    // Q11B (2026-10-09): the server refuses an early cancellation approve, so the row must not
    // offer one, matching what the reschedule row already does.
    it('says Awaiting consent for a cancellation while consent is still outstanding', () => {
      expect(rowActionLabel(false, 'needs-date', true)).toBe('Awaiting consent');
    });

    it('ignores the cancellation flag for a reschedule, which is stage-driven', () => {
      expect(rowActionLabel(true, 'granted', true)).toBe('Approve');
    });
  });

  describe('rowActionIsFinal', () => {
    it('is false at the stages that are not approvals, so they do not look green and final', () => {
      expect(rowActionIsFinal(true, 'needs-date')).toBeFalse();
      expect(rowActionIsFinal(true, 'awaiting-consent')).toBeFalse();
    });

    it('is true at the genuine approve step', () => {
      expect(rowActionIsFinal(true, 'granted')).toBeTrue();
    });

    it('is true for a cancellation whose consent is not outstanding', () => {
      expect(rowActionIsFinal(false, 'needs-date')).toBeTrue();
    });

    it('is false for a cancellation still awaiting consent (Q11B)', () => {
      expect(rowActionIsFinal(false, 'needs-date', true)).toBeFalse();
    });
  });

  describe('formatSlotLabel', () => {
    // A slot date is a CALENDAR date: the label must be the day that was written, whatever zone
    // designator the value arrives with. Until 2026-10-14 the server sent it as UTC midnight
    // ('...T00:00:00Z'), and `new Date()` made a Pacific viewer read the day before -- the
    // approve modal said "Oct 19" for a reschedule staff had set to Oct 20.
    const DAY = '2026-08-27T00:00:00';

    it('labels the written day when the date arrives as UTC midnight', () => {
      expect(formatSlotLabel('2026-08-27T00:00:00Z', '10:30')).toBe('Aug 27, 2026 at 10:30');
    });

    it('labels the written day whatever offset the date carries', () => {
      // +14:00 is the one that fails in EVERY zone, CI's UTC included: read as an instant it is
      // 10:00 UTC on the 26th. The Z case above only fails west of Greenwich.
      expect(formatSlotLabel('2026-08-27T00:00:00+14:00', '10:30')).toBe('Aug 27, 2026 at 10:30');
    });

    it('labels the written day for a bare date', () => {
      // The ECMAScript spec parses a bare date as UTC midnight, the same trap as the Z.
      expect(formatSlotLabel('2026-08-27', null)).toBe('Aug 27, 2026');
    });

    it('labels a slot with its date and start time', () => {
      expect(formatSlotLabel(DAY, '10:30')).toBe('Aug 27, 2026 at 10:30');
    });

    it('labels a slot with its date alone when there is no start time', () => {
      expect(formatSlotLabel(DAY, null)).toBe('Aug 27, 2026');
    });

    it('gives no label for an unparseable date', () => {
      // Positive control: the tests above show a parseable date DOES label.
      expect(formatSlotLabel('not-a-date', '10:30')).toBeNull();
    });
  });

  describe('rescheduleModalTitle', () => {
    it('names the step the reschedule is really on', () => {
      expect(rescheduleModalTitle('needs-date')).toBe('Set reschedule date');
      expect(rescheduleModalTitle('awaiting-consent')).toBe('Reschedule awaiting consent');
      expect(rescheduleModalTitle('granted')).toBe('Complete reschedule');
    });
  });

  describe('effectiveRescheduleOutcome', () => {
    it('uses the outcome chosen at Set date over the modal pick', () => {
      // The fallback Approve used to send the modal's No bill default even when Late was chosen.
      expect(effectiveRescheduleOutcome(8, 7)).toBe(8);
    });

    it('falls back to the modal pick on a round that carries no outcome', () => {
      expect(effectiveRescheduleOutcome(null, 7)).toBe(7);
      expect(effectiveRescheduleOutcome(undefined, 8)).toBe(8);
    });

    it('is null when neither is set', () => {
      expect(effectiveRescheduleOutcome(null, null)).toBeNull();
    });
  });

  describe('consentStatusLabel', () => {
    it('labels a consent that was not needed', () => {
      expect(consentStatusLabel(0)).toBe('Not needed');
    });

    it('labels a consent still awaiting a reply', () => {
      expect(consentStatusLabel(1)).toBe('Awaiting reply');
    });

    it('labels an agreed consent', () => {
      expect(consentStatusLabel(2)).toBe('Agreed');
    });

    it('labels a declined consent', () => {
      expect(consentStatusLabel(3)).toBe('Declined');
    });

    it('labels an expired consent', () => {
      expect(consentStatusLabel(4)).toBe('Expired');
    });

    it('labels a consent round that never asked', () => {
      expect(consentStatusLabel(null)).toBe('Not asked');
      expect(consentStatusLabel(undefined)).toBe('Not asked');
    });
  });
});
