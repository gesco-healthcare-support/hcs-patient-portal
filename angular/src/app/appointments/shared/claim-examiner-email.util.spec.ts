import { claimExaminerEmailMayBeBlank } from './claim-examiner-email.util';

/** When the booking wizard stops asking for the claim examiner email (2026-11-13). */
describe('claimExaminerEmailMayBeBlank', () => {
  const real = { enabled: true, email: 'jane.doe@firm.test' };

  it('is optional for a law-office booker once the other side has a real attorney email', () => {
    expect(claimExaminerEmailMayBeBlank('applicant', real)).toBeTrue();
    expect(claimExaminerEmailMayBeBlank('defense', real)).toBeTrue();
  });

  it('stays required for staff, patient and claim examiner bookers (no law-office side)', () => {
    expect(claimExaminerEmailMayBeBlank(null, real)).toBeFalse();
  });

  it('stays required while the other side has no usable attorney email', () => {
    expect(
      claimExaminerEmailMayBeBlank('applicant', { enabled: false, email: real.email }),
    ).toBeFalse();
    expect(claimExaminerEmailMayBeBlank('applicant', { enabled: true, email: '' })).toBeFalse();
    expect(claimExaminerEmailMayBeBlank('applicant', { enabled: true, email: null })).toBeFalse();
    expect(
      claimExaminerEmailMayBeBlank('applicant', { enabled: true, email: 'not-an-email' }),
    ).toBeFalse();
    expect(
      claimExaminerEmailMayBeBlank('defense', { enabled: true, email: 'unknown@firm.test' }),
    ).toBeFalse();
  });
});
