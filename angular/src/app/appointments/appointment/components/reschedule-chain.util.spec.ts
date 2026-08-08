import type { RescheduleChainDto } from '../../../proxy/appointments/models';
import {
  hasRescheduleSource,
  rescheduleChainSteps,
  rescheduleSourceLabel,
} from './reschedule-chain.util';

/**
 * Phase 4d (2026-08-05) -- the "rescheduled from" block's derivation.
 *
 * All fixture data is synthetic.
 */
describe('reschedule-chain.util', () => {
  const fullChain: RescheduleChainDto = {
    sourceAppointmentId: '11111111-2222-4333-8444-555555555555',
    sourceRequestConfirmationNumber: 'A00036',
    sideAAgreedAt: '2026-08-16T17:05:00Z',
    sideBAgreedAt: '2026-08-17T21:40:00Z',
    decidedAt: '2026-08-18T16:00:00Z',
  };

  describe('hasRescheduleSource', () => {
    it('is false for an appointment that was booked normally', () => {
      expect(hasRescheduleSource(null)).toBe(false);
      expect(hasRescheduleSource(undefined)).toBe(false);
    });

    it('is true once a source appointment is present', () => {
      expect(hasRescheduleSource(fullChain)).toBe(true);
    });
  });

  describe('rescheduleSourceLabel', () => {
    it('returns no label without a chain', () => {
      expect(rescheduleSourceLabel(null)).toBeNull();
    });

    it('returns the source confirmation number', () => {
      expect(rescheduleSourceLabel(fullChain)).toBe('A00036');
    });

    it('returns null rather than a raw id when the number is missing', () => {
      // A Guid on screen is noise no reader can act on, so the block renders nothing at all.
      expect(
        rescheduleSourceLabel({ ...fullChain, sourceRequestConfirmationNumber: null }),
      ).toBeNull();
      expect(
        rescheduleSourceLabel({ ...fullChain, sourceRequestConfirmationNumber: '   ' }),
      ).toBeNull();
    });
  });

  describe('rescheduleChainSteps', () => {
    it('returns nothing without a chain', () => {
      expect(rescheduleChainSteps(null)).toEqual([]);
    });

    it('returns the three steps in the order they happen', () => {
      expect(rescheduleChainSteps(fullChain)).toEqual([
        { kind: 'side-a-agreed', at: '2026-08-16T17:05:00Z' },
        { kind: 'side-b-agreed', at: '2026-08-17T21:40:00Z' },
        { kind: 'decided', at: '2026-08-18T16:00:00Z' },
      ]);
    });

    it('omits a side that was never asked instead of showing it empty', () => {
      // An internal-staff reschedule solicits only the sides that exist, so a missing Side B is
      // normal -- printing a blank row for it would read as a fault.
      const oneSided: RescheduleChainDto = { ...fullChain, sideBAgreedAt: null };

      expect(rescheduleChainSteps(oneSided)).toEqual([
        { kind: 'side-a-agreed', at: '2026-08-16T17:05:00Z' },
        { kind: 'decided', at: '2026-08-18T16:00:00Z' },
      ]);
    });

    it('still reports the decision when consent predates the round columns', () => {
      // A request decided before 4c stored consent per round has neither side recorded, but it was
      // still decided at a knowable moment.
      const decidedOnly: RescheduleChainDto = {
        ...fullChain,
        sideAAgreedAt: null,
        sideBAgreedAt: null,
      };

      expect(rescheduleChainSteps(decidedOnly)).toEqual([
        { kind: 'decided', at: '2026-08-18T16:00:00Z' },
      ]);
    });

    it('returns nothing when the chain exists but no step was recorded', () => {
      const bare: RescheduleChainDto = {
        sourceAppointmentId: fullChain.sourceAppointmentId,
        sourceRequestConfirmationNumber: 'A00037',
      };

      expect(rescheduleChainSteps(bare)).toEqual([]);
    });
  });
});
