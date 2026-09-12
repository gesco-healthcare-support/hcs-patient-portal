import {
  avatarColor,
  derivePortalStatus,
  emptyPeopleFilters,
  initials,
  maskSsn,
  matchesPeopleFilters,
  matchesPeopleSearch,
  PEOPLE_SECTIONS,
  PersonRow,
} from './people.util';

function row(partial: Partial<PersonRow>): PersonRow {
  return { id: 'x', firstName: '', lastName: '', portal: 'none', ...partial };
}

describe('people.util', () => {
  describe('PEOPLE_SECTIONS', () => {
    it('defines the four sections in rail order', () => {
      expect(PEOPLE_SECTIONS.map((s) => s.key)).toEqual(['patients', 'aa', 'da', 'ce']);
    });

    it('flags only patients as the detail section and only attorneys as firm sections', () => {
      const byKey = (k: string) => PEOPLE_SECTIONS.find((s) => s.key === k)!;
      expect(byKey('patients').isPatient).toBe(true);
      expect(byKey('aa').hasFirm).toBe(true);
      expect(byKey('da').hasFirm).toBe(true);
      expect(byKey('ce').hasFirm).toBe(false);
      expect(byKey('aa').isPatient).toBe(false);
    });
  });

  describe('derivePortalStatus', () => {
    const invited = new Set<string>(['pat@example.test']);

    it('returns linked when an identity user is attached (regardless of invite)', () => {
      expect(derivePortalStatus('user-1', 'pat@example.test', invited)).toBe('linked');
    });

    it('returns invited when the email has an active invitation (case-insensitive)', () => {
      expect(derivePortalStatus(null, 'PAT@example.test', invited)).toBe('invited');
    });

    it('returns none when neither linked nor invited', () => {
      expect(derivePortalStatus(null, 'other@example.test', invited)).toBe('none');
    });

    it('returns none for an attorney with no email even if the set is non-empty', () => {
      expect(derivePortalStatus(null, null, invited)).toBe('none');
    });
  });

  describe('matchesPeopleSearch', () => {
    const r = row({
      firstName: 'Daniel',
      lastName: 'Brooks',
      email: 'dbrooks@firm.test',
      phoneNumber: '(213) 555-0101',
      firmName: 'Brooks & Associates',
    });

    it('matches everything on an empty query', () => {
      expect(matchesPeopleSearch(r, '   ')).toBe(true);
    });

    it('matches on name, email, phone, and firm case-insensitively', () => {
      expect(matchesPeopleSearch(r, 'daniel brooks')).toBe(true);
      expect(matchesPeopleSearch(r, 'DBROOKS@firm')).toBe(true);
      expect(matchesPeopleSearch(r, '555-0101')).toBe(true);
      expect(matchesPeopleSearch(r, 'associates')).toBe(true);
    });

    it('does not match an unrelated query', () => {
      expect(matchesPeopleSearch(r, 'zzzz')).toBe(false);
    });
  });

  describe('maskSsn', () => {
    // Inputs are deliberately NOT in SSN format -- maskSsn strips non-digits and
    // keeps only the last four, so any digit-bearing string exercises it.
    it('returns empty string for empty input', () => {
      expect(maskSsn(null)).toBe('');
      expect(maskSsn('')).toBe('');
    });

    it('keeps only the last four digits from a labeled value', () => {
      expect(maskSsn('acct 7788')).toBe('***-**-7788');
    });

    it('keeps the LAST four when more digits are present', () => {
      // "id-1002-0030" -> digits 10020030 -> last four 0030
      expect(maskSsn('id-1002-0030')).toBe('***-**-0030');
    });

    it('is idempotent on an already-masked value', () => {
      expect(maskSsn('***-**-7788')).toBe('***-**-7788');
    });

    it('fully masks when fewer than four digits are present', () => {
      expect(maskSsn('12')).toBe('***-**-****');
    });
  });

  describe('matchesPeopleFilters', () => {
    const r = row({
      genderId: 2,
      dateOfBirth: '1985-06-14',
      city: 'Los Angeles',
      stateId: 'ca',
      appointmentLanguageId: 'en',
      portal: 'invited',
    });

    it('passes when no filters are set', () => {
      expect(matchesPeopleFilters(r, emptyPeopleFilters())).toBe(true);
    });

    it('filters by gender, city (contains), and portal', () => {
      expect(matchesPeopleFilters(r, { ...emptyPeopleFilters(), genderId: 2 })).toBe(true);
      expect(matchesPeopleFilters(r, { ...emptyPeopleFilters(), genderId: 1 })).toBe(false);
      expect(matchesPeopleFilters(r, { ...emptyPeopleFilters(), city: 'angeles' })).toBe(true);
      expect(matchesPeopleFilters(r, { ...emptyPeopleFilters(), portal: 'none' })).toBe(false);
    });

    it('filters by date-of-birth range inclusively', () => {
      expect(
        matchesPeopleFilters(r, {
          ...emptyPeopleFilters(),
          dobMin: '1985-01-01',
          dobMax: '1985-12-31',
        }),
      ).toBe(true);
      expect(matchesPeopleFilters(r, { ...emptyPeopleFilters(), dobMin: '1990-01-01' })).toBe(
        false,
      );
    });
  });

  describe('initials + avatarColor', () => {
    it('builds up to two uppercase initials and falls back to ?', () => {
      expect(initials('Daniel', 'Brooks')).toBe('DB');
      expect(initials('', '')).toBe('?');
    });

    it('returns a stable color for the same seed', () => {
      expect(avatarColor('Daniel Brooks')).toBe(avatarColor('Daniel Brooks'));
      expect(avatarColor('Daniel Brooks')).toMatch(/^hsl\(\d{1,3}, 55%, 45%\)$/);
    });
  });
});
