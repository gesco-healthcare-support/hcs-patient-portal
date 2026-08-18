import {
  attorneyQuestionText,
  resolveAttorneyToggleAction,
  shouldAskAttorneyQuestion,
  type AttorneyQuestionViewer,
} from './attorney-question.util';

const PATIENT: AttorneyQuestionViewer = {
  isPatient: true,
  isApplicantAttorney: false,
  isDefenseAttorney: false,
};
const APPLICANT_ATTORNEY: AttorneyQuestionViewer = {
  isPatient: false,
  isApplicantAttorney: true,
  isDefenseAttorney: false,
};
const DEFENSE_ATTORNEY: AttorneyQuestionViewer = {
  isPatient: false,
  isApplicantAttorney: false,
  isDefenseAttorney: true,
};
const STAFF: AttorneyQuestionViewer = {
  isPatient: false,
  isApplicantAttorney: false,
  isDefenseAttorney: false,
};

describe('attorney-question.util', () => {
  describe('resolveAttorneyToggleAction', () => {
    // REGRESSION (2026-08-18, found live): removing the flag's `true` default gave it a third
    // state, and the subscriber's `!enabled` test could not tell "not answered yet" from
    // "answered No". A fresh booking therefore opened with a confirmation modal asking the
    // booker to confirm removing an attorney they had never been asked about.
    it('treats not-yet-answered as a question to leave alone, not a No', () => {
      expect(resolveAttorneyToggleAction(null)).toBe('unanswered');
      expect(resolveAttorneyToggleAction(undefined)).toBe('unanswered');
    });

    it('treats an explicit No as a toggle-off worth confirming', () => {
      expect(resolveAttorneyToggleAction(false)).toBe('confirm-off');
    });

    it('treats Yes as enabling the section', () => {
      expect(resolveAttorneyToggleAction(true)).toBe('enable');
    });
  });

  describe('shouldAskAttorneyQuestion', () => {
    it('does not ask an applicant attorney whether the applicant is represented', () => {
      expect(shouldAskAttorneyQuestion('applicantAttorney', APPLICANT_ATTORNEY)).toBe(false);
    });

    it('does not ask a defense attorney whether there is a defense attorney', () => {
      expect(shouldAskAttorneyQuestion('defenseAttorney', DEFENSE_ATTORNEY)).toBe(false);
    });

    it('still asks each attorney about the OTHER side', () => {
      // An applicant attorney has no special knowledge of whether a defense attorney exists,
      // so suppressing that question too would force a guess.
      expect(shouldAskAttorneyQuestion('defenseAttorney', APPLICANT_ATTORNEY)).toBe(true);
      expect(shouldAskAttorneyQuestion('applicantAttorney', DEFENSE_ATTORNEY)).toBe(true);
    });

    it('asks patients and staff both questions', () => {
      for (const viewer of [PATIENT, STAFF]) {
        expect(shouldAskAttorneyQuestion('applicantAttorney', viewer)).toBe(true);
        expect(shouldAskAttorneyQuestion('defenseAttorney', viewer)).toBe(true);
      }
    });
  });

  describe('attorneyQuestionText', () => {
    it('speaks plainly to a patient about their own representation', () => {
      expect(attorneyQuestionText('applicantAttorney', PATIENT)).toBe(
        'Do you have an attorney representing you?',
      );
    });

    it('uses claim language for everyone else', () => {
      // "Applicant" matches the section's own name and the language of the claim; only the
      // patient needs it translated.
      expect(attorneyQuestionText('applicantAttorney', STAFF)).toBe(
        'Is the applicant represented?',
      );
      expect(attorneyQuestionText('applicantAttorney', DEFENSE_ATTORNEY)).toBe(
        'Is the applicant represented?',
      );
    });

    it('asks the same defense question of every audience', () => {
      for (const viewer of [PATIENT, STAFF, APPLICANT_ATTORNEY, DEFENSE_ATTORNEY]) {
        expect(attorneyQuestionText('defenseAttorney', viewer)).toBe(
          'Is there a defense attorney?',
        );
      }
    });

    it('never uses the ambiguous word "include"', () => {
      // The defect being fixed: "Include" conflated "show me this section" with "this
      // appointment has an attorney".
      for (const viewer of [PATIENT, STAFF]) {
        expect(attorneyQuestionText('applicantAttorney', viewer).toLowerCase()).not.toContain(
          'include',
        );
        expect(attorneyQuestionText('defenseAttorney', viewer).toLowerCase()).not.toContain(
          'include',
        );
      }
    });
  });
});
