import { ChangeRequestConsentStatus } from '../../proxy/appointment-change-requests/change-request-consent-status.enum';
import { ChangeRequestSide } from '../../proxy/appointment-change-requests/change-request-side.enum';
import {
  changeRequestAgeClass,
  changeRequestAgeDays,
  changeRequestConsentView,
  consentBlockNote,
  consentBlocksApproval,
  requestingSideLabel,
} from './cr-inbox.util';

describe('cr-inbox.util', () => {
  const now = new Date('2026-06-15T12:00:00Z').getTime();

  describe('changeRequestAgeDays', () => {
    it('counts whole days since the request was filed', () => {
      expect(changeRequestAgeDays('2026-06-13T12:00:00Z', now)).toBe(2);
      expect(changeRequestAgeDays('2026-06-15T12:00:00Z', now)).toBe(0);
    });
    it('clamps to 0 for a future or unparseable date', () => {
      expect(changeRequestAgeDays('2026-06-20T12:00:00Z', now)).toBe(0);
      expect(changeRequestAgeDays('not-a-date', now)).toBe(0);
      expect(changeRequestAgeDays(null, now)).toBe(0);
    });
  });

  describe('changeRequestAgeClass', () => {
    it('buckets by waiting age', () => {
      expect(changeRequestAgeClass(0)).toBe('ok');
      expect(changeRequestAgeClass(3)).toBe('ok');
      expect(changeRequestAgeClass(4)).toBe('warn');
      expect(changeRequestAgeClass(6)).toBe('warn');
      expect(changeRequestAgeClass(7)).toBe('crit');
    });
  });

  describe('changeRequestConsentView (two-sided)', () => {
    const S = ChangeRequestConsentStatus;
    it('hides the chip when neither side is in play', () => {
      expect(changeRequestConsentView(S.NotRequired, S.NotRequired).show).toBe(false);
      expect(changeRequestConsentView(null, null).show).toBe(false);
    });
    it('shows "received" only when every in-play side is approved', () => {
      expect(changeRequestConsentView(S.Approved, S.Approved)).toEqual({
        show: true,
        label: 'Consent received',
        cls: 'agreed',
      });
      expect(changeRequestConsentView(S.NotRequired, S.Approved).label).toBe('Consent received');
    });
    it('is pending when an in-play side is still pending', () => {
      expect(changeRequestConsentView(S.Approved, S.Pending).cls).toBe('pending');
      expect(changeRequestConsentView(S.Pending, S.NotRequired).cls).toBe('pending');
    });
    it('is declined/expired when any side rejected or expired', () => {
      expect(changeRequestConsentView(S.Approved, S.Rejected).cls).toBe('declined');
      expect(changeRequestConsentView(S.Approved, S.Rejected).label).toBe('Consent declined');
      expect(changeRequestConsentView(S.Expired, S.Approved).label).toBe('Consent expired');
    });
  });

  describe('consentBlocksApproval (two-sided)', () => {
    const S = ChangeRequestConsentStatus;
    it('blocks approval when any side is pending, declined, or expired', () => {
      expect(consentBlocksApproval(S.Pending, S.Approved)).toBe(true);
      expect(consentBlocksApproval(S.Approved, S.Rejected)).toBe(true);
      expect(consentBlocksApproval(S.Expired, S.NotRequired)).toBe(true);
    });
    it('allows approval only when every side is granted or not required', () => {
      expect(consentBlocksApproval(S.Approved, S.Approved)).toBe(false);
      expect(consentBlocksApproval(S.NotRequired, S.Approved)).toBe(false);
      expect(consentBlocksApproval(S.NotRequired, S.NotRequired)).toBe(false);
      expect(consentBlocksApproval(null, null)).toBe(false);
    });
  });

  describe('consentBlockNote (two-sided)', () => {
    const S = ChangeRequestConsentStatus;
    it('returns a corrective note naming the blocking condition', () => {
      expect(consentBlockNote(S.Pending, S.Approved)).toContain('still pending');
      expect(consentBlockNote(S.Approved, S.Rejected)).toContain('declined');
      expect(consentBlockNote(S.Expired, S.Approved)).toContain('expired');
    });
    it('is null when approval is allowed (every side granted or not required)', () => {
      expect(consentBlockNote(S.Approved, S.Approved)).toBeNull();
      expect(consentBlockNote(S.NotRequired, S.Approved)).toBeNull();
      expect(consentBlockNote(null, null)).toBeNull();
    });
  });

  describe('requestingSideLabel', () => {
    it('names the filing side', () => {
      expect(requestingSideLabel(ChangeRequestSide.SideA)).toBe('Applicant side');
      expect(requestingSideLabel(ChangeRequestSide.SideB)).toBe('Defense side');
      expect(requestingSideLabel(null)).toBe('');
    });
  });
});
