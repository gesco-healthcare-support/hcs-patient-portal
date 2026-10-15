import { ExternalUserType } from '../proxy/external-signups/external-user-type.enum';
import { InvitationStatus } from '../proxy/invitations/invitation-status.enum';
import {
  avatarColor,
  expiryChip,
  initials,
  invitationStatusChip,
  isAttorneyType,
  primaryInternalRole,
  roleChipClass,
  userTypeFromName,
} from './users-hub.util';

describe('users-hub.util', () => {
  const NOW = Date.parse('2026-06-16T12:00:00Z');
  const iso = (daysFromNow: number) => new Date(NOW + daysFromNow * 86400000).toISOString();

  describe('userTypeFromName', () => {
    it('maps the People-hub names to enum values', () => {
      expect(userTypeFromName('Patient')).toBe(ExternalUserType.Patient);
      expect(userTypeFromName('ApplicantAttorney')).toBe(ExternalUserType.ApplicantAttorney);
      expect(userTypeFromName('DefenseAttorney')).toBe(ExternalUserType.DefenseAttorney);
      expect(userTypeFromName('ClaimExaminer')).toBe(ExternalUserType.ClaimExaminer);
    });
    it('defaults unknown/blank to Patient', () => {
      expect(userTypeFromName(null)).toBe(ExternalUserType.Patient);
      expect(userTypeFromName('Nonsense')).toBe(ExternalUserType.Patient);
    });
  });

  describe('isAttorneyType', () => {
    it('is true only for the two attorney roles', () => {
      expect(isAttorneyType(ExternalUserType.ApplicantAttorney)).toBe(true);
      expect(isAttorneyType(ExternalUserType.DefenseAttorney)).toBe(true);
      expect(isAttorneyType(ExternalUserType.Patient)).toBe(false);
      expect(isAttorneyType(ExternalUserType.ClaimExaminer)).toBe(false);
    });
  });

  describe('expiryChip', () => {
    it('shows a multi-day countdown for a healthy pending invite', () => {
      const chip = expiryChip(iso(5), InvitationStatus.Pending, NOW);
      expect(chip.cls).toBe('ok');
      expect(chip.label).toBe('5 days');
    });
    it('flags a soon-expiring pending invite (<= 1 day)', () => {
      expect(expiryChip(iso(1), InvitationStatus.Pending, NOW).cls).toBe('soon');
    });
    it('collapses a past-expiry pending invite to a dash', () => {
      const chip = expiryChip(iso(-1), InvitationStatus.Pending, NOW);
      expect(chip.cls).toBe('gone');
      expect(chip.label).toBe('--');
    });
    it('collapses any non-pending status to a dash regardless of date', () => {
      expect(expiryChip(iso(5), InvitationStatus.Accepted, NOW).cls).toBe('gone');
      expect(expiryChip(iso(5), InvitationStatus.Revoked, NOW).cls).toBe('gone');
    });
    it('collapses when no expiry date is present', () => {
      expect(expiryChip(null, InvitationStatus.Pending, NOW).cls).toBe('gone');
    });
  });

  describe('invitationStatusChip', () => {
    it('maps each status to its chip', () => {
      expect(invitationStatusChip(InvitationStatus.Pending)).toEqual({
        cls: 'invited',
        label: 'Pending',
      });
      expect(invitationStatusChip(InvitationStatus.Accepted)).toEqual({
        cls: 'linked',
        label: 'Accepted',
      });
      expect(invitationStatusChip(InvitationStatus.Expired)).toEqual({
        cls: 'none',
        label: 'Expired',
      });
      expect(invitationStatusChip(InvitationStatus.Revoked)).toEqual({
        cls: 'none',
        label: 'Revoked',
      });
    });
  });

  describe('roleChipClass', () => {
    it('marks Staff Supervisor purple and IT Admin green', () => {
      expect(roleChipClass('Staff Supervisor')).toBe('sup');
      expect(roleChipClass('IT Admin')).toBe('admin');
      expect(roleChipClass('Intake Staff')).toBe('');
    });
  });

  describe('primaryInternalRole', () => {
    it('prefers the highest internal role on the user', () => {
      expect(primaryInternalRole(['Patient', 'Staff Supervisor'])).toBe('Staff Supervisor');
      expect(primaryInternalRole(['Intake Staff'])).toBe('Intake Staff');
    });
    it('falls back to the first role, then a dash', () => {
      expect(primaryInternalRole(['Patient'])).toBe('Patient');
      expect(primaryInternalRole([])).toBe('--');
      expect(primaryInternalRole(null)).toBe('--');
    });
  });

  describe('initials', () => {
    it('uses first + last initials', () => {
      expect(initials('Ada', 'Lovelace')).toBe('AL');
    });
    it('falls back to email when no name', () => {
      expect(initials(null, null, 'sam@clinic.test')).toBe('S');
    });
    it('returns a placeholder when nothing is available', () => {
      expect(initials('', '', '')).toBe('?');
    });
  });

  describe('avatarColor', () => {
    it('is deterministic for the same seed', () => {
      expect(avatarColor('user-1')).toBe(avatarColor('user-1'));
    });
    it('returns a hex color from the palette', () => {
      expect(avatarColor('abc')).toMatch(/^#[0-9a-f]{6}$/);
    });
  });
});
