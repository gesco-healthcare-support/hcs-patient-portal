import type { ReEvaluationSourceDto } from '../../proxy/appointments/models';
import {
  loadsThroughSourceLookup,
  newerSourceNote,
  sectionsFromLookup,
  showsSourceDateOfBirth,
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

  it('loads every re-evaluation through the lookup, and re-request / re-book through the ordinary read', () => {
    // 2026-11-08 (W2): only the lookup walks to the newest case in the chain.
    expect(loadsThroughSourceLookup('reval')).toBeTrue();
    expect(loadsThroughSourceLookup('reBook')).toBeFalse();
    expect(loadsThroughSourceLookup('reRequest')).toBeFalse();
  });

  it('names the newer case when the form was filled from it, and says nothing otherwise', () => {
    expect(
      newerSourceNote({
        filledFromNewer: true,
        source: { appointment: { requestConfirmationNumber: 'A00012' } },
      }),
    ).toBe('A newer evaluation exists for this case (A00012); we filled the form from that one.');
    expect(
      newerSourceNote({
        filledFromNewer: false,
        source: { appointment: { requestConfirmationNumber: 'A00004' } },
      }),
    ).toBeNull();
    expect(newerSourceNote({ filledFromNewer: true, source: { appointment: null } })).toBeNull();
  });
});
