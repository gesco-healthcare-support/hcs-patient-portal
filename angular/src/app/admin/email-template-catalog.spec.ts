import {
  catalogEntryFor,
  EMAIL_TEMPLATE_CATALOG,
  groupTemplatesByLifecycle,
  KNOWN_TEMPLATE_CODES,
  matchesTemplateQuery,
  TEMPLATE_GROUPS,
  TemplateGroupKey,
} from './email-template-catalog';

describe('email-template-catalog', () => {
  const groupKeys = new Set<TemplateGroupKey>(TEMPLATE_GROUPS.map((g) => g.key));

  describe('completeness guard', () => {
    it('lists 64 unique backend template codes', () => {
      expect(KNOWN_TEMPLATE_CODES.length).toBe(64);
      expect(new Set(KNOWN_TEMPLATE_CODES).size).toBe(KNOWN_TEMPLATE_CODES.length);
    });

    it('has a catalog entry with name, blurb, and valid group for every known code', () => {
      const missing = KNOWN_TEMPLATE_CODES.filter((code) => !EMAIL_TEMPLATE_CATALOG[code]);
      expect(missing).withContext('codes missing a catalog entry').toEqual([]);

      for (const code of KNOWN_TEMPLATE_CODES) {
        const entry = EMAIL_TEMPLATE_CATALOG[code];
        expect(entry.name.trim()).withContext(`name for ${code}`).not.toBe('');
        expect(entry.whenItFires.trim()).withContext(`blurb for ${code}`).not.toBe('');
        expect(groupKeys.has(entry.group)).withContext(`group for ${code}`).toBeTrue();
      }
    });

    it('has no catalog entry for an unknown code (no orphan metadata)', () => {
      const known = new Set(KNOWN_TEMPLATE_CODES);
      const orphans = Object.keys(EMAIL_TEMPLATE_CATALOG).filter((code) => !known.has(code));
      expect(orphans).withContext('catalog keys not in the backend set').toEqual([]);
    });
  });

  describe('catalogEntryFor', () => {
    it('returns the catalog entry for a known code', () => {
      const entry = catalogEntryFor('AppointmentApproved');
      expect(entry.name).toBe('Appointment approved');
      expect(entry.group).toBe('status');
    });

    it('falls back to a humanized name in the Other group for an unknown code', () => {
      const entry = catalogEntryFor('SomeBrandNewCode');
      expect(entry.name).toBe('Some Brand New Code');
      expect(entry.whenItFires).toBe('');
      expect(entry.group).toBe('other');
    });
  });

  describe('matchesTemplateQuery', () => {
    const entry = catalogEntryFor('ResetPassword');

    it('matches everything on an empty query', () => {
      expect(matchesTemplateQuery(entry, 'ResetPassword', '')).toBeTrue();
      expect(matchesTemplateQuery(entry, 'ResetPassword', '   ')).toBeTrue();
    });

    it('matches on the friendly name (case-insensitive)', () => {
      expect(matchesTemplateQuery(entry, 'ResetPassword', 'reset')).toBeTrue();
    });

    it('matches on the when-it-fires blurb', () => {
      expect(matchesTemplateQuery(entry, 'ResetPassword', 'reset link')).toBeTrue();
    });

    it('matches on the raw code', () => {
      expect(matchesTemplateQuery(entry, 'ResetPassword', 'resetpass')).toBeTrue();
    });

    it('does not match an unrelated term', () => {
      expect(matchesTemplateQuery(entry, 'ResetPassword', 'invoice')).toBeFalse();
    });
  });

  describe('groupTemplatesByLifecycle', () => {
    it('buckets rows into groups in display order and drops empty groups', () => {
      const rows = [
        { code: 'AppointmentApproved' }, // status
        { code: 'PasswordChange' }, // accounts
        { code: 'AppointmentBooked' }, // request
      ];
      const groups = groupTemplatesByLifecycle(rows);
      expect(groups.map((g) => g.key)).toEqual(['accounts', 'request', 'status']);
      expect(groups.every((g) => g.items.length > 0)).toBeTrue();
    });

    it('sorts templates within a group by friendly name', () => {
      const rows = [
        { code: 'ResetPassword' }, // "Password reset link"
        { code: 'PasswordChange' }, // "Password changed"
      ];
      const accounts = groupTemplatesByLifecycle(rows).find((g) => g.key === 'accounts');
      expect(accounts?.items.map((i) => i.entry.name)).toEqual([
        'Password changed',
        'Password reset link',
      ]);
    });

    it('places an un-catalogued code in the Other group without hiding it', () => {
      const groups = groupTemplatesByLifecycle([{ code: 'FutureUnmappedCode' }]);
      const other = groups.find((g) => g.key === 'other');
      expect(other?.items[0].row.code).toBe('FutureUnmappedCode');
      expect(other?.items[0].entry.name).toBe('Future Unmapped Code');
    });
  });
});
