import {
  allFixed,
  buildCorrectionsPayload,
  fieldKind,
  fieldLabelOf,
  fixItProgress,
  isInlineEditable,
} from './external-fix-it.util';

describe('external-fix-it.util', () => {
  describe('isInlineEditable', () => {
    it('is true for editable scalar fields across sections', () => {
      expect(isInlineEditable('socialSecurityNumber')).toBe(true);
      expect(isInlineEditable('appointmentInsuranceName')).toBe(true);
      expect(isInlineEditable('employerName')).toBe(true);
    });

    it('is false for documents and the dropped claim-information section', () => {
      expect(isInlineEditable('documents')).toBe(false);
      expect(isInlineEditable('claimInformation')).toBe(false);
    });
  });

  describe('fieldKind', () => {
    it('classifies select + special-widget fields', () => {
      expect(fieldKind('documents')).toBe('document');
      expect(fieldKind('genderId')).toBe('gender');
      expect(fieldKind('appointmentLanguageId')).toBe('language');
      expect(fieldKind('dateOfBirth')).toBe('date');
      expect(fieldKind('stateId')).toBe('state');
      expect(fieldKind('employerStateId')).toBe('state');
      expect(fieldKind('appointmentClaimExaminerStateId')).toBe('state');
      expect(fieldKind('firstName')).toBe('text');
    });
  });

  describe('fieldLabelOf', () => {
    it('disambiguates shared field names by section', () => {
      expect(fieldLabelOf('employerStreet')).toBe('Employer Details: Street');
    });

    it('does not double up when group and label match', () => {
      expect(fieldLabelOf('documents')).toBe('Documents');
    });
  });

  describe('fixItProgress', () => {
    it('counts addressed flagged fields', () => {
      const flagged = ['dateOfBirth', 'street', 'documents'];
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
    it('maps flagged + edited fields verbatim, trimming values', () => {
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
        appointmentClaimExaminerEmail: 'ce@example.test',
        appointmentInsuranceName: 'Acme Mutual',
      });
    });

    it('omits edits to fields that were not flagged', () => {
      const payload = buildCorrectionsPayload(['street'], {
        street: '128 W 4th St',
        cellPhoneNumber: '(213) 555-0148',
      });
      expect(payload).toEqual({ street: '128 W 4th St' });
    });

    it('omits blank edits and the documents key', () => {
      const payload = buildCorrectionsPayload(['street', 'documents'], {
        street: '   ',
        documents: 'whatever',
      });
      expect(payload).toEqual({});
    });
  });
});
