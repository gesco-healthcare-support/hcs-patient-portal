import {
  reviewSubmitNote,
  sourceLookupBanner,
  wizardEyebrow,
  wizardSubtitle,
  wizardTitle,
} from './wizard-copy.util';

describe('wizard-copy.util', () => {
  describe('wizardTitle', () => {
    it('uses "Book" wording for internal staff', () => {
      expect(wizardTitle(true, 'new')).toBe('Book an Appointment');
      expect(wizardTitle(true, 'reval')).toBe('Book a Re-evaluation');
    });
    it('keeps the "Request" wording for external users', () => {
      expect(wizardTitle(false, 'new')).toBe('Request an Appointment');
      expect(wizardTitle(false, 'reval')).toBe('Request a Re-evaluation');
    });
    it('names a re-book as a replacement, for either audience', () => {
      expect(wizardTitle(true, 'reBook')).toBe('Book a Replacement Appointment');
      expect(wizardTitle(false, 'reBook')).toBe('Request a Replacement Appointment');
    });
    it('treats a re-request as an ordinary booking', () => {
      // Re-request re-enters the SAME appointment, so it keeps the plain heading rather
      // than announcing a distinct flow.
      expect(wizardTitle(true, 'reRequest')).toBe('Book an Appointment');
      expect(wizardTitle(false, 'reRequest')).toBe('Request an Appointment');
    });
  });

  describe('wizardSubtitle', () => {
    it('mentions booking on behalf for internal staff', () => {
      expect(wizardSubtitle(true, 'new')).toContain('on behalf of the patient');
      expect(wizardSubtitle(true, 'reval')).toContain('on behalf of the patient');
      expect(wizardSubtitle(true, 'reBook')).toContain('on behalf of the patient');
    });
    it('keeps the self-service wording for external users', () => {
      expect(wizardSubtitle(false, 'new')).toBe(
        'Complete the steps below. Your progress is saved automatically as a draft.',
      );
    });
    it('tells a re-booker the appointment did not take place', () => {
      expect(wizardSubtitle(false, 'reBook')).toBe(
        'Look up the appointment that did not take place, then choose a new date and time.',
      );
    });
  });

  describe('wizardEyebrow', () => {
    it('labels staff bookings', () => {
      expect(wizardEyebrow(true, 'new')).toBe('Staff booking');
    });
    it('labels follow-ups the same for either audience', () => {
      expect(wizardEyebrow(true, 'reval')).toBe('Follow-up evaluation');
      expect(wizardEyebrow(false, 'reval')).toBe('Follow-up evaluation');
    });
    it('labels re-books the same for either audience', () => {
      expect(wizardEyebrow(true, 'reBook')).toBe('Rebooking');
      expect(wizardEyebrow(false, 'reBook')).toBe('Rebooking');
    });
    it('labels new external evaluations', () => {
      expect(wizardEyebrow(false, 'new')).toBe('New evaluation');
    });
  });

  describe('sourceLookupBanner', () => {
    // This banner is the only thing telling a booker why Submit is refusing them, so each
    // flow must name the appointment it is actually asking for. It replaced a pair of
    // nested template ternaries that could only express two flows.
    it('asks a re-evaluation for the prior APPROVED appointment', () => {
      expect(sourceLookupBanner('reval')).toContain('prior approved appointment');
      expect(sourceLookupBanner('reval')).toContain('re-evaluation');
    });
    it('asks a re-book for the appointment being booked again', () => {
      expect(sourceLookupBanner('reBook')).toContain('book again');
      expect(sourceLookupBanner('reBook')).not.toContain('approved');
    });
    it('asks a re-request for the prior appointment', () => {
      expect(sourceLookupBanner('reRequest')).toContain('re-request');
    });
    it('says nothing for a plain booking, which has no source', () => {
      expect(sourceLookupBanner('new')).toBe('');
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
