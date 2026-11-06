import type { ReEvaluationSourceDto } from '../../proxy/appointments/models';
import {
  sectionsFromLookup,
  showsSourceDateOfBirth,
  usesDateOfBirthLookup,
} from './reval-lookup.util';

describe('reval-lookup util (2026-11-06)', () => {
  const found = {
    callerIsParty: false,
    source: {
      appointment: { id: 'a1' },
      appointmentEmployerDetail: { appointmentEmployerDetail: { employerName: 'TEST-Employer' } },
      appointmentInjuryDetails: [{ appointmentInjuryDetail: { claimNumber: 'TEST-CLM-1' } }],
    },
    applicantAttorney: { firstName: 'TEST-Avery' },
    defenseAttorney: null,
  } as unknown as ReEvaluationSourceDto;

  it('takes every section the form copies from the one answer', () => {
    const sections = sectionsFromLookup(found, []);

    expect(sections.employer?.employerName).toBe('TEST-Employer');
    expect(sections.injuries).toHaveSize(1);
    expect(sections.applicantAttorney?.firstName).toBe('TEST-Avery');
    expect(sections.defenseAttorney).toBeNull();
    expect(sections.accessors).toEqual([]);
  });

  it('copes with an answer that carries no employer or injuries', () => {
    const sections = sectionsFromLookup(
      {
        callerIsParty: true,
        source: { appointment: { id: 'a1' } },
      } as unknown as ReEvaluationSourceDto,
      [],
    );

    expect(sections.employer).toBeNull();
    expect(sections.injuries).toEqual([]);
    expect(sections.applicantAttorney).toBeNull();
  });

  it('shows the date-of-birth box to outside bookers re-evaluating, never to staff or other modes', () => {
    expect(showsSourceDateOfBirth('reval', false)).toBeTrue();
    expect(showsSourceDateOfBirth('reval', true)).toBeFalse();
    expect(showsSourceDateOfBirth('reBook', false)).toBeFalse();
    expect(showsSourceDateOfBirth('new', false)).toBeFalse();
  });

  it('uses the date-of-birth lookup only for an outside re-evaluation with a date typed', () => {
    expect(usesDateOfBirthLookup('reval', '1985-04-12', false)).toBeTrue();
    expect(usesDateOfBirthLookup('reval', '  ', false)).toBeFalse();
    expect(usesDateOfBirthLookup('reval', null, false)).toBeFalse();
    expect(usesDateOfBirthLookup('reval', '1985-04-12', true)).toBeFalse();
    expect(usesDateOfBirthLookup('reBook', '1985-04-12', false)).toBeFalse();
    expect(usesDateOfBirthLookup('reRequest', '1985-04-12', false)).toBeFalse();
  });
});
