import {
  authorizedUserRoleLabel,
  authorizedUserRoleOptions,
  hasOnlyExternalRoles,
  isApplicantSideExternal,
  isDefenseSideExternal,
  isLawOfficeExternal,
  isParalegalRole,
  manageableAccessorRoles,
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
    it('gives staff every role, both sides', () => {
      const o = authorizedUserRoleOptions(['Staff Supervisor'], true);
      expect(o).toEqual([
        'Patient',
        'Applicant Attorney',
        'Defense Attorney',
        'Claim Examiner',
        'Applicant Paralegal',
        'Defense Paralegal',
      ]);
    });

    it("gives a law-office booker only their own side's law-office roles", () => {
      for (const role of ['Applicant Attorney', 'Applicant Paralegal']) {
        expect(authorizedUserRoleOptions([role], false)).toEqual([
          'Applicant Attorney',
          'Applicant Paralegal',
        ]);
      }
      for (const role of ['Defense Attorney', 'Defense Paralegal']) {
        expect(authorizedUserRoleOptions([role], false)).toEqual([
          'Defense Attorney',
          'Defense Paralegal',
        ]);
      }
    });

    it('never offers an external caller the other side (the server answers 400)', () => {
      const applicant = authorizedUserRoleOptions(['Applicant Attorney'], false);
      expect(applicant).not.toContain('Defense Attorney');
      expect(applicant).not.toContain('Defense Paralegal');
      const defense = authorizedUserRoleOptions(['Defense Paralegal'], false);
      expect(defense).not.toContain('Applicant Attorney');
      expect(defense).not.toContain('Applicant Paralegal');
    });

    it('gives a patient nothing and a claim examiner only the Claim Examiner role', () => {
      expect(authorizedUserRoleOptions(['Patient'], false)).toEqual([]);
      expect(authorizedUserRoleOptions(['Claim Examiner'], false)).toEqual(['Claim Examiner']);
    });
  });
});

describe('authorizedUserRoleLabel', () => {
  it('shows paralegals by side and leaves other roles as they are', () => {
    expect(authorizedUserRoleLabel('Applicant Paralegal')).toBe('Paralegal (Applicant side)');
    expect(authorizedUserRoleLabel('Defense Paralegal')).toBe('Paralegal (Defense side)');
    expect(authorizedUserRoleLabel('Patient')).toBe('Patient');
  });

  it('labels an external claim examiner helper as an assistant (a localization key)', () => {
    expect(authorizedUserRoleLabel('Claim Examiner')).toBe('Claim Examiner');
    expect(authorizedUserRoleLabel('Claim Examiner', true)).toBe(
      '::Appointment:AuthorizedUsers:ClaimExaminerAssistant',
    );
  });
});

describe('manageableAccessorRoles (own-side Authorized Users after booking)', () => {
  const base = {
    isBooker: false,
    isAuthorizedUser: false,
    applicantAttorneyEmail: 'applicant.lawyer@example.test',
    defenseAttorneyEmail: 'defense.lawyer@example.test',
    claimExaminerEmail: 'examiner@example.test',
  };

  it('offers the named defense attorney the defense side only', () => {
    expect(
      manageableAccessorRoles({
        ...base,
        roles: ['Defense Attorney'],
        verifiedEmail: 'Defense.Lawyer@example.test',
      }),
    ).toEqual(['Defense Attorney', 'Defense Paralegal']);
  });

  it('does not treat an attorney with an unverified or non-matching email as named', () => {
    expect(
      manageableAccessorRoles({ ...base, roles: ['Defense Attorney'], verifiedEmail: null }),
    ).toEqual([]);
    expect(
      manageableAccessorRoles({
        ...base,
        roles: ['Defense Attorney'],
        verifiedEmail: 'applicant.lawyer@example.test',
      }),
    ).toEqual([]);
  });

  it('does not make a paralegal the named attorney by email alone', () => {
    expect(
      manageableAccessorRoles({
        ...base,
        roles: ['Defense Paralegal'],
        verifiedEmail: 'defense.lawyer@example.test',
      }),
    ).toEqual([]);
  });

  it('offers a booker or Authorized User paralegal their own side only', () => {
    expect(
      manageableAccessorRoles({ ...base, roles: ['Applicant Paralegal'], isBooker: true }),
    ).toEqual(['Applicant Attorney', 'Applicant Paralegal']);
    expect(
      manageableAccessorRoles({ ...base, roles: ['Defense Paralegal'], isAuthorizedUser: true }),
    ).toEqual(['Defense Attorney', 'Defense Paralegal']);
  });

  it('judges a firm holding both sides per side', () => {
    expect(
      manageableAccessorRoles({
        ...base,
        roles: ['Applicant Attorney', 'Defense Attorney'],
        verifiedEmail: 'defense.lawyer@example.test',
      }),
    ).toEqual(['Defense Attorney', 'Defense Paralegal']);
  });

  it('offers a claim examiner party Claim Examiner helpers only', () => {
    expect(
      manageableAccessorRoles({
        ...base,
        roles: ['Claim Examiner'],
        verifiedEmail: 'examiner@example.test',
      }),
    ).toEqual(['Claim Examiner']);
    expect(
      manageableAccessorRoles({ ...base, roles: ['Claim Examiner'], isAuthorizedUser: true }),
    ).toEqual(['Claim Examiner']);
    expect(manageableAccessorRoles({ ...base, roles: ['Claim Examiner'] })).toEqual([]);
  });

  it('offers a patient nothing, even as booker', () => {
    expect(
      manageableAccessorRoles({
        ...base,
        roles: ['Patient'],
        isBooker: true,
        isAuthorizedUser: true,
        verifiedEmail: 'examiner@example.test',
      }),
    ).toEqual([]);
  });
});
