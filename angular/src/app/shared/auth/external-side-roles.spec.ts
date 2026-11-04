import {
  authorizedUserRoleLabel,
  authorizedUserRoleOptions,
  hasOnlyExternalRoles,
  isApplicantSideExternal,
  isDefenseSideExternal,
  isLawOfficeExternal,
  isParalegalRole,
  paralegalRoleForSide,
} from './external-user-roles';

describe('law-office side role helpers', () => {
  it('maps each side to attorney OR paralegal, case-insensitively', () => {
    expect(isApplicantSideExternal(['Applicant Attorney'])).toBeTrue();
    expect(isApplicantSideExternal([' applicant paralegal '])).toBeTrue();
    expect(isApplicantSideExternal(['Defense Paralegal'])).toBeFalse();
    expect(isDefenseSideExternal(['Defense Attorney'])).toBeTrue();
    expect(isDefenseSideExternal(['DEFENSE PARALEGAL'])).toBeTrue();
    expect(isDefenseSideExternal(['Applicant Attorney'])).toBeFalse();
    expect(isLawOfficeExternal(['Claim Examiner'])).toBeFalse();
    expect(isLawOfficeExternal(null)).toBeFalse();
  });

  it('recognises the two paralegal roles only', () => {
    expect(isParalegalRole(['Applicant Paralegal'])).toBeTrue();
    expect(isParalegalRole(['Defense Paralegal'])).toBeTrue();
    expect(isParalegalRole(['Applicant Attorney', 'Patient'])).toBeFalse();
  });

  it('names the paralegal role of a side as the server does', () => {
    expect(paralegalRoleForSide('applicant')).toBe('Applicant Paralegal');
    expect(paralegalRoleForSide('defense')).toBe('Defense Paralegal');
  });

  it('treats a paralegal as an external-only user (routes to the external home)', () => {
    expect(hasOnlyExternalRoles(['Applicant Paralegal'])).toBeTrue();
    expect(hasOnlyExternalRoles(['Defense Paralegal'])).toBeTrue();
    expect(hasOnlyExternalRoles(['Defense Paralegal', 'Staff Supervisor'])).toBeFalse();
  });

  describe('authorizedUserRoleOptions', () => {
    it('gives staff both paralegal roles', () => {
      const o = authorizedUserRoleOptions(['Staff Supervisor'], true);
      expect(o).toContain('Applicant Paralegal');
      expect(o).toContain('Defense Paralegal');
    });

    it("gives a law-office booker only their own side's paralegal", () => {
      expect(authorizedUserRoleOptions(['Applicant Attorney'], false)).toEqual([
        'Patient',
        'Applicant Attorney',
        'Defense Attorney',
        'Claim Examiner',
        'Applicant Paralegal',
      ]);
      expect(authorizedUserRoleOptions(['Defense Paralegal'], false)).toContain(
        'Defense Paralegal',
      );
      expect(authorizedUserRoleOptions(['Defense Paralegal'], false)).not.toContain(
        'Applicant Paralegal',
      );
    });

    it('gives other external roles no paralegal option', () => {
      expect(authorizedUserRoleOptions(['Patient'], false).length).toBe(4);
    });
  });
});

describe('authorizedUserRoleLabel', () => {
  it('shows paralegals by side and leaves other roles as they are', () => {
    expect(authorizedUserRoleLabel('Applicant Paralegal')).toBe('Paralegal (Applicant side)');
    expect(authorizedUserRoleLabel('Defense Paralegal')).toBe('Paralegal (Defense side)');
    expect(authorizedUserRoleLabel('Patient')).toBe('Patient');
  });
});
