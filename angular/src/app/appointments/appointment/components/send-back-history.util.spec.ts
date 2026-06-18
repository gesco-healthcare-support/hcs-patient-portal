import {
  changedRows,
  fieldLabel,
  fixedSummary,
  flaggedSummary,
  latestRound,
  notePreview,
  wasResubmitted,
} from './send-back-history.util';
import type { AppointmentInfoRequestRoundDto } from '../../../proxy/appointment-info-requests/models';

function round(partial: Partial<AppointmentInfoRequestRoundDto>): AppointmentInfoRequestRoundDto {
  return {
    id: 'r',
    roundNumber: 1,
    note: '',
    isResolved: false,
    flaggedCount: 0,
    fixedCount: 0,
    diffs: [],
    ...partial,
  };
}

describe('send-back-history.util', () => {
  describe('fieldLabel', () => {
    it('maps a known key to its registry label', () => {
      expect(fieldLabel('dateOfBirth')).toBe('Date of birth');
    });
    it('falls back to the raw key when unknown', () => {
      expect(fieldLabel('mystery')).toBe('mystery');
    });
    it('returns empty string for null', () => {
      expect(fieldLabel(null)).toBe('');
    });
  });

  describe('latestRound / wasResubmitted', () => {
    it('returns the first (newest) round', () => {
      const rounds = [round({ id: 'a' }), round({ id: 'b' })];
      expect(latestRound(rounds)?.id).toBe('a');
    });
    it('returns null for empty or nullish', () => {
      expect(latestRound([])).toBeNull();
      expect(latestRound(null)).toBeNull();
    });
    it('is resubmitted only when the newest round is resolved', () => {
      expect(wasResubmitted([round({ isResolved: true })])).toBeTrue();
      expect(wasResubmitted([round({ isResolved: false })])).toBeFalse();
      expect(wasResubmitted([])).toBeFalse();
    });
  });

  describe('changedRows', () => {
    it('keeps only changed diffs and maps label/old/new', () => {
      const r = round({
        diffs: [
          { key: 'dateOfBirth', oldValue: 'a', newValue: 'b', changed: true },
          { key: 'address', oldValue: 'x', newValue: 'x', changed: false },
        ],
      });

      const rows = changedRows(r);

      expect(rows.length).toBe(1);
      expect(rows[0].label).toBe('Date of birth');
      expect(rows[0].oldValue).toBe('a');
      expect(rows[0].newValue).toBe('b');
    });
    it('returns empty for a null round', () => {
      expect(changedRows(null).length).toBe(0);
    });
  });

  describe('summaries', () => {
    it('fixedSummary pluralizes the flagged count', () => {
      expect(fixedSummary(round({ fixedCount: 2, flaggedCount: 3 }))).toBe(
        '2 of 3 flagged items fixed',
      );
      expect(fixedSummary(round({ fixedCount: 1, flaggedCount: 1 }))).toBe(
        '1 of 1 flagged item fixed',
      );
    });
    it('flaggedSummary pluralizes', () => {
      expect(flaggedSummary(round({ flaggedCount: 1 }))).toBe('1 field flagged');
      expect(flaggedSummary(round({ flaggedCount: 2 }))).toBe('2 fields flagged');
    });
  });

  describe('notePreview', () => {
    it('returns a short note unchanged', () => {
      expect(notePreview('hello')).toBe('hello');
    });
    it('truncates a long note with an ellipsis', () => {
      const long = 'x'.repeat(100);
      expect(notePreview(long).length).toBe(83);
      expect(notePreview(long).endsWith('...')).toBeTrue();
    });
    it('handles null and undefined', () => {
      expect(notePreview(null)).toBe('');
      expect(notePreview(undefined)).toBe('');
    });
  });
});
