import {
  attorneyChange,
  buildStaffPartiesPayload,
  claimExaminerChange,
  partiesPayloadProblem,
  type StaffPartiesInput,
} from './staff-parties.util';

/**
 * The staff parties body (2026-10-21). The property that matters most: a party
 * staff did NOT touch is never sent, so an unrelated save never rewrites an
 * attorney, never moves an access-key column and never mails anyone.
 */
describe('staff-parties.util', () => {
  const da = (email: string | null, extra: Record<string, unknown> = {}) => ({
    defenseAttorneyFirstName: 'TEST-Dana',
    defenseAttorneyLastName: 'TEST-Defense',
    defenseAttorneyEmail: email,
    defenseAttorneyFirmName: 'TEST-Firm',
    ...extra,
  });

  const input = (over: Partial<StaffPartiesInput> = {}): StaffPartiesInput => ({
    raw: {},
    initial: {},
    claimExaminer: {},
    initialClaimExaminer: {},
    removed: { applicantAttorney: false, defenseAttorney: false, claimExaminer: false },
    ...over,
  });

  describe('attorneyChange', () => {
    it('sends a newly typed attorney with its email, and no record id', () => {
      const change = attorneyChange(
        da('new@example.test'),
        {},
        'defenseAttorney',
        false,
        'master-1',
      );

      expect(change?.remove).toBeFalse();
      expect(change?.email).toBe('new@example.test');
      expect(change?.firstName).toBe('TEST-Dana');
      expect(change?.firmName).toBe('TEST-Firm');
      expect(change?.attorneyId).toBeNull();
    });

    it('keeps the loaded record id while the email is unchanged (any capitalisation)', () => {
      const change = attorneyChange(
        da('DA@Example.test', { defenseAttorneyPhoneNumber: '8185550100' }),
        da('da@example.test'),
        'defenseAttorney',
        false,
        'master-1',
      );

      expect(change?.attorneyId).toBe('master-1');
    });

    it('drops the loaded record id when the email changed, so attorney A is never rewritten as B', () => {
      const change = attorneyChange(
        da('b@example.test'),
        da('a@example.test'),
        'defenseAttorney',
        false,
        'master-a',
      );

      expect(change?.attorneyId).toBeNull();
      expect(change?.email).toBe('b@example.test');
    });

    it('sends nothing for an untouched attorney', () => {
      expect(
        attorneyChange(da('a@example.test'), da('a@example.test'), 'defenseAttorney', false, 'm'),
      ).toBeNull();
    });

    it('sends a removal only when the edit started with an attorney', () => {
      expect(attorneyChange({}, da('a@example.test'), 'defenseAttorney', true, 'm')).toEqual({
        remove: true,
      });
      expect(attorneyChange({}, {}, 'defenseAttorney', true, null)).toBeNull();
    });

    it('treats a section emptied without Remove as no change', () => {
      expect(attorneyChange({}, da('a@example.test'), 'defenseAttorney', false, 'm')).toBeNull();
    });
  });

  describe('claimExaminerChange', () => {
    it('sends a changed examiner, trimmed', () => {
      const change = claimExaminerChange(
        { name: ' TEST-Cleo ', email: 'ce@example.test' },
        {},
        false,
      );

      expect(change?.remove).toBeFalse();
      expect(change?.name).toBe('TEST-Cleo');
      expect(change?.email).toBe('ce@example.test');
    });

    it('sends nothing when nothing changed, and a removal only for an existing examiner', () => {
      expect(
        claimExaminerChange({ email: 'ce@example.test' }, { email: 'ce@example.test' }, false),
      ).toBeNull();
      expect(claimExaminerChange({}, { email: 'ce@example.test' }, true)).toEqual({ remove: true });
      expect(claimExaminerChange({}, {}, true)).toBeNull();
    });
  });

  describe('buildStaffPartiesPayload', () => {
    it('reports no changes for an untouched form, so the parties call is skipped', () => {
      const same = { patientEmail: 'p@example.test', ...da('a@example.test') };

      const { hasChanges, payload } = buildStaffPartiesPayload(input({ raw: same, initial: same }));

      expect(hasChanges).toBeFalse();
      expect(payload.patientEmail).toBeNull();
      expect(payload.defenseAttorney).toBeNull();
    });

    it('sends the patient email only when staff changed it', () => {
      const { payload, hasChanges } = buildStaffPartiesPayload(
        input({
          raw: { patientEmail: 'new@example.test' },
          initial: { patientEmail: 'old@example.test' },
        }),
      );

      expect(hasChanges).toBeTrue();
      expect(payload.patientEmail).toBe('new@example.test');
    });

    it('carries the removal flags through', () => {
      const { payload } = buildStaffPartiesPayload(
        input({
          initial: da('a@example.test'),
          initialClaimExaminer: { email: 'ce@example.test' },
          removed: { applicantAttorney: false, defenseAttorney: true, claimExaminer: true },
        }),
      );

      expect(payload.defenseAttorney).toEqual({ remove: true });
      expect(payload.claimExaminer).toEqual({ remove: true });
      expect(payload.applicantAttorney).toBeNull();
    });
  });

  describe('partiesPayloadProblem', () => {
    it('asks for the email of a party being added', () => {
      const message = partiesPayloadProblem({
        defenseAttorney: { remove: false, firmName: 'TEST-Firm' },
        claimExaminer: { remove: false, name: 'TEST-Cleo' },
      });

      expect(message).toContain('defense attorney and claim examiner');
    });

    it('asks for the firm name of an attorney being added', () => {
      expect(
        partiesPayloadProblem({ applicantAttorney: { remove: false, email: 'aa@example.test' } }),
      ).toContain('firm name');
    });

    it('accepts removals and complete parties', () => {
      expect(
        partiesPayloadProblem({
          applicantAttorney: { remove: true },
          defenseAttorney: { remove: false, email: 'da@example.test', firmName: 'TEST-Firm' },
          claimExaminer: { remove: false, email: 'ce@example.test' },
        }),
      ).toBeNull();
    });
  });
});
