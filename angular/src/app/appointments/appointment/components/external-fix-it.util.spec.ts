import {
  allFixed,
  buildCorrectionsPayload,
  fixItProgress,
  isInlineEditable,
} from './external-fix-it.util';

describe('external-fix-it.util', () => {
  describe('isInlineEditable', () => {
    it('is true for form fields', () => {
      expect(isInlineEditable('socialSecurityNumber')).toBe(true);
      expect(isInlineEditable('appointmentInsuranceName')).toBe(true);
    });

    it('is false for documents (handled by upload)', () => {
      expect(isInlineEditable('documents')).toBe(false);
    });

    it('is false for an unknown key', () => {
      expect(isInlineEditable('panelNumber')).toBe(false);
    });
  });

  describe('fixItProgress', () => {
    it('counts addressed flagged fields', () => {
      const flagged = ['dateOfBirth', 'address', 'documents'];
      const touched = new Set(['dateOfBirth']);
      expect(fixItProgress(flagged, touched)).toEqual({ fixed: 1, total: 3 });
    });
  });

  describe('allFixed', () => {
    it('is false until every flagged field is addressed', () => {
      const flagged = ['dateOfBirth', 'documents'];
      expect(allFixed(flagged, new Set(['dateOfBirth']))).toBe(false);
      expect(allFixed(flagged, new Set(['dateOfBirth', 'documents']))).toBe(true);
    });

    it('is false when nothing is flagged', () => {
      expect(allFixed([], new Set())).toBe(false);
    });
  });

  describe('buildCorrectionsPayload', () => {
    it('maps flagged + edited fields to the payload, trimming values', () => {
      const payload = buildCorrectionsPayload(
        ['cellPhoneNumber', 'appointmentClaimExaminerEmail', 'appointmentInsuranceName'],
        {
          cellPhoneNumber: '  (213) 555-0148  ',
          appointmentClaimExaminerEmail: 'ce@example.test',
          appointmentInsuranceName: 'Acme Mutual',
        },
      );
      expect(payload).toEqual({
        cellPhoneNumber: '(213) 555-0148',
        claimExaminerEmail: 'ce@example.test',
        insuranceName: 'Acme Mutual',
      });
    });

    it('omits edits to fields that were not flagged', () => {
      const payload = buildCorrectionsPayload(['address'], {
        address: '128 W 4th St',
        cellPhoneNumber: '(213) 555-0148',
      });
      expect(payload).toEqual({ address: '128 W 4th St' });
    });

    it('omits blank edits and documents', () => {
      const payload = buildCorrectionsPayload(['address', 'documents'], {
        address: '   ',
        documents: 'whatever',
      });
      expect(payload).toEqual({});
    });
  });
});
