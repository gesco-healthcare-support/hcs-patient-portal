import { buildSendBackInput, canSendBack } from './request-info-modal.util';

describe('request-info-modal.util', () => {
  describe('canSendBack', () => {
    it('is false with no fields selected', () => {
      expect(canSendBack(0, 'a note')).toBe(false);
    });

    it('is false with a blank note', () => {
      expect(canSendBack(2, '   ')).toBe(false);
    });

    it('is true with at least one field and a non-blank note', () => {
      expect(canSendBack(1, 'fix this')).toBe(true);
    });
  });

  describe('buildSendBackInput', () => {
    it('trims the note', () => {
      expect(buildSendBackInput(['dateOfBirth'], {}, '  note  ').note).toBe('note');
    });

    it('emits one flagged field per key, trimming hints and nulling empties', () => {
      const input = buildSendBackInput(
        ['dateOfBirth', 'socialSecurityNumber'],
        { dateOfBirth: '  bad dob  ', socialSecurityNumber: '   ' },
        'note',
      );
      expect(input.flaggedFields).toEqual([
        { key: 'dateOfBirth', hint: 'bad dob' },
        { key: 'socialSecurityNumber', hint: null },
      ]);
    });

    it('defaults a missing hint to null', () => {
      const input = buildSendBackInput(['address'], {}, 'note');
      expect(input.flaggedFields).toEqual([{ key: 'address', hint: null }]);
    });
  });
});
