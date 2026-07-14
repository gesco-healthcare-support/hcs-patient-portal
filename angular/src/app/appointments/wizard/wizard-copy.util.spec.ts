import { reviewSubmitNote, wizardEyebrow, wizardSubtitle, wizardTitle } from './wizard-copy.util';

describe('wizard-copy.util', () => {
  describe('wizardTitle', () => {
    it('uses "Book" wording for internal staff', () => {
      expect(wizardTitle(true, false)).toBe('Book an Appointment');
      expect(wizardTitle(true, true)).toBe('Book a Re-evaluation');
    });
    it('keeps the "Request" wording for external users', () => {
      expect(wizardTitle(false, false)).toBe('Request an Appointment');
      expect(wizardTitle(false, true)).toBe('Request a Re-evaluation');
    });
  });

  describe('wizardSubtitle', () => {
    it('mentions booking on behalf for internal staff', () => {
      expect(wizardSubtitle(true, false)).toContain('on behalf of the patient');
      expect(wizardSubtitle(true, true)).toContain('on behalf of the patient');
    });
    it('keeps the self-service wording for external users', () => {
      expect(wizardSubtitle(false, false)).toBe(
        'Complete the steps below. Your progress is saved automatically as a draft.',
      );
    });
  });

  describe('wizardEyebrow', () => {
    it('labels staff bookings', () => {
      expect(wizardEyebrow(true, false)).toBe('Staff booking');
    });
    it('labels follow-ups the same for either audience', () => {
      expect(wizardEyebrow(true, true)).toBe('Follow-up evaluation');
      expect(wizardEyebrow(false, true)).toBe('Follow-up evaluation');
    });
    it('labels new external evaluations', () => {
      expect(wizardEyebrow(false, false)).toBe('New evaluation');
    });
  });

  describe('reviewSubmitNote', () => {
    it('tells staff they can edit afterward', () => {
      expect(reviewSubmitNote(true)).toContain('edit the appointment afterward');
    });
    it('warns external users they cannot self-edit', () => {
      expect(reviewSubmitNote(false)).toContain('cannot edit the request yourself');
    });
  });
});
