import {
  allFixed,
  buildCorrectionsPayload,
  buildInjuryCorrections,
  fieldKind,
  fieldLabelOf,
  fixItProgress,
  injuryRowToDraft,
  isInlineEditable,
  type InjuryCorrectionRow,
} from './external-fix-it.util';

describe('external-fix-it.util', () => {
  describe('isInlineEditable', () => {
    it('is true for editable scalar fields across sections', () => {
      expect(isInlineEditable('socialSecurityNumber')).toBe(true);
      expect(isInlineEditable('appointmentInsuranceName')).toBe(true);
      expect(isInlineEditable('employerName')).toBe(true);
    });

    it('is false for documents and the claim-information collection section', () => {
      // Both use a dedicated editor (re-upload / repeating injury editor), not the scalar loop.
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

  describe('injuryRowToDraft', () => {
    it('splits the comma summary into body parts and trims dates to yyyy-MM-dd', () => {
      const row: InjuryCorrectionRow = {
        dateOfInjury: '2025-03-04T00:00:00Z',
        toDateOfInjury: '2025-06-01T12:30:00Z',
        claimNumber: 'CLM-1',
        isCumulativeInjury: true,
        wcabAdj: 'ADJ-9',
        bodyPartsSummary: 'Lower back,  Neck , Right shoulder',
        wcabOfficeId: 'office-guid',
      };

      const draft = injuryRowToDraft(row);

      expect(draft.dateOfInjury).toBe('2025-03-04');
      expect(draft.toDateOfInjury).toBe('2025-06-01');
      expect(draft.claimNumber).toBe('CLM-1');
      expect(draft.isCumulativeInjury).toBe(true);
      expect(draft.wcabAdj).toBe('ADJ-9');
      expect(draft.wcabOfficeId).toBe('office-guid');
      expect(draft.bodyParts).toEqual(['Lower back', 'Neck', 'Right shoulder']);
      expect(draft.bodyPartsSummary).toBe('Lower back,  Neck , Right shoulder');
    });

    it('is null-safe on empty summary and absent optional fields', () => {
      const row: InjuryCorrectionRow = {
        dateOfInjury: '2025-01-01',
        toDateOfInjury: null,
        claimNumber: 'CLM-2',
        isCumulativeInjury: false,
        wcabAdj: 'ADJ-1',
        bodyPartsSummary: '',
        wcabOfficeId: null,
      };

      const draft = injuryRowToDraft(row);

      expect(draft.toDateOfInjury).toBeNull();
      expect(draft.wcabOfficeId).toBeNull();
      expect(draft.bodyParts).toEqual([]);
      expect(draft.bodyPartsSummary).toBe('');
    });
  });

  describe('buildInjuryCorrections', () => {
    it('maps drafts to the replacement payload, passing the derived summary through', () => {
      const rows = buildInjuryCorrections([
        {
          isCumulativeInjury: false,
          dateOfInjury: '2025-02-02',
          toDateOfInjury: null,
          claimNumber: 'CLM-3',
          wcabOfficeId: null,
          wcabAdj: 'ADJ-3',
          bodyParts: ['Neck', 'Back'],
          bodyPartsSummary: 'Neck, Back',
        },
      ]);

      expect(rows).toEqual([
        {
          dateOfInjury: '2025-02-02',
          toDateOfInjury: null,
          claimNumber: 'CLM-3',
          isCumulativeInjury: false,
          wcabAdj: 'ADJ-3',
          bodyPartsSummary: 'Neck, Back',
          wcabOfficeId: null,
        },
      ]);
    });

    it('produces an empty set for no drafts', () => {
      expect(buildInjuryCorrections([])).toEqual([]);
    });
  });
});
